using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;

namespace Orleans.Activities.Configuration
{
    public interface IParameters
    {
        /// <summary>
        /// If not set in the Timeout activity, this value is added to the delay time expressed by Expire argument to handle the time inaccuracy in different systems.
        /// </summary>
        TimeSpan DefaultTimeoutDelay { get; }

        /// <summary>
        /// If not set in the RetryDelay activity, the first retry delay starts with this value.
        /// <para>Must be at least 1 min (lower limit of reminders).</para>
        /// </summary>
        TimeSpan DefaultRetryDelayStartValue { get; }
        /// <summary>
        /// If not set in the RetryDelay activity, the retry delay time multiplicated with this value on each iteration.
        /// </summary>
        Single DefaultRetryDelayDelayMultiplicator { get; }
        /// <summary>
        /// If not set in the RetryDelay activity, the maximum of the retry delay after several iterations is this value.
        /// <para>Must be at least 1 min (lower limit of reminders).</para>
        /// </summary>
        TimeSpan DefaultRetryDelayMaxValue { get; }

        /// <summary>
        /// The timeout to start processing an incoming operation, in case the workflow is executing some other activity.
        /// <para>Must be less than 30 seconds.</para>
        /// </summary>
        TimeSpan ResumeOperationTimeout { get; }
        /// <summary>
        /// The timeout to start processing an incoming non-operation (reminder, abort, cancel or terminate) request, in case the workflow is executing some other activity.
        /// <para>Must be less than 30 seconds.</para>
        /// </summary>
        TimeSpan ResumeInfrastructureTimeout { get; }
        /// <summary>
        /// The timeout after which to abort the flush operation of the pending tracking records.
        /// </summary>
        TimeSpan TrackingTimeout { get; }
        /// <summary>
        /// If the workflow is persisted in a running state without any active timeout/delay activity, a reactivation reminder is created to activate the workflow in case of any crash.
        /// A crash can happen when eg. an unhandled exception occurs in the workflow and by default it aborts it's state.
        /// <para>Must be at least 1 min (lower limit of reminders).</para>
        /// </summary>
        TimeSpan ReactivationReminderPeriod { get; }

        /// <summary>
        /// When persist automatically the workflow's state. See <see cref="Orleans.Activities.Configuration.IdlePersistenceMode"/>
        /// </summary>
        IdlePersistenceMode IdlePersistenceMode { get; }
        /// <summary>
        /// Some of the executing workflow's internal state variables can be saved as standalone instance values that can be used by a persistence provider to store them separately.
        /// See <see cref="Orleans.Activities.Persistence.WorkflowNamespace"/> for these values.
        /// </summary>
        bool PersistWriteOnlyValues { get; }
        /// <summary>
        /// The timeout after which to abort an extension's persistence operation (OnLoad, OnSave, etc.).
        /// </summary>
        TimeSpan ExtensionsPersistenceTimeout { get; }

        /// <summary>
        /// What should do the workflow in case of an unhandled exception.
        /// Unhandled exceptions can happen when the workflow executes activities after the respone has been sent to the calling grain's request and there is nowhere to propagate them.
        /// </summary>
        UnhandledExceptionAction UnhandledExceptionAction { get; }
    }
}
