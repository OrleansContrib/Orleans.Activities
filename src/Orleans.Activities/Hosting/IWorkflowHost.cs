using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using Orleans.Activities.Tracking;

namespace Orleans.Activities.Hosting
{
    /// <summary>
    /// Infrastructure related methods on the WorkflowHost side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IWorkflowHostInfrastructure
    {
        Task DeactivateAsync();

        Task ReminderAsync(string reminderName);
    }

    /// <summary>
    /// Control related methods on the WorkflowHost side of the communication between the WorkflowGrain and the WorkflowHost.
    /// <para>These methods are for special cases, under normal circumstances there is no need to use them, read their descriptions carefully!</para>
    /// </summary>
    public interface IWorkflowHostControl
    {
        /// <summary>
        /// Can be null.
        /// <para>In a typical implementation of this method a <see cref="TrackingParticipant"/> implementation can be added to the workflow to log the steps that are executed.</para>
        /// <para>Also can be used for custom activities that require some workflow extensions,
        /// but it would be better to use the standard ReceiveRequest, SendResponse, SendRequest, ReceiveResponse activities and keep the special logic in the grain code.</para>
        /// <para>Executed once on each activation,
        /// and after each case when the workflow is restarted or reloaded from the last persisted state due to an abort tipically caused by an unhandled exception.</para>
        /// </summary>
        Func<IEnumerable<object>> ExtensionsFactory { set; }

        /// <summary>
        /// Can be null.
        /// <para>If the workflow (as an activity) has input arguments, their values can be set by the dictionary returned by this method.</para>
        /// <para>Executed only once on the first activation,
        /// and after each case when the workflow is restarted due to an abort tipically caused by an unhandled exception before the first persistence.</para>
        /// </summary>
        Func<Task<IDictionary<string, object>>> OnStartAsync { set; }

        /// <summary>
        /// Can be null.
        /// <para>Called when the workflow is completed.</para>
        /// <para>Tipically executed only once, but in case of failure, there is a small chance that it is called multiple times.</para>
        /// <para>Argument: "ActivityInstanceState completionState" Is always set.</para>
        /// <para>Argument: "IDictionary&lt;string, object&gt; outputArguments" Can be null. If the workflow (as an activity) has output arguments, their values are returned here.</para>
        /// <para>Argument: "Exception terminationException" Can be null.</para>
        /// </summary>
        Func<ActivityInstanceState, IDictionary<string, object>, Exception, Task> OnCompletedAsync { set; }

        /// <summary>
        /// Use this method only, when the workflow doesn't start with accepting an incoming operation.
        /// The ReceiveRequest activity shouldn't be the absolute first activity to start the workflow and to skip this method to use, this method is for the situation,
        /// when there's no ReceiveRequest activity at all, ie. the workflow is purely computational. Though if you call this method before any operation, this will cause no problem.
        /// Also you have to call it once even in case of computational workflows, because after the workflow is started, it will create it's reactivation reminder during persistence.
        /// <para>IMPORTANT: The method will return only when the workflow goes idle. But this doesn't mean necessarily, that the workflow is also completed!</para>
        /// <para>IMPORTANT: Set grain.Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Allways) in case of computational workflows,
        /// because by default workflow won't persist on first idle after start, that idle is usually waiting for an incoming operation.</para>
        /// </summary>
        Task RunAsync();

        Task AbortAsync(Exception reason);
        Task CancelAsync();
        Task TerminateAsync(Exception reason);
    }

    /// <summary>
    /// Incoming operation related methods on the WorkflowHost side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IWorkflowHostOperations
    {
        Task<TResponseParameter> OperationAsync<TRequestResult, TResponseParameter>(string operationName, Func<Task<TRequestResult>> requestResult)
            where TRequestResult : class
            where TResponseParameter : class;
        Task OperationAsync<TRequestResult>(string operationName, Func<Task<TRequestResult>> requestResult)
            where TRequestResult : class;
        Task<TResponseParameter> OperationAsync<TResponseParameter>(string operationName, Func<Task> requestResult)
            where TResponseParameter : class;
        Task OperationAsync(string operationName, Func<Task> requestResult);
    }

    /// <summary>
    /// The WorkflowHost side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IWorkflowHost : IWorkflowHostInfrastructure, IWorkflowHostControl, IWorkflowHostOperations
    { }
}
