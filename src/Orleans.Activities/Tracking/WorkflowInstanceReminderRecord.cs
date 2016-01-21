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
    /// Tracking record for Reminder registration/unregistration.
    /// <para>This is the only record type we can track during reminder registration/unregistration, because we create it in the instance, that's why this is a WorkflowInstanceRecord.
    /// The event is similar to persistence (though reminders are registered/unregistered independently), that's why we should treat this as state change in the grain.</para>
    /// </summary>
    [DataContract]
    public sealed class WorkflowInstanceReminderRecord : WorkflowInstanceRecord
    {
        [DataMember(Name = nameof(ReminderName))]
        public string ReminderName { get; }

        public WorkflowInstanceReminderRecord(Guid instanceId, string activityDefinitionId, string state, string reminderName)
            : this(instanceId, 0, activityDefinitionId, state, reminderName)
        { }

        public WorkflowInstanceReminderRecord(Guid instanceId, long recordNumber, string activityDefinitionId, string state, string reminderName)
            : base(instanceId, recordNumber, activityDefinitionId, state)
        {
            if (string.IsNullOrEmpty(activityDefinitionId))
                throw new ArgumentNullException(nameof(activityDefinitionId));
            if (reminderName == null)
                throw new ArgumentNullException(nameof(reminderName));
            ReminderName = reminderName;
            Level = TraceLevel.Info;
        }

        public WorkflowInstanceReminderRecord(Guid instanceId, string activityDefinitionId, string state, string reminderName, WorkflowIdentity workflowDefinitionIdentity)
            : this(instanceId, activityDefinitionId, state, reminderName)
        {
            WorkflowDefinitionIdentity = workflowDefinitionIdentity;
        }

        public WorkflowInstanceReminderRecord(Guid instanceId, long recordNumber, string activityDefinitionId, string state, string reminderName, WorkflowIdentity workflowDefinitionIdentity)
            : this(instanceId, recordNumber, activityDefinitionId, state, reminderName)
        {
            WorkflowDefinitionIdentity = workflowDefinitionIdentity;
        }

        private WorkflowInstanceReminderRecord(WorkflowInstanceReminderRecord record)
            : base(record)
        {
            ReminderName = record.ReminderName;
        }

        protected override TrackingRecord Clone() => new WorkflowInstanceReminderRecord(this);

        public override string ToString() =>
            // For backward compatibility, the ToString() does not return WorkflowIdentity, if it is null.
            WorkflowDefinitionIdentity == null
                ? $"WorkflowInstanceReminderRecord {{ InstanceId = {InstanceId}, RecordNumber = {RecordNumber}, EventTime = {EventTime}, ActivityDefinitionId = {ActivityDefinitionId}, State = {State}, ReminderName = {ReminderName} }} "
                : $"WorkflowInstanceReminderRecord {{ InstanceId = {InstanceId}, RecordNumber = {RecordNumber}, EventTime = {EventTime}, ActivityDefinitionId = {ActivityDefinitionId}, State = {State}, ReminderName = {ReminderName}, WorkflowDefinitionIdentity = {WorkflowDefinitionIdentity} }} ";
    }
}
