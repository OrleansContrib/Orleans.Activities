using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using Orleans.Runtime;
using Orleans.Activities.AsyncEx;
using Orleans.Activities.Configuration;
using Orleans.Activities.Helpers;
using Orleans.Activities.Hosting;
using Orleans.Activities.Tracking;

namespace Orleans.Activities
{
    // Main concept
    // - from outside a WorkflowGrain is indistinguishable from a normal Grain; WorkflowGrain is the only class that has dependency on Orleans runtime!
    // - each WorkflowGrain has it's own WorkflowHost
    // - each WorkflowHost hosts a WorkflowInstance (and recreates it when it aborts)
    // - if you want to access another workflow, access the WorkflowGrain that hosts it

    /// <summary>
    /// Base class for workflow backed grains.
    /// <para>IMPORTANT: See TWorkflowInterface and TWorkflowCallbackInterface type parameters' description! These types must be interfaces and must have methods with required signatures!</para>
    /// <para>IMPORTANT: The WorkflowGrain implementation must explicitly implement the TWorkflowCallbackInterface interface!</para>
    /// </summary>
    /// <typeparam name="TGrainState"></typeparam>
    /// <typeparam name="TWorkflowInterface">Defines the interface that contains the operations that the backing workflow can accept, ie. the incoming requests.
    /// These operations shouldn't be the same as the grain's public grain interface, it can contain more or less operations.
    /// And the signature of these operations are never the same as the grain's public grain interface's methods, they can have 1 parameter and can have 1 return value
    /// (ie. booth are optional, but wrapped in Func&lt;Task&gt; and Task).
    /// Valid signatures are (the requestResult delegate is executed only, when the workflow can handle the incoming request):
    /// <para>Task&lt;...&gt; OperationNameAsync(Func&lt;Task&lt;...&gt;&gt; requestResult)</para>
    /// <para>Task OperationNameAsync(Func&lt;Task&lt;...&gt;&gt; requestResult)</para>
    /// <para>Task&lt;...&gt; OperationNameAsync(Func&lt;Task&gt; requestResult)</para>
    /// <para>Task OperationNameAsync(Func&lt;Task&gt; requestResult)</para>
    /// </typeparam>
    /// <typeparam name="TWorkflowCallbackInterface">Defines the interface that contains the operations that the backing workflow can call, ie. the outgoing requests.
    /// These operations shouldn't be the same as the public grain interfaces this grain will call on other grains, it can contain more or less operations.
    /// And the signature of these operations are never the same as the public grain interfaces' methods this grain will call on other grains, they can have 1 parameter and can have 1 return value
    /// (ie. booth are optional, but return value wrapped in Task&lt;Func&lt;Task&gt;&gt;).
    /// Valid signatures are (the delegate in the return value is executed only, when the workflow can handle the result of the outgoing request):
    /// <para>Task&lt;Func&lt;Task&lt;...&gt;&gt;&gt; OnOperationNameAsync(... requestParameter)</para>
    /// <para>Task&lt;Func&lt;Task&gt;&gt; OnOperationNameAsync(... requestParameter)</para>
    /// <para>Task&lt;Func&lt;Task&lt;...&gt;&gt;&gt; OnOperationNameAsync()</para>
    /// <para>Task&lt;Func&lt;Task&gt;&gt; OnOperationNameAsync()</para>
    /// </typeparam>
    public abstract class WorkflowGrain<TGrainState, TWorkflowInterface, TWorkflowCallbackInterface> : Grain<TGrainState>, IRemindable
        where TGrainState : GrainState, IWorkflowState
        where TWorkflowInterface : class
        where TWorkflowCallbackInterface : class
    {
        #region private fields

        private IWorkflowHost workflowHost;

        private TWorkflowInterface workflowInterfaceProxy;

        #endregion

        #region ctor and properties for DI

        protected WorkflowGrain(Func<WorkflowIdentity, Activity> workflowDefinitionFactory, WorkflowIdentity workflowDefinitionIdentity)
        {
            // TODO
            // it's not possible to force at compile time that the WorkflowGrain implementation should implement TWorkflowCallbackInterface
            // it would be great to check all the WorkflowGrain implementations at assembly load time (Orleans bootstrap? DI? module initilizer .cctor?)
            // WorkflowInterfaceProxy<> and WorkflowCallbackInterfaceProxy<> cctor also executes checks on TWorkflowInterface and TWorkflowCallbackInterface method signatures, but called only at first usage and not load time
            // Fody/ModuleInit: https://github.com/fody/moduleinit
            // Module Initializer: http://einaregilsson.com/module-initializers-in-csharp/

            if (!(this is TWorkflowCallbackInterface))
                throw new InvalidProgramException($"Type '{GetType().GetFriendlyName()}' must explicitly implement interface '{typeof(TWorkflowCallbackInterface).GetFriendlyName()}'!");

            workflowHost = new WorkflowHost(new WorkflowHostCallback(this), workflowDefinitionFactory, workflowDefinitionIdentity);
            workflowInterfaceProxy = WorkflowInterfaceProxy<TWorkflowInterface>.CreateProxy(workflowHost);
        }

        private IParameters parameters;

        public IParameters Parameters
        {
            get
            {
                if (parameters == null)
                    parameters = new Parameters();
                return parameters;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (parameters != null)
                    throw new InvalidOperationException(nameof(Parameters) + " property is already set!");
                parameters = value;
            }
        }

        #endregion

        #region Grain & IRemindable infrastructure members

        public override Task OnDeactivateAsync() => workflowHost.DeactivateAsync();

        public virtual Task ReceiveReminder(string reminderName, TickStatus tickStatus) => workflowHost.ReminderAsync(reminderName);

        #endregion

        #region IWorkflowHostCallback members (wrapper to hide IWorkflowHostCallback interface implementation)

        private class WorkflowHostCallback : IWorkflowHostCallback
        {
            private WorkflowGrain<TGrainState, TWorkflowInterface, TWorkflowCallbackInterface> grain;

            private IWorkflowHostCallbackOperations workflowCallbackInterfaceProxy;

            public WorkflowHostCallback(WorkflowGrain<TGrainState, TWorkflowInterface, TWorkflowCallbackInterface> grain)
            {
                this.grain = grain;
                this.workflowCallbackInterfaceProxy = WorkflowCallbackInterfaceProxy<TWorkflowCallbackInterface>.CreateProxy(grain as TWorkflowCallbackInterface);
            }

            public Guid PrimaryKey => grain.GetPrimaryKey();

            public IWorkflowState WorkflowState => grain.State;

            public Task LoadWorkflowStateAsync() => grain.ReadStateAsync();

            public Task SaveWorkflowStateAsync() => grain.WriteStateAsync();

            private static TimeSpan oneMinuteTimeSpan = TimeSpan.FromMinutes(1);

            public Task RegisterOrUpdateReminderAsync(string reminderName, TimeSpan dueTime, TimeSpan period) =>
                grain.RegisterOrUpdateReminder(reminderName,
                    dueTime < oneMinuteTimeSpan ? oneMinuteTimeSpan : dueTime,
                    period < oneMinuteTimeSpan ? oneMinuteTimeSpan : period);

            public async Task UnregisterReminderAsync(string reminderName)
            {
                try
                {
                    IGrainReminder grainReminder = await grain.GetReminder(reminderName);
                    if (grainReminder != null)
                        await grain.UnregisterReminder(grainReminder);
                }
                catch (AggregateException ae)
                {
                    ae.Handle((e) => e is KeyNotFoundException);
                }
            }

            public async Task<IEnumerable<string>> GetRemindersAsync() => from grainReminder in await grain.GetReminders() select grainReminder.ReminderName;

            public IParameters Parameters => grain.Parameters;

            public IEnumerable<object> CreateExtensions() => grain.CreateExtensions();

            public Task<IDictionary<string, object>> OnStartAsync() =>
                grain.OnStartAsync();

            public Task OnUnhandledExceptionAsync(Exception exception, Activity source) =>
                grain.OnUnhandledExceptionAsync(exception, source);

            public Task OnCompletedAsync(ActivityInstanceState completionState, IDictionary<string, object> outputs, Exception terminationException)  =>
                grain.OnCompletedAsync(completionState, outputs, terminationException);

            public Task<Func<Task<TResponseResult>>> OnOperationAsync<TRequestParameter, TResponseResult>(string operationName, TRequestParameter requestParameter)
                    where TRequestParameter : class
                    where TResponseResult : class =>
                workflowCallbackInterfaceProxy.OnOperationAsync<TRequestParameter, TResponseResult>(operationName, requestParameter);

            public Task<Func<Task>> OnOperationAsync<TRequestParameter>(string operationName, TRequestParameter requestParameter)
                    where TRequestParameter : class  =>
                workflowCallbackInterfaceProxy.OnOperationAsync<TRequestParameter>(operationName, requestParameter);

            public Task<Func<Task<TResponseResult>>> OnOperationAsync<TResponseResult>(string operationName)
                    where TResponseResult : class =>
                workflowCallbackInterfaceProxy.OnOperationAsync<TResponseResult>(operationName);

            public Task<Func<Task>> OnOperationAsync(string operationName) =>
                workflowCallbackInterfaceProxy.OnOperationAsync(operationName);
        }

        #endregion

        /// <summary>
        /// The control functions of the workflow is accessible through this property.
        /// <para>Under normal circumstances there is no need to access these functions.</para>
        /// </summary>
        protected IWorkflowHostControl WorkflowControl => workflowHost;

        /// <summary>
        /// The TWorkflowInterface operations of the workflow are accessible through this property.
        /// </summary>
        protected TWorkflowInterface WorkflowInterface => workflowInterfaceProxy;

        /// <summary>
        /// In a typical implementation of this method a <see cref="TrackingParticipant"/> implementation can be added to the workflow to log the steps that are executed.
        /// <para>Also can be used for custom activities that require some workflow extensions,
        /// but it would be better to use the standard ReceiveRequest, SendResponse, SendRequest, ReceiveResponse activities and keep the special logic in the grain code.</para>
        /// <para>Executed once on each activation,
        /// and after each case when the workflow is restarted or reloaded from the last persisted state due to an abort tipically caused by an unhandled exception.</para>
        /// </summary>
        /// <returns></returns>
        protected virtual IEnumerable<object> CreateExtensions() => Enumerable.Empty<object>();

        /// <summary>
        /// If the workflow (as an activity) has input arguments, their values can be set by the dictionary returned by this method.
        /// <para>Executed only once on the first activation,
        /// and after each case when the workflow is restarted due to an abort tipically caused by an unhandled exception before the first persistence.</para>
        /// </summary>
        /// <returns></returns>
        protected virtual Task<IDictionary<string, object>> OnStartAsync() => TaskConstants<IDictionary<string, object>>.Default;

        /// <summary>
        /// Exceptions are propagated back to the caller before a grain incoming request's response has been sent back, but after the workflow runs on the tail of the request,
        /// the grain has to handle (and log, if a tracking-participant-extension is not used) the exception.
        /// <para>By default the workflow is aborted on an unhandled exception and will be reloaded from the last persisted state (or restarted, if persistence hasn't happened before)
        /// on the next incoming request or by the reactivation reminder. See <see cref="IParameters.UnhandledExceptionAction"/> on <see cref="Parameters"/> property.</para>
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="source">Can be null.</param>
        /// <returns></returns>
        protected abstract Task OnUnhandledExceptionAsync(Exception exception, Activity source);

        /// <summary>
        /// Called when the workflow is completed.
        /// <para>Tipically executed only once, but in case of failure, there is a small chance that it is called multiple times.</para>
        /// </summary>
        /// <param name="completionState"></param>
        /// <param name="outputArguments">Can be null. If the workflow (as an activity) has output arguments, their values are returned here.</param>
        /// <param name="terminationException">Can be null.</param>
        /// <returns></returns>
        protected virtual Task OnCompletedAsync(ActivityInstanceState completionState, IDictionary<string, object> outputArguments, Exception terminationException) => TaskConstants.Completed;
    }
}
