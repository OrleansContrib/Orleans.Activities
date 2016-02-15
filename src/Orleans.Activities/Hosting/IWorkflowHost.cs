using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Hosting;
using Orleans.Activities.Configuration;

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
    /// </summary>
    public interface IWorkflowHostControl
    {
        /// <summary>
        /// Use this method only, when the workflow doesn't start with accepting an incoming request, but executes other activities.
        /// <para>IMPORTANT: The method will return only when the workflow goes idle.</para>
        /// <para>IMPORTANT: Set grain.Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Allways),
        /// because by default it won't persist on first idle after start, that idle is usually waiting for an incoming operation.</para>
        /// </summary>
        Task StartAsync();

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
