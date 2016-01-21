using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities.Tracking
{
    /// <summary>
    /// Constant values for tracking records.
    /// </summary>
    public static class WorkflowInstanceStates
    {
        public const string Deactivated = "Deactivated";

        public const string ReminderRegistered = "ReminderRegistered";
        public const string ReminderUnregistered = "ReminderUnregistered";
    }
}
