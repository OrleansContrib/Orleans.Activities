using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Hosting;
using System.Runtime.DurableInstancing;
using System.Threading;
using System.Xml.Linq;
using Orleans.Activities.AsyncEx;
using Orleans.Activities.Configuration;
using Orleans.Activities.Extensions;
using Orleans.Activities.Helpers;

namespace Orleans.Activities.Hosting
{
    // Main concept
    // - from outside a WorkflowGrain is indistinguishable from a normal Grain; WorkflowGrain is the only class that has dependency on Orleans runtime!
    // - each WorkflowGrain has it's own WorkflowHost
    // - each WorkflowHost hosts a WorkflowInstance (and recreates it when it aborts)
    // - if you want to access another workflow, access the WorkflowGrain that hosts it

    // WorkflowHost implementation details
    // - WorkflowHost is responsible to (re)start/(re)load WorkflowInstance if it is aborted (ie. prepare for execution),
    //   WorkflowInstance preparation happens on the next incoming operation (that can be also a default 120s auto-reactivation in a persisted runnable state)
    // - can only be used with a "single threaded", optionally reentrant scheduler,
    //   but WorkflowHost will degrade it always to non-reentrant in case of workflow/activities, this is a WFI (System.Activities.Hosting.WorkflowInstance) design requirement
    //   but in WFI non-reentrant means, it can't accept requests until it goes idle
    // - it maintains an idle async autoresetevent to queue the incoming operations,
    //   each incoming operation awaits for idle, schedules itself on the WorkflowInstance and calls RunAsync() on it
    // - after the RunAsync() is called on WorkflowInstance it returns immediately, but runs in the "background" until it runs out of work to do
    // - normal operations will await until their SendResponse activity completes their TaskCompletionSource,
    //   but the WorkflowInstance can continue it's work in the "background" if there are more activities to execute (until it gets idle),
    //   these activities are running on the "tail" of the operation that previously called the RunAsync(),
    //   other operations will await WorkflowInstance to be idle
    // - when WorkflowInstance gets idle, it calls OnNotifyIdle(), that by default sets the idle async autoresetevent,
    //   but during preparation, it doesn't set the idle async autoresetevent, it completes the preparation's TaskCompletionSource
    // - in case of an unhandled exception in the WorkflowInstance, OnUnhandledExceptionAsync() will be called, that by default calls the grain to eg. log it,
    //   but during preparation, it stores the first exception, and OnNotifyIdle() will send it to the preparation's TaskCompletionSource,
    //   and during workflow interface operation, it sends the exception to the operation's TaskCompletionSource immediately (not when it gets idle)

    /// <summary>
    /// WorkflowHost and WorkflowInstance are the main classes responsible to execute System.Activities workflows above an Orleans like single threaded reentrant scheduler.
    /// These classes convert the original persistence and timer API to an Orleans compatible persistence and reminder API, but remain compatible with legacy extensions and activities.
    /// These classes also implement an operation specific API to back grain's incoming and outgoing operations, in a way, where the main operation logic remains outside of activities,
    /// those are part of the grain implementation without depending on any workflow related functionality.
    /// <para>For implementation details see the detailed comments in the source code!</para>
    /// </summary>
    public class WorkflowHost : IWorkflowHost, IWorkflowInstanceCallback
    {
        #region protected/private fields

        private Func<WorkflowIdentity, Activity> workflowDefinitionFactory;
        private Func<WorkflowIdentity> workflowDefinitionIdentityFactory;

        private ActiveTaskCompletionSources activeTaskCompletionSources;
        private AsyncAutoResetEvent idle;
        private PreviousResponseParameterExtension previousResponseParameterExtension;

        protected IWorkflowHostCallback grain;
        protected WorkflowIdentity workflowDefinitionIdentity;
        protected IWorkflowInstance instance;

        #endregion

        #region ctor

        public WorkflowHost(IWorkflowHostCallback grain, Func<WorkflowIdentity, Activity> workflowDefinitionFactory, Func<WorkflowIdentity> workflowDefinitionIdentityFactory)
        {
            this.grain = grain;
            this.workflowDefinitionFactory = workflowDefinitionFactory;
            this.workflowDefinitionIdentityFactory = workflowDefinitionIdentityFactory ?? (() => null);

            activeTaskCompletionSources = new ActiveTaskCompletionSources();
            idle = new AsyncAutoResetEvent(true);
            previousResponseParameterExtension = new PreviousResponseParameterExtension();
        }

        #endregion

        #region IWorkflowHost/IWorkflowHostInfrastructure members

        public Task DeactivateAsync()
        {
            if (instance != null && instance.WorkflowInstanceState != WorkflowInstanceState.Aborted)
                return ScheduleInstanceAsync(Parameters.ResumeInfrastructureTimeout, () => instance.DeactivateAsync());
            else
                return TaskConstants.Completed;
        }

        public Task ReminderAsync(string reminderName) =>
            ResumeReminderBookmarkAsync(reminderName);

        #endregion

        #region IWorkflowHost/IWorkflowHostControl members

        public Func<IEnumerable<object>> ExtensionsFactory { private get; set; }

        public Func<Task<IDictionary<string, object>>> OnStartAsync { private get; set; }

        public Func<ActivityInstanceState, IDictionary<string, object>, Exception, Task> OnCompletedAsync { private get; set; }

        public Task RunAsync() =>
            PrepareInstanceAsync(Parameters.ResumeInfrastructureTimeout);

        public async Task<IDictionary<string, object>> RunToCompletionAsync()
        {
            TaskCompletionSource<object> taskCompletionSource = new TaskCompletionSource<object>();
            IDictionary<string, object> outputArguments = null;
            Exception terminationException = null;
            OnCompletedAsync = (ActivityInstanceState _activityInstanceState, IDictionary<string, object> _outputArguments, Exception _terminationException) =>
            {
                outputArguments = _outputArguments;
                terminationException = _terminationException;
                taskCompletionSource.SetResult(null);
                return TaskConstants.Completed;
            };

            try
            {
                // We run the instance like an operation, but without scheduling a bookmark resumption. And not a SendResponse activity sets the TCS but the OnCompleted event.
                await PrepareInstanceAsync(Parameters.ResumeInfrastructureTimeout, taskCompletionSource);
                // RunInstanceAsync will throw if wasn't successful, so awaiting TCS is safe now.
                await taskCompletionSource.Task;
            }
            finally
            {
                activeTaskCompletionSources.Remove(taskCompletionSource);
            }

            if (terminationException != null)
                throw terminationException;
            return outputArguments;
        }

        public Task AbortAsync(Exception reason) =>
            ScheduleInstanceAsync(Parameters.ResumeInfrastructureTimeout, () => instance.AbortAsync(reason));

        public Task CancelAsync() =>
            ScheduleAndRunInstanceAsync(Parameters.ResumeInfrastructureTimeout, () => instance.ScheduleCancelAsync());

        public Task TerminateAsync(Exception reason) =>
            ScheduleAndRunInstanceAsync(Parameters.ResumeInfrastructureTimeout, () => instance.TerminateAsync(reason));

        #endregion

        #region IWorkflowHost/IWorkflowHostOperations members

        public Task<TResponseParameter> OperationAsync<TRequestResult, TResponseParameter>(string operationName, Func<Task<TRequestResult>> requestResult)
                where TRequestResult : class
                where TResponseParameter : class =>
            ResumeOperationBookmarkAsync<TResponseParameter>(operationName, requestResult);

        public Task OperationAsync<TRequestResult>(string operationName, Func<Task<TRequestResult>> requestResult)
                where TRequestResult : class =>
            ResumeOperationBookmarkAsync(operationName, requestResult);

        public Task<TResponseParameter> OperationAsync<TResponseParameter>(string operationName, Func<Task> requestResult)
                where TResponseParameter : class =>
            ResumeOperationBookmarkAsync<TResponseParameter>(operationName, requestResult);

        public Task OperationAsync(string operationName, Func<Task> requestResult) =>
            ResumeOperationBookmarkAsync(operationName, requestResult);

        #endregion

        #region IWorkflowHost members helper methods

        // It waits for scheduling the resumption, and than waits for the response from the SendResponse activity, through the TaskCompletionSource.
        private async Task ResumeOperationBookmarkAsync(string operationName, object requestResult)
        {
            TaskCompletionSource<object> taskCompletionSource = new TaskCompletionSource<object>();
            try
            {
                await ResumeOperationBookmarkAsync(operationName, taskCompletionSource, requestResult, typeof(void),
                    (responseParameter, message) => new OperationRepeatedException(message));
                // ScheduleAndRunInstanceAsync will throw if scheduling the operation wasn't successful, so awaiting TCS is safe now.
                await taskCompletionSource.Task;
            }
            finally
            {
                activeTaskCompletionSources.Remove(taskCompletionSource);
            }
        }

        // It waits for scheduling the resumption, and than waits for the response from the SendResponse activity, through the TaskCompletionSource.
        private async Task<TResponseParameter> ResumeOperationBookmarkAsync<TResponseParameter>(string operationName, object requestResult)
            where TResponseParameter : class
        {
            TaskCompletionSource<TResponseParameter> taskCompletionSource = new TaskCompletionSource<TResponseParameter>();
            try
            {
                await ResumeOperationBookmarkAsync(operationName, taskCompletionSource, requestResult, typeof(TResponseParameter),
                    (responseParameter, message) => new OperationRepeatedException<TResponseParameter>(responseParameter as TResponseParameter, message));
                // ScheduleAndRunInstanceAsync will throw if scheduling the operation wasn't successful, so awaiting TCS is safe now.
                return await taskCompletionSource.Task;
            }
            finally
            {
                activeTaskCompletionSources.Remove(taskCompletionSource);
            }
        }

        // If the resumption didn't timed out nor aborted, but not found, it tries to return the previous response parameter if the operation was idempotent,
        // ie. throws OperationRepeatedException, or throws InvalidOperationException if the previous response is not known (didn't happen or not idempotent).
        private async Task ResumeOperationBookmarkAsync<TResponseParameter>(string operationName, TaskCompletionSource<TResponseParameter> taskCompletionSource,
                object requestResult, Type responseParameterType,
                Func<object, string, OperationRepeatedException> createOperationRepeatedException)
            where TResponseParameter : class
        {
            BookmarkResumptionResult result = await ScheduleAndRunInstanceAsync(Parameters.ResumeOperationTimeout,
                () => instance.ScheduleOperationBookmarkResumptionAsync(operationName, new object[] { taskCompletionSource, requestResult }),
                (_result) => _result == BookmarkResumptionResult.Success,
                taskCompletionSource);
            WorkflowInstanceState workflowInstanceState = instance.WorkflowInstanceState;

            if (result == BookmarkResumptionResult.NotFound
                || result == BookmarkResumptionResult.NotReady && workflowInstanceState == WorkflowInstanceState.Complete)
                previousResponseParameterExtension.ThrowPreviousResponseParameter(operationName, responseParameterType, createOperationRepeatedException);
            else if (result == BookmarkResumptionResult.NotReady) // && !Complete
                // Instance is created but the initialization RunAsync() hasn't been called, this is impossible.
                throw new InvalidOperationException($"Instance state is '{workflowInstanceState}', instance is not ready to process operation '{operationName}'.");
            //else // Success
        }

        private async Task ResumeReminderBookmarkAsync(string reminderName)
        {
            BookmarkResumptionResult result = await ScheduleAndRunInstanceAsync(Parameters.ResumeInfrastructureTimeout,
                () => instance.ScheduleReminderBookmarkResumptionAsync(reminderName),
                (_result) => _result == BookmarkResumptionResult.Success);
            WorkflowInstanceState workflowInstanceState = instance.WorkflowInstanceState;

            if (result == BookmarkResumptionResult.NotReady && workflowInstanceState != WorkflowInstanceState.Complete)
                // Instance is created but the initialization RunAsync() hasn't been called, this is impossible.
                throw new InvalidOperationException($"Instance state is '{workflowInstanceState}', instance is not ready to process reminder '{reminderName}'.");
            //else // NotFound, Complete or Success
                // If we don't find a reminder, it's not an issue, maybe the grain/silo crashed after the reminder was created but before the workflow state was persisted,
                // or the grain/silo crashed after persistence but before the reminder was unregistered,
                // it will be unregistered on the next persistence event.
                // See ReminderTable for the detailed description of the algorithm.
        }

        #endregion

        #region protected/private helper methods

        protected IParameters Parameters => grain.Parameters;

        private IEnumerable<object> Extensions =>
            ExtensionsFactory == null
            ? previousResponseParameterExtension.Yield()
            : ExtensionsFactory().Append(previousResponseParameterExtension);

        private Task<IDictionary<string, object>> RaiseOnStartAsync() =>
            OnStartAsync == null
            ? TaskConstants<IDictionary<string, object>>.Default
            : OnStartAsync();

        private Task RaiseOnCompletedAsync(ActivityInstanceState completionState, IDictionary<string, object> outputArguments, Exception terminationException) =>
            OnCompletedAsync == null
            ? TaskConstants.Completed
            : OnCompletedAsync(completionState, outputArguments, terminationException);

        private async Task WaitIdleAsync(TimeSpan timeout)
        {
            // TimeSpan.MaxValue uses Int64.MaxValue ticks, CTS compares it to Int32.MaxValue milliseconds to throw ArgumentOutOfRangeException...
            CancellationTokenSource cts = (timeout == TimeSpan.MaxValue ? new CancellationTokenSource() : new CancellationTokenSource(timeout));
            try
            {
                await idle.WaitAsync(cts.Token);
            }
            catch (TaskCanceledException exception)
            {
                // at this point this operation was not able to auto-reset idle, other operations will be able to wait for it
                throw new TimeoutException($"Operation can't be scheduled within timeout '{timeout}'.", exception);
            }
        }

        // TODO handle timeout? TimeoutException on this task, and finish preparation in the background and set idle at the end, problem: it shouldn't know about idle!
        // Can be called only after a successful WaitIdleAsync()!
        private async Task PrepareInstanceAsync()
        {
            if (instance != null && instance.WorkflowInstanceState == WorkflowInstanceState.Aborted)
            {
                // We try to restart or reload it from the previous persisted state.
                await grain.LoadWorkflowStateAsync();
                instance = null;
            }
            if (instance == null)
            {
                IWorkflowState workflowState = grain.WorkflowState;
                WorkflowIdentity workflowDefinitionIdentity;
                IWorkflowInstance instance;
                if (workflowState.InstanceValues == null)
                {
                    // Start, there is no previous persisted state.
                    workflowDefinitionIdentity = workflowDefinitionIdentityFactory();
                    instance = new WorkflowInstance(this, workflowDefinitionFactory(workflowDefinitionIdentity), workflowDefinitionIdentity);
                    instance.Start(await RaiseOnStartAsync(), Extensions);
                }
                else
                {
                    // Load previous persisted state.
                    instance = new WorkflowInstance(this, workflowDefinitionFactory(workflowState.WorkflowDefinitionIdentity), workflowState.WorkflowDefinitionIdentity);

                    // TODO If workflowState.WorkflowDefinitionIdentity differs from workflowDefinitionIdentity, we should create a DynamicUpdateMap and update the loaded instance.
                    //      Currently we downgrade the workflowDefinitionIdentity to the loaded value.
                    // NOTE The workflowDefinitionFactory usually yields the same singleton workflow definition (ie. activity) for the same WorkflowDefinitionIdentity,
                    //      what happens with these activity trees during update???
                    // await instance.LoadAsync(workflowState.InstanceValues, GetExtensions(), >>>DynamicUpdateMap: workflowState.WorkflowDefinitionIdentity -> workflowDefinitionIdentity<<<);

                    await instance.LoadAsync(workflowState.InstanceValues, Extensions);
                    workflowDefinitionIdentity = workflowState.WorkflowDefinitionIdentity;
                }
                // Set the the values only, when the instance was successfully initialized.
                this.workflowDefinitionIdentity = workflowDefinitionIdentity;
                this.instance = instance;
            }
            if (instance.WorkflowInstanceState == WorkflowInstanceState.Runnable)
            {
                TaskCompletionSource<object> taskCompletionSource = new TaskCompletionSource<object>();
                try
                {
                    activeTaskCompletionSources.ProtectionLevel = ActiveTaskCompletionSources.TaskCompletionSourceProtectionLevel.Preparation;
                    activeTaskCompletionSources.Add(taskCompletionSource);
                    await instance.RunAsync();
                    await taskCompletionSource.Task;
                }
                finally
                {
                    activeTaskCompletionSources.ProtectionLevel = ActiveTaskCompletionSources.TaskCompletionSourceProtectionLevel.Normal;
                    activeTaskCompletionSources.Remove(taskCompletionSource);
                }
            }
        }

        protected async Task PrepareInstanceAsync(TimeSpan timeout)
        {
            await WaitIdleAsync(timeout);
            try
            {
                await PrepareInstanceAsync();
            }
            finally // Yes, finally, because there is no RunAsync(), it's already happend optionally during PrepareInstanceAsync().
            {
                idle.Set();
            }
        }

        protected async Task PrepareInstanceAsync<TResponseParameter>(TimeSpan timeout, TaskCompletionSource<TResponseParameter> taskCompletionSource)
            where TResponseParameter : class
        {
            await WaitIdleAsync(timeout);
            try
            {
                await PrepareInstanceAsync();
                activeTaskCompletionSources.Add(taskCompletionSource);
            }
            finally // Yes, finally, because there is no RunAsync(), it's already happend optionally during PrepareInstanceAsync().
            {
                idle.Set();
            }
        }

        protected async Task ScheduleInstanceAsync(TimeSpan timeout, Func<Task> asyncActionToSchedule)
        {
            await WaitIdleAsync(timeout);
            try
            {
                await PrepareInstanceAsync();
                await asyncActionToSchedule();
            }
            finally // Yes, finally, because there is no RunAsync(), it's already happend optionally during PrepareInstanceAsync().
            {
                idle.Set();
            }
        }

        protected async Task ScheduleAndRunInstanceAsync(TimeSpan timeout, Func<Task> asyncActionToSchedule)
        {
            await WaitIdleAsync(timeout);
            try
            {
                await PrepareInstanceAsync();
                await asyncActionToSchedule();
                await instance.RunAsync();
            }
            catch
            {
                // TODO shouldn't we only set idle, if RunAsync() wasn't successful and instance.WorkflowInstanceState != WorkflowInstanceState.Runnable ???
                idle.Set();
                throw;
            }
        }

        protected async Task<TResult> ScheduleAndRunInstanceAsync<TResult>(TimeSpan timeout, Func<Task<TResult>> asyncFunctionToSchedule,
            Func<TResult, bool> isScheduleSuccessful)
        {
            await WaitIdleAsync(timeout);
            try
            {
                await PrepareInstanceAsync();
                TResult result = await asyncFunctionToSchedule();
                if (isScheduleSuccessful(result))
                    await instance.RunAsync();
                else
                    idle.Set();
                return result;
            }
            catch
            {
                // TODO shouldn't we only set idle, if RunAsync() wasn't successful and instance.WorkflowInstanceState != WorkflowInstanceState.Runnable ???
                idle.Set();
                throw;
            }
        }

        // If schedule is not successful, caller musn't await TCS, because it will never be completed (nor the WF, nor the host will complete it in any way)!
        protected async Task<TResult> ScheduleAndRunInstanceAsync<TResult, TResponseParameter>(TimeSpan timeout, Func<Task<TResult>> asyncFunctionToSchedule,
                Func<TResult, bool> isScheduleSuccessful, TaskCompletionSource<TResponseParameter> taskCompletionSource)
            where TResponseParameter : class
        {
            await WaitIdleAsync(timeout);
            try
            {
                await PrepareInstanceAsync();
                TResult result = await asyncFunctionToSchedule();
                if (isScheduleSuccessful(result))
                {
                    activeTaskCompletionSources.Add(taskCompletionSource);
                    await instance.RunAsync();
                }
                else
                    idle.Set();
                return result;
            }
            catch
            {
                // TODO shouldn't we only set idle, if RunAsync() wasn't successful and instance.WorkflowInstanceState != WorkflowInstanceState.Runnable ???
                idle.Set();
                throw;
            }
        }

        #endregion

        #region IWorkflowInstanceCallback members

        Guid IWorkflowInstanceCallback.PrimaryKey => grain.PrimaryKey;

        IParameters IWorkflowInstanceCallback.Parameters => Parameters;

        Task IWorkflowInstanceCallback.SaveAsync(IDictionary<XName, InstanceValue> instanceValues)
        {
            IWorkflowState workflowState = grain.WorkflowState;
            workflowState.WorkflowDefinitionIdentity = workflowDefinitionIdentity;
            workflowState.InstanceValues = instanceValues;
            return grain.SaveWorkflowStateAsync();
        }

        void IWorkflowInstanceCallback.OnNotifyIdle()
        {
            if (!activeTaskCompletionSources.TrySetCompleted())
                idle.Set();
        }

        Task IWorkflowInstanceCallback.OnUnhandledExceptionAsync(Exception exception, Activity source)
        {
            if (activeTaskCompletionSources.TrySetException(exception))
                return TaskConstants.Completed;
            return grain.OnUnhandledExceptionAsync(exception, source);
        }

        Task IWorkflowInstanceCallback.OnCompletedAsync(ActivityInstanceState completionState, IDictionary<string, object> outputArguments, Exception terminationException) =>
            RaiseOnCompletedAsync(completionState, outputArguments, terminationException);

        Task<BookmarkResumptionResult> IWorkflowInstanceCallback.ResumeBookmarkAsync(Bookmark bookmark, object value, TimeSpan timeout) =>
            ScheduleAndRunInstanceAsync(timeout,
                () => instance.ScheduleBookmarkResumptionAsync(bookmark, value),
                (_result) => _result == BookmarkResumptionResult.Success);

        Task IWorkflowInstanceCallback.AbortAsync(Exception reason) => AbortAsync(reason);

        Task<Func<Task<TResponseResult>>> IWorkflowInstanceCallback.OnOperationAsync<TRequestParameter, TResponseResult>(string operationName, TRequestParameter requestParameter) =>
            grain.OnOperationAsync<TRequestParameter, TResponseResult>(operationName, requestParameter);

        Task<Func<Task>> IWorkflowInstanceCallback.OnOperationAsync<TRequestParameter>(string operationName, TRequestParameter requestParameter) =>
            grain.OnOperationAsync<TRequestParameter>(operationName, requestParameter);

        Task<Func<Task<TResponseResult>>> IWorkflowInstanceCallback.OnOperationAsync<TResponseResult>(string operationName) =>
            grain.OnOperationAsync<TResponseResult>(operationName);

        Task<Func<Task>> IWorkflowInstanceCallback.OnOperationAsync(string operationName) =>
            grain.OnOperationAsync(operationName);

        Task IWorkflowInstanceCallback.RegisterOrUpdateReminderAsync(string reminderName, TimeSpan dueTime) =>
            grain.RegisterOrUpdateReminderAsync(reminderName, dueTime, Parameters.ReactivationReminderPeriod);

        Task IWorkflowInstanceCallback.UnregisterReminderAsync(string reminderName) =>
            grain.UnregisterReminderAsync(reminderName);

        Task<IEnumerable<string>> IWorkflowInstanceCallback.GetRemindersAsync() =>
            grain.GetRemindersAsync();

        #endregion
    }
}
