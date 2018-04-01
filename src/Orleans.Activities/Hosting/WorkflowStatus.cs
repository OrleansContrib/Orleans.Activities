using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities.Hosting
{
    public static class WorkflowStatus
    {
        public const string Idle = "Idle";
        public const string Executing = "Executing";
        public const string Closed = "Closed";
        public const string Canceled = "Canceled";
        public const string Faulted = "Faulted";

        public static bool IsCompleted(string status)
            => status != WorkflowStatus.Idle && status != WorkflowStatus.Executing;
    }
}
