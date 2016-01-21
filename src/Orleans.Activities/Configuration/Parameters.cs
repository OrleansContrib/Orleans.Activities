using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;

namespace Orleans.Activities.Configuration
{
    /// <summary>
    /// See <see cref="Orleans.Activities.Configuration.IParameters"/> for parameter descriptions.
    /// </summary>
    public class Parameters : IParameters
    {
        public TimeSpan DefaultTimeoutDelay { get; }

        public TimeSpan DefaultRetryDelayStartValue { get; }
        public Single DefaultRetryDelayDelayMultiplicator { get; }
        public TimeSpan DefaultRetryDelayMaxValue { get; }

        public TimeSpan ResumeOperationTimeout { get; }
        public TimeSpan ResumeInfrastructureTimeout { get; }
        public TimeSpan TrackingTimeout { get; }
        public TimeSpan ReactivationReminderPeriod { get; }

        public IdlePersistenceMode IdlePersistenceMode { get; }
        public bool PersistWriteOnlyValues { get; }
        public TimeSpan ExtensionsPersistenceTimeout { get; }

        public UnhandledExceptionAction UnhandledExceptionAction { get; }

        /// <summary>
        /// See <see cref="Orleans.Activities.Configuration.IParameters"/> for parameter descriptions.
        /// </summary>
        public Parameters(
            // Yes, these are intentionally nullable, this way the default values are centralised and not scattered (and fixed) through the code during compile time.
            TimeSpan? defaultTimeoutDelay = null,

            TimeSpan? defaultRetryDelayStartValue = null,
            Single? defaultRetryDelayDelayMultiplicator = null,
            TimeSpan? defaultRetryDelayMaxValue = null,

            TimeSpan? resumeOperationTimeout = null,
            TimeSpan? resumeInfrastructureTimeout = null,
            TimeSpan? trackingTimeout = null,
            TimeSpan? reactivationReminderPeriod = null,

            IdlePersistenceMode? idlePersistenceMode = null,
            bool? persistWriteOnlyValues = null,
            TimeSpan? extensionsPersistenceTimeout = null,

            UnhandledExceptionAction? unhandledExceptionAction = null)
        {
            DefaultTimeoutDelay = defaultTimeoutDelay ?? TimeSpan.FromMinutes(5);

            DefaultRetryDelayStartValue = defaultRetryDelayStartValue ?? TimeSpan.FromMinutes(1);
            DefaultRetryDelayDelayMultiplicator = defaultRetryDelayDelayMultiplicator ?? 2.0f;
            DefaultRetryDelayMaxValue = defaultRetryDelayMaxValue ?? TimeSpan.FromMinutes(60);

            ResumeOperationTimeout = resumeOperationTimeout ?? TimeSpan.FromSeconds(30);
            ResumeInfrastructureTimeout = resumeInfrastructureTimeout ?? TimeSpan.FromSeconds(30);
            TrackingTimeout = trackingTimeout ?? TimeSpan.FromSeconds(30);
            ReactivationReminderPeriod = reactivationReminderPeriod ?? TimeSpan.FromMinutes(2);

            IdlePersistenceMode = idlePersistenceMode ?? IdlePersistenceMode.OnPersistableIdle | IdlePersistenceMode.OnCompleted;
            PersistWriteOnlyValues = persistWriteOnlyValues ?? false;
            ExtensionsPersistenceTimeout = extensionsPersistenceTimeout ?? TimeSpan.FromSeconds(30);

            UnhandledExceptionAction = unhandledExceptionAction ?? UnhandledExceptionAction.Abort;
        }
    }
}
