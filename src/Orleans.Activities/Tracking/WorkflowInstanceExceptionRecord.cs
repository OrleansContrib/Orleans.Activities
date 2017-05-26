using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Tracking;
using System.Runtime.Serialization;
using System.Diagnostics;

namespace Orleans.Activities.Tracking
{

    /// <summary>
    /// Tracking record for unhandled exceptions.
    /// <para>This happens when exception is thrown in a place where we can't send it back to the initiating operation, ie. when it happens in the "background",
    /// eg. exception in an OnUnhandledExceptionAsync() in the host, instance will abort, but we try to track the exception at least.</para>
    /// </summary>
    [DataContract]
    public sealed class WorkflowInstanceExceptionRecord : WorkflowInstanceRecord
    {
        [DataMember(Name = nameof(Exception))]
        public Exception Exception { get; }

        public WorkflowInstanceExceptionRecord(Guid instanceId, string activityDefinitionId, Exception exception)
            : this(instanceId, 0, activityDefinitionId, exception)
        { }

        public WorkflowInstanceExceptionRecord(Guid instanceId, long recordNumber, string activityDefinitionId, Exception exception)
            : base(instanceId, recordNumber, activityDefinitionId, System.Activities.Tracking.WorkflowInstanceStates.UnhandledException)
        {
            if (string.IsNullOrEmpty(activityDefinitionId))
                throw new ArgumentNullException(nameof(activityDefinitionId));
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Level = TraceLevel.Error;
        }

        public WorkflowInstanceExceptionRecord(Guid instanceId, string activityDefinitionId, Exception exception, WorkflowIdentity workflowDefinitionIdentity)
            : this(instanceId, activityDefinitionId, exception)
        {
            WorkflowDefinitionIdentity = workflowDefinitionIdentity;
        }

        public WorkflowInstanceExceptionRecord(Guid instanceId, long recordNumber, string activityDefinitionId, Exception exception, WorkflowIdentity workflowDefinitionIdentity)
            : this(instanceId, recordNumber, activityDefinitionId, exception)
        {
            WorkflowDefinitionIdentity = workflowDefinitionIdentity;
        }

        private WorkflowInstanceExceptionRecord(WorkflowInstanceExceptionRecord record)
            : base(record)
        {
            Exception = record.Exception;
        }

        protected override TrackingRecord Clone() => new WorkflowInstanceExceptionRecord(this);

        public override string ToString() =>
            // For backward compatibility, the ToString() does not return WorkflowIdentity, if it is null.
            WorkflowDefinitionIdentity == null
                ? $"WorkflowInstanceExceptionRecord {{ InstanceId = {InstanceId}, RecordNumber = {RecordNumber}, EventTime = {EventTime}, ActivityDefinitionId = {ActivityDefinitionId}, Exception = {Exception} }} "
                : $"WorkflowInstanceExceptionRecord {{ InstanceId = {InstanceId}, RecordNumber = {RecordNumber}, EventTime = {EventTime}, ActivityDefinitionId = {ActivityDefinitionId}, Exception = {Exception}, WorkflowDefinitionIdentity = {WorkflowDefinitionIdentity} }} ";
    }
}
