using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Hosting;
using System.Runtime.DurableInstancing;
using System.Xml.Linq;

namespace Orleans.Activities.Hosting
{
    /// <summary>
    /// The WorkflowInstance side of the communication between the WorkflowHost and the WorkflowInstance.
    /// </summary>
    public interface IWorkflowInstance
    {
        void Start(IDictionary<string, object> inputArguments, IEnumerable<object> extensions);
        Task LoadAsync(IDictionary<XName, InstanceValue> instanceValues, IEnumerable<object> extensions);
        Task DeactivateAsync();

        WorkflowInstanceState WorkflowInstanceState { get; }
        ActivityInstanceState GetCompletionState(out IDictionary<string, object> outputArguments, out Exception terminationException);

        Task AbortAsync(Exception reason);
        Task ScheduleCancelAsync();
        Task TerminateAsync(Exception reason);
        Task<BookmarkResumptionResult> ScheduleBookmarkResumptionAsync(Bookmark bookmark, object value);
        Task<BookmarkResumptionResult> ScheduleOperationBookmarkResumptionAsync(string operationName, object value);
        Task<BookmarkResumptionResult> ScheduleReminderBookmarkResumptionAsync(string reminderName);
        Task RunAsync();
    }
}
