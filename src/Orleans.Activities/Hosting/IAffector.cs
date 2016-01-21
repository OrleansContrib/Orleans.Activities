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
    public interface IAffectorInfrastructure
    {
        Task ActivateAsync();
        Task DeactivateAsync();

        Task ReminderAsync(string reminderName);
    }

    /// <summary>
    /// Control related methods on the WorkflowHost side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IAffectorControl
    {
        WorkflowInstanceState WorkflowInstanceState { get; }
        ActivityInstanceState GetCompletionState(out IDictionary<string, object> outputArguments, out Exception terminationException);

        Task AbortAsync(Exception reason);
        Task CancelAsync();
        Task TerminateAsync(Exception reason);
    }

    /// <summary>
    /// Incoming operation related methods on the WorkflowHost side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IAffectorOperations
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
    public interface IAffector : IAffectorInfrastructure, IAffectorControl, IAffectorOperations
    { }
}
