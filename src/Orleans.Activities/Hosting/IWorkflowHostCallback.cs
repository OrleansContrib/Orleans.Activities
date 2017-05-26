using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using Orleans.Activities.Configuration;

namespace Orleans.Activities.Hosting
{
    /// <summary>
    /// Infrastructure related methods on the WorkflowGrain side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IWorkflowHostCallbackInfrastructure
    {
        Guid PrimaryKey { get; }

        IWorkflowState WorkflowState { get; }
        Task LoadWorkflowStateAsync();
        Task SaveWorkflowStateAsync();

        Task RegisterOrUpdateReminderAsync(string reminderName, TimeSpan dueTime, TimeSpan period);
        Task UnregisterReminderAsync(string reminderName);
        Task<IEnumerable<string>> GetRemindersAsync();
    }

    /// <summary>
    /// Control related methods on the WorkflowGrain side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IWorkflowHostCallbackControl
    {
        IParameters Parameters { get; }

        // source can be null
        Task OnUnhandledExceptionAsync(Exception exception, Activity source);
    }

    /// <summary>
    /// Outgoing operation related methods on the WorkflowGrain side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IWorkflowHostCallbackOperations
    {
        Task<Func<Task<TResponseResult>>> OnOperationAsync<TRequestParameter, TResponseResult>(string operationName, TRequestParameter requestParameter);
        Task<Func<Task>> OnOperationAsync<TRequestParameter>(string operationName, TRequestParameter requestParameter);
        Task<Func<Task<TResponseResult>>> OnOperationAsync<TResponseResult>(string operationName);
        Task<Func<Task>> OnOperationAsync(string operationName);
    }

    /// <summary>
    /// The WorkflowGrain side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IWorkflowHostCallback : IWorkflowHostCallbackInfrastructure, IWorkflowHostCallbackControl, IWorkflowHostCallbackOperations
    { }
}
