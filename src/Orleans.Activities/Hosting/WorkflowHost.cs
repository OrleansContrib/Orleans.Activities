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
        protected IWorkflowCompletionState completionState;

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

        public async Task DeactivateAsync()
        {
            await WaitIdleAsync(Parameters.ResumeInfrastructureTimeout);
            try
            {
                if (instance != null && instance.WorkflowInstanceState != WorkflowInstanceState.Aborted)
                    await instance.DeactivateAsync();
            }
            finally
            {
                idle.Set();
            }
        }

        public Task ReminderAsync(string reminderName) =>
            ResumeReminderBookmarkAsync(reminderName);

        #endregion

        #region IWorkflowHost/IWorkflowHostControl members

        public Func<IEnumerable<object>> ExtensionsFactory { private get; set; }

        public Func<Task<IDictionary<string, object>>> StartingAsync { private get; set; }

        public Func<ActivityInstanceState, IDictionary<string, object>, Exception, Task> CompletedAsync { private get; set; }

        public async Task RunAsync()
        {
            await WaitIdleAsync(Parameters.ResumeInfrastructureTimeout);
            try
            {
                await PrepareAsync();
            }
            finally
            {
                idle.Set();
            }
        }

        public async Task<IDictionary<string, object>> RunToCompletionAsync()
        {
            await WaitIdleAsync(Parameters.ResumeInfrastructureTimeout);
            try
            {
                if (completionState == null)
                {
                    TaskCompletionSource<object> taskCompletionSource = new TaskCompletionSource<object>();
                    CompletedAsync = (ActivityInstanceState _activityInstanceState, IDictionary<string, object> _outputArguments, Exception _terminationException) =>
                    {
                        taskCompletionSource.SetResult(null);
                        return TaskConstants.Completed;
                    };
                    try
                    {
                        // We run the instance like an operation, but without scheduling a bookmark resumption. And not a SendResponse activity sets the TCS but the Completed event.
                        await PrepareAsync();

                        if (completionState == null)
                        {
                            try
                            {
                                activeTaskCompletionSources.Add(taskCompletionSource);
                                idle.Set();
                                await taskCompletionSource.Task;
                            }
                            finally
                            {
                                activeTaskCompletionSources.Remove(taskCompletionSource);
                            }
                        }
                        else
                            idle.Set();
                    }
                    finally
                    {
                        CompletedAsync = null;
                    }
                }
                else
                    idle.Set();
            }
            catch
            {
                // TODO shouldn't we only set idle, if RunAsync() wasn't successful and instance.WorkflowInstanceState != WorkflowInstanceState.Runnable ???
                idle.Set();
                throw;
            }
            return completionState.Result;
        }

        public async Task AbortAsync(Exception reason)
        {
            await WaitIdleAsync(Parameters.ResumeInfrastructureTimeout);
            try
            {
                await PrepareAsync();
                if (completionState == null)
                {
                    if (instance.WorkflowInstanceState == WorkflowInstanceState.Complete)
                        throw CreateCompletedException();
                    await instance.AbortAsync(reason);
                }
                else
                    throw CreateCompletedException();
            }
            finally
            {
                idle.Set();
            }
        }

        public async Task CancelAsync()
        {
            await WaitIdleAsync(Parameters.ResumeInfrastructureTimeout);
            try
            {
                await PrepareAsync();
                if (completionState == null)
                {
                    if (instance.WorkflowInstanceState == WorkflowInstanceState.Complete)
                        throw CreateCompletedException();
                    await instance.ScheduleCancelAsync();
                    await instance.RunAsync();
                }
                else
                    throw CreateCompletedException();
            }
            catch
            {
                // TODO shouldn't we only set idle, if RunAsync() wasn't successful and instance.WorkflowInstanceState != WorkflowInstanceState.Runnable ???
                idle.Set();
                throw;
            }
        }

        public async Task TerminateAsync(Exception reason)
        {
            await WaitIdleAsync(Parameters.ResumeInfrastructureTimeout);
            try
            {
                await PrepareAsync();
                if (completionState == null)
                {
                    if (instance.WorkflowInstanceState == WorkflowInstanceState.Complete)
                        throw CreateCompletedException();
                    await instance.TerminateAsync(reason);
                    await instance.RunAsync();
                }
                else
                    throw CreateCompletedException();
            }
            catch
            {
                // TODO shouldn't we only set idle, if RunAsync() wasn't successful and instance.WorkflowInstanceState != WorkflowInstanceState.Runnable ???
                idle.Set();
                throw;
            }
        }

        #endregion

        #region IWorkflowHost/IWorkflowHostOperations members

        public Task<TResponseParameter> OperationAsync<TRequestResult, TResponseParameter>(string operationName, Func<Task<TRequestResult>> requestResult) =>
            ResumeOperationBookmarkAsync<TResponseParameter>(operationName, requestResult, typeof(TResponseParameter));

        public Task OperationAsync<TRequestResult>(string operationName, Func<Task<TRequestResult>> requestResult) =>
            ResumeOperationBookmarkAsync<object>(operationName, requestResult, typeof(void));

        public Task<TResponseParameter> OperationAsync<TResponseParameter>(string operationName, Func<Task> requestResult) =>
            ResumeOperationBookmarkAsync<TResponseParameter>(operationName, requestResult, typeof(TResponseParameter));

        public Task OperationAsync(string operationName, Func<Task> requestResult) =>
            ResumeOperationBookmarkAsync<object>(operationName, requestResult, typeof(void));

        #endregion

        #region IWorkflowHost members helper methods

        // If the resumption didn't timed out nor aborted, but not found, it tries to return the previous response parameter if the operation was idempotent,
        // ie. throws OperationRepeatedException, or throws InvalidOperationException if the previous response is not known (didn't happen or not idempotent).
        private async Task<TResponseParameter> ResumeOperationBookmarkAsync<TResponseParameter>(string operationName, object requestResult, Type responseParameterType)
        {
            await WaitIdleAsync(Parameters.ResumeOperationTimeout);
            try
            {
                await PrepareAsync();
                if (completionState == null)
                {
                    TaskCompletionSource<TResponseParameter> taskCompletionSource = new TaskCompletionSource<TResponseParameter>();
                    BookmarkResumptionResult result = await instance.ScheduleOperationBookmarkResumptionAsync(operationName, new object[] { taskCompletionSource, requestResult });
                    WorkflowInstanceState workflowInstanceState = instance.WorkflowInstanceState;
                    if (result == BookmarkResumptionResult.Success)
                    {
                        try
                        {
                            activeTaskCompletionSources.Add(taskCompletionSource);
                            await instance.RunAsync();
                            return await taskCompletionSource.Task;
                        }
                        finally
                        {
                            activeTaskCompletionSources.Remove(taskCompletionSource);
                        }
                    }
                    else
                    {
                        if (result == BookmarkResumptionResult.NotReady && workflowInstanceState != WorkflowInstanceState.Complete)
                            // Instance is created but the initialization RunAsync() hasn't been called, this is impossible.
                            throw new InvalidOperationException($"Instance state is '{workflowInstanceState}', instance is not ready to process operation '{operationName}'.");
                        else // NotFound or NotReady && Complete, though the later is also not possible at this point, completionState != null after prepare in that case
                            throw previousResponseParameterExtension.CreatePreviousResponseParameterException<TResponseParameter>(operationName, responseParameterType);
                    }
                }
                else
                    throw previousResponseParameterExtension.CreatePreviousResponseParameterException<TResponseParameter>(operationName, responseParameterType);
            }
            catch
            {
                // TODO shouldn't we only set idle, if RunAsync() wasn't successful and instance.WorkflowInstanceState != WorkflowInstanceState.Runnable ???
                idle.Set();
                throw;
            }
        }

        private async Task ResumeReminderBookmarkAsync(string reminderName)
        {
            await WaitIdleAsync(Parameters.ResumeInfrastructureTimeout);
            try
            {
                await PrepareAsync();
                if (completionState == null)
                {
                    BookmarkResumptionResult result = await instance.ScheduleReminderBookmarkResumptionAsync(reminderName);
                    WorkflowInstanceState workflowInstanceState = instance.WorkflowInstanceState;
                    if (result == BookmarkResumptionResult.Success)
                        await instance.RunAsync();
                    else
                    {
                        if (result == BookmarkResumptionResult.NotReady && workflowInstanceState != WorkflowInstanceState.Complete)
                            // Instance is created but the initialization RunAsync() hasn't been called, this is impossible.
                            throw new InvalidOperationException($"Instance state is '{workflowInstanceState}', instance is not ready to process reminder '{reminderName}'.");
                        else // NotFound or NotReady && Complete, though the later is also not possible at this point, completionState != null after prepare in that case
                            // If we don't find a reminder, it's not an issue, maybe the grain/silo crashed after the reminder was created but before the workflow state was persisted,
                            // or the grain/silo crashed after persistence but before the reminder was unregistered,
                            // it will be unregistered on the next persistence event.
                            // See ReminderTable for the detailed description of the algorithm.
                            idle.Set();
                    }
                }
                else
                {
                    await grain.UnregisterReminderAsync(reminderName);
                    idle.Set();
                }
            }
            catch
            {
                // TODO shouldn't we only set idle, if RunAsync() wasn't successful and instance.WorkflowInstanceState != WorkflowInstanceState.Runnable ???
                idle.Set();
                throw;
            }
        }

        private async Task<BookmarkResumptionResult> ResumeInstanceBookmarkAsync(Bookmark bookmark, object value, TimeSpan timeout)
        {
            await WaitIdleAsync(timeout);
            try
            {
                await PrepareAsync();

                BookmarkResumptionResult result = await instance.ScheduleBookmarkResumptionAsync(bookmark, value);
                if (result == BookmarkResumptionResult.Success)
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

        #endregion

        #region protected/private helper methods

        private IEnumerable<object> Extensions =>
            ExtensionsFactory == null
            ? previousResponseParameterExtension.Yield()
            : ExtensionsFactory().Append(previousResponseParameterExtension);

        private IEnumerable<object> HostExtensions =>
            previousResponseParameterExtension.Yield();

        private Task<IDictionary<string, object>> RaiseStartingAsync() =>
            StartingAsync == null
            ? TaskConstants<IDictionary<string, object>>.Default
            : StartingAsync();

        private Task RaiseCompletedAsync(ActivityInstanceState completionState, IDictionary<string, object> outputArguments, Exception terminationException) =>
            CompletedAsync == null
            ? TaskConstants.Completed
            : CompletedAsync(completionState, outputArguments, terminationException);

        private Exception CreateCompletedException() =>
            new InvalidOperationException("The workflow has already completed.");

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
        private async Task PrepareAsync()
        {
            if (completionState == null)
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
                    WorkflowIdentity workflowDefinitionIdentity = null;
                    IWorkflowInstance instance = null;
                    WorkflowCompletionState completionState = null;
                    if (workflowState.InstanceValues == null)
                    {
                        // Start, there is no previous persisted state.
                        workflowDefinitionIdentity = workflowDefinitionIdentityFactory();
                        instance = new WorkflowInstance(this, workflowDefinitionFactory(workflowDefinitionIdentity), workflowDefinitionIdentity);
                        instance.Start(await RaiseStartingAsync(), Extensions);
                    }
                    else if (!WorkflowInstance.IsCompleted(workflowState.InstanceValues))
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
                    else
                    {
                        completionState = new WorkflowCompletionState();
                        await completionState.LoadAsync(workflowState.InstanceValues, HostExtensions, Parameters);
                    }
                    // Set the the values only, when the instance/completionState was successfully initialized.
                    this.workflowDefinitionIdentity = workflowDefinitionIdentity;
                    this.instance = instance;
                    this.completionState = completionState;
                }
                if (instance != null && instance.WorkflowInstanceState == WorkflowInstanceState.Runnable)
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
        }

        #endregion

        #region IWorkflowInstanceCallback members

        public Guid PrimaryKey => grain.PrimaryKey;

        public IParameters Parameters => grain.Parameters;

        public Task SaveAsync(IDictionary<XName, InstanceValue> instanceValues)
        {
            IWorkflowState workflowState = grain.WorkflowState;
            workflowState.WorkflowDefinitionIdentity = workflowDefinitionIdentity;
            workflowState.InstanceValues = instanceValues;
            return grain.SaveWorkflowStateAsync();
        }

        public void OnNotifyIdle()
        {
            bool isTrySetCompleted = activeTaskCompletionSources.TrySetCompleted();
            if (completionState != null)
            {
                if (instance.WorkflowInstanceState == WorkflowInstanceState.Complete)
                    // Completed as expected.
                    instance = null;
                else
                    // Typically aborted after completion due to a persistence failure.
                    completionState = null;
            }
            if (!isTrySetCompleted)
                idle.Set();
        }

        public Task OnUnhandledExceptionAsync(Exception exception, Activity source)
        {
            if (activeTaskCompletionSources.TrySetException(exception))
                return TaskConstants.Completed;
            return grain.OnUnhandledExceptionAsync(exception, source);
        }

        public Task OnCompletedAsync(ActivityInstanceState completionState, IDictionary<string, object> outputArguments, Exception terminationException)
        {
            this.completionState = new WorkflowCompletionState(completionState, outputArguments, terminationException);
            return RaiseCompletedAsync(completionState, outputArguments, terminationException);
        }

        public Task<BookmarkResumptionResult> ResumeBookmarkAsync(Bookmark bookmark, object value, TimeSpan timeout) =>
            ResumeInstanceBookmarkAsync(bookmark, value, timeout);

        // It is common with IWorkflowHostControl.
        //public Task AbortAsync(Exception reason);

        public Task<Func<Task<TResponseResult>>> OnOperationAsync<TRequestParameter, TResponseResult>(string operationName, TRequestParameter requestParameter) =>
            grain.OnOperationAsync<TRequestParameter, TResponseResult>(operationName, requestParameter);

        public Task<Func<Task>> OnOperationAsync<TRequestParameter>(string operationName, TRequestParameter requestParameter) =>
            grain.OnOperationAsync<TRequestParameter>(operationName, requestParameter);

        public Task<Func<Task<TResponseResult>>> OnOperationAsync<TResponseResult>(string operationName) =>
            grain.OnOperationAsync<TResponseResult>(operationName);

        public Task<Func<Task>> OnOperationAsync(string operationName) =>
            grain.OnOperationAsync(operationName);

        public Task RegisterOrUpdateReminderAsync(string reminderName, TimeSpan dueTime) =>
            grain.RegisterOrUpdateReminderAsync(reminderName, dueTime, Parameters.ReactivationReminderPeriod);

        public Task UnregisterReminderAsync(string reminderName) =>
            grain.UnregisterReminderAsync(reminderName);

        public Task<IEnumerable<string>> GetRemindersAsync() =>
            grain.GetRemindersAsync();

        #endregion
    }
}
