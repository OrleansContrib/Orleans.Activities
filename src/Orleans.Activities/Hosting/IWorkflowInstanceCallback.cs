using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Runtime.DurableInstancing;
using System.Xml.Linq;
using Orleans.Activities.Configuration;

namespace Orleans.Activities.Hosting
{
    /// <summary>
    /// The WorkflowHost side of the communication between the WorkflowHost and the WorkflowInstance.
    /// </summary>
    public interface IWorkflowInstanceCallback
    {
        Guid PrimaryKey { get; }
        IParameters Parameters { get; }

        Task SaveAsync(IDictionary<XName, InstanceValue> instanceValues);

        void OnNotifyIdle();
        // source can be null
        Task OnUnhandledExceptionAsync(Exception exception, Activity source);
        // outputArguments and terminationException can be null
        Task OnCompletedAsync(ActivityInstanceState completionState, IDictionary<string, object> outputArguments, Exception terminationException);

        Task<BookmarkResumptionResult> ResumeBookmarkAsync(Bookmark bookmark, object value, TimeSpan timeout);
        Task AbortAsync(Exception reason);

        Task<Func<Task<TResponseResult>>> OnOperationAsync<TRequestParameter, TResponseResult>(string operationName, TRequestParameter requestParameter)
            where TRequestParameter : class
            where TResponseResult : class;
        Task<Func<Task>> OnOperationAsync<TRequestParameter>(string operationName, TRequestParameter requestParameter)
            where TRequestParameter : class;
        Task<Func<Task<TResponseResult>>> OnOperationAsync<TResponseResult>(string operationName)
            where TResponseResult : class;
        Task<Func<Task>> OnOperationAsync(string operationName);

        Task RegisterOrUpdateReminderAsync(string reminderName, TimeSpan dueTime);
        Task UnregisterReminderAsync(string reminderName);
        Task<IEnumerable<string>> GetRemindersAsync();
    }
}
