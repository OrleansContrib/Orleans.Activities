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
        public TimeSpan DefaultTimeoutDelay { get; private set; }

        public TimeSpan DefaultRetryDelayStartValue { get; private set; }
        public float DefaultRetryDelayDelayMultiplicator { get; private set; }
        public TimeSpan DefaultRetryDelayMaxValue { get; private set; }

        public TimeSpan ResumeOperationTimeout { get; private set; }
        public TimeSpan ResumeInfrastructureTimeout { get; private set; }
        public TimeSpan TrackingTimeout { get; private set; }
        public TimeSpan ReactivationReminderPeriod { get; private set; }

        public IdlePersistenceMode IdlePersistenceMode { get; private set; }
        public bool PersistWriteOnlyValues { get; private set; }
        public TimeSpan ExtensionsPersistenceTimeout { get; private set; }

        public UnhandledExceptionAction UnhandledExceptionAction { get; private set; }

        /// <summary>
        /// See <see cref="Orleans.Activities.Configuration.IParameters"/> for parameter descriptions.
        /// </summary>
        public Parameters(
            // Yes, these are intentionally nullable, this way the default values are centralised and not scattered (and fixed) through the code during compile time.
            TimeSpan? defaultTimeoutDelay = null,

            TimeSpan? defaultRetryDelayStartValue = null,
            float? defaultRetryDelayDelayMultiplicator = null,
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
            this.DefaultTimeoutDelay = defaultTimeoutDelay ?? TimeSpan.FromMinutes(5);

            this.DefaultRetryDelayStartValue = defaultRetryDelayStartValue ?? TimeSpan.FromMinutes(1);
            this.DefaultRetryDelayDelayMultiplicator = defaultRetryDelayDelayMultiplicator ?? 2.0f;
            this.DefaultRetryDelayMaxValue = defaultRetryDelayMaxValue ?? TimeSpan.FromMinutes(60);

            this.ResumeOperationTimeout = resumeOperationTimeout ?? TimeSpan.FromSeconds(30);
            this.ResumeInfrastructureTimeout = resumeInfrastructureTimeout ?? TimeSpan.FromSeconds(30);
            this.TrackingTimeout = trackingTimeout ?? TimeSpan.FromSeconds(30);
            this.ReactivationReminderPeriod = reactivationReminderPeriod ?? TimeSpan.FromMinutes(2);

            this.IdlePersistenceMode = idlePersistenceMode ?? IdlePersistenceMode.OnPersistableIdle | IdlePersistenceMode.OnCompleted;
            this.PersistWriteOnlyValues = persistWriteOnlyValues ?? false;
            this.ExtensionsPersistenceTimeout = extensionsPersistenceTimeout ?? TimeSpan.FromSeconds(30);

            this.UnhandledExceptionAction = unhandledExceptionAction ?? UnhandledExceptionAction.Abort;
        }
    }
}
