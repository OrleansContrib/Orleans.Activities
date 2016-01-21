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
    public interface IEffectorInfrastructure
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
    public interface IEffectorControl
    {
        IParameters Parameters { get; }

        IEnumerable<object> CreateExtensions();
        Task<IDictionary<string, object>> OnStartAsync();
        // source can be null
        Task OnUnhandledExceptionAsync(Exception exception, Activity source);
        // outputArguments and terminationException can be null
        Task OnCompletedAsync(ActivityInstanceState completionState, IDictionary<string, object> outputArguments, Exception terminationException);
    }

    /// <summary>
    /// Outgoing operation related methods on the WorkflowGrain side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IEffectorOperations
    {
        Task<Func<Task<TResponseResult>>> OnOperationAsync<TRequestParameter, TResponseResult>(string operationName, TRequestParameter requestParameter)
            where TRequestParameter : class
            where TResponseResult : class;
        Task<Func<Task>> OnOperationAsync<TRequestParameter>(string operationName, TRequestParameter requestParameter)
            where TRequestParameter : class;
        Task<Func<Task<TResponseResult>>> OnOperationAsync<TResponseResult>(string operationName)
            where TResponseResult : class;
        Task<Func<Task>> OnOperationAsync(string operationName);
    }

    /// <summary>
    /// The WorkflowGrain side of the communication between the WorkflowGrain and the WorkflowHost.
    /// </summary>
    public interface IEffector : IEffectorInfrastructure, IEffectorControl, IEffectorOperations
    { }
}
