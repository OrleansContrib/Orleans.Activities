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
        /// Optional, can be null.
        /// If the grain has it's own extensions, always set the value in the ctor, and optionally add a constructor parameter as a mandatory injection point for DI.
        /// If the grain has no required extensions but want to publish an optional injection point for DI, set the value through a property setter.
        /// Use the Yield, Append and Concat LINQ extension methods to specify the grain's own extensions.
        /// <para>In a typical implementation of this method a <see cref="TrackingParticipant"/> implementation can be added to the workflow to log the steps that are executed.</para>
        /// <para>Also can be used for custom activities that require some workflow extensions,
        /// but it would be better to use the standard ReceiveRequest, SendResponse, SendRequest, ReceiveResponse activities and keep the special logic in the grain code.</para>
        /// <para>Executed once on each activation,
        /// and after each case when the workflow is restarted or reloaded from the last persisted state due to an abort typically caused by an unhandled exception.</para>
        /// </summary>
        Func<IEnumerable<object>> ExtensionsFactory { set; }

        /// <summary>
        /// Optional, can be null.
        /// Always set the value in the first incoming public grain method call that creates/starts the grain.
        /// <para>If the workflow (as an activity) has input arguments, their values can be set by the dictionary returned by this method.</para>
        /// <para>Executed only once on the first activation,
        /// and after each case when the workflow is restarted due to an abort typically caused by an unhandled exception before the first persistence.</para>
        /// <para>IMPORTANT: Do not copy values from the grain's state into the input arguments, because input arguments will be persisted by the workflow also.
        /// Closure directly the necessary values from the incoming public grain method call's parameters into the delegate.</para>
        /// </summary>
        Func<Task<IDictionary<string, object>>> StartingAsync { set; }

        /// <summary>
        /// Optional, can be null.
        /// Always set the value in the ctor to a lambda or to an instance method and never set the value together with the StartingAsync property in the first incoming public grain method call that creates/starts the grain!
        /// When the workflow persists itself and a reactivation reminder reactivates it later, the value of this property has to be set already.
        /// In case of a short running computational workflow, see <see cref="WorkflowHost.RunToCompletionAsync"/> method.
        /// <para>Called when the workflow is completed.
        /// The typical implementation in case of a long running computational workflow can call a callback method on the client that started the grain.</para>
        /// <para>Typically executed only once, but in case of failure, there is a small chance that it is called multiple times.</para>
        /// <para>Argument: "ActivityInstanceState completionState" Is always set.</para>
        /// <para>Argument: "IDictionary&lt;string, object&gt; outputArguments" Can be null. If the workflow (as an activity) has output arguments, their values are returned here.</para>
        /// <para>Argument: "Exception terminationException" Can be null.</para>
        /// </summary>
        Func<ActivityInstanceState, IDictionary<string, object>, Exception, Task> CompletedAsync { set; }

        /// <summary>
        /// Use this method only, when the workflow doesn't start with accepting an incoming operation.
        /// Certainly call it only after StartingAsync property is set.
        /// The ReceiveRequest activity shouldn't be the absolute first activity to start the workflow and to skip this method to use, this method is for the situation,
        /// when there's no ReceiveRequest activity at all, ie. the workflow is purely computational. Though if you call this method before any operation, this will cause no problem.
        /// Also you have to call it once even in case of computational workflows, because after the workflow is started, it will create it's reactivation reminder during persistence.
        /// <para>IMPORTANT: The method will return only when the workflow goes idle. But this doesn't mean necessarily, that the workflow is also completed!</para>
        /// <para>IMPORTANT: Set grain.Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Always) in case of computational workflows,
        /// because by default workflow won't persist on first idle after start, that idle is usually waiting for an incoming operation.</para>
        /// </summary>
        Task RunAsync();

        /// <summary>
        /// Use this method only for short running workflows, when the workflow doesn't start with accepting an incoming operation and completes without calling back to the caller.
        /// Certainly call it only after StartingAsync property is set.
        /// It combines <see cref="WorkflowHost.CompletedAsync"/> and <see cref="WorkflowHost.RunAsync"/> in a special way, that RunToCompletionAsync returns only when the workflow is completed or aborted.
        /// The ReceiveRequest activity shouldn't be the absolute first activity to start the workflow and to skip this method to use, this method is for the situation,
        /// when there's no ReceiveRequest activity at all, ie. the workflow is purely computational.
        /// Also you have to call it once even in case of computational workflows, because after the workflow is started, it will create it's reactivation reminder during persistence.
        /// <para>IMPORTANT: Set grain.Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Always) in case of computational workflows,
        /// because by default workflow won't persist on first idle after start, that idle is usually waiting for an incoming operation.</para>
        /// </summary>
        /// <returns></returns>
        Task<IDictionary<string, object>> RunToCompletionAsync();

        Task AbortAsync(Exception reason);
        Task CancelAsync();
        Task TerminateAsync(Exception reason);
    }

    /// <summary>
    /// Incoming operation related methods on the WorkflowHost side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IWorkflowHostOperations
    {
        Task<TResponseParameter> OperationAsync<TRequestResult, TResponseParameter>(string operationName, Func<Task<TRequestResult>> requestResult);
        Task OperationAsync<TRequestResult>(string operationName, Func<Task<TRequestResult>> requestResult);
        Task<TResponseParameter> OperationAsync<TResponseParameter>(string operationName, Func<Task> requestResult);
        Task OperationAsync(string operationName, Func<Task> requestResult);
    }

    /// <summary>
    /// The WorkflowHost side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IWorkflowHost : IWorkflowHostInfrastructure, IWorkflowHostControl, IWorkflowHostOperations
    { }
}
