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
    /// Published functionality to the activities by the workflow instance, through the ActivityContextExtension.
    /// </summary>
    public interface IActivityContext
    {
        IParameters Parameters { get; }
        WorkflowInstanceState WorkflowInstanceState { get; }
        bool IsStarting { get; }
        bool IsReloaded { get; }
        bool TrackingEnabled { get; }

        Task<BookmarkResumptionResult> ResumeBookmarkThroughHostAsync(Bookmark bookmark, object value, TimeSpan timeout);
        Task AbortThroughHostAsync(Exception reason);
        Task<bool> NotifyHostOnUnhandledExceptionAsync(Exception exception, Activity source);

        Task<Func<Task<TResponseResult>>> OnOperationAsync<TRequestParameter, TResponseResult>(string operationName, TRequestParameter requestParameter);
        Task<Func<Task>> OnOperationAsync<TRequestParameter>(string operationName, TRequestParameter requestParameter);
        Task<Func<Task<TResponseResult>>> OnOperationAsync<TResponseResult>(string operationName);
        Task<Func<Task>> OnOperationAsync(string operationName);

        Task RegisterOrUpdateReminderAsync(string reminderName, TimeSpan dueTime);
        Task UnregisterReminderAsync(string reminderName);
        Task<IEnumerable<string>> GetRemindersAsync();
    }
}
