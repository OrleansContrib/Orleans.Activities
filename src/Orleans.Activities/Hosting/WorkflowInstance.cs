using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.DynamicUpdate;
using System.Activities.Hosting;
using System.Activities.Tracking;
using System.Runtime.DurableInstancing;
using System.Xml.Linq;
using Orleans.Activities.AsyncEx;
using Orleans.Activities.Configuration;
using Orleans.Activities.Extensions;
using Orleans.Activities.Notification;
using Orleans.Activities.Persistence;
using Orleans.Activities.Tracking;

namespace Orleans.Activities.Hosting
{
    // Main concept
    // - from outside a WorkflowGrain is indistinguishable from a normal Grain; WorkflowGrain is the only class that has dependency on Orleans runtime!
    // - each WorkflowGrain has it's own WorkflowHost
    // - each WorkflowHost hosts a WorkflowInstance (and recreates it when it aborts)
    // - if you want to access another workflow, access the WorkflowGrain that hosts it

    // WorkflowInstance implementation details
    // - this is the basic, original WFI (System.Activities.Hosting.WorkflowInstance), extended with simple (but extension aware) persistence, and 2 extra events:
    //   - OnIdleAsync is called in case the controller/executor goes idle (even it is nonpersistable), and
    //   - OnSavedAsync is called after the instanceValues are saved by the host/grain
    //   these events are used by DurableReminderExtension, or any notification- or persistence-participant extension
    // - must be hosted with WorkflowHost, because WorkflowHost will recreate it from the latest known/saved state if it aborts
    // - can only be used with a "single threaded", optionally reentrant scheduler,
    //   but WorkflowHost will degrade it always to non-reentrant in case of workflow/activities, this is a WFI design requirement,
    //   but in WFI non-reentrant means, it can't accept requests until it goes idle
    // - after the Run() is called on controller/executor it returns immediately, but runs in the "background" until it runs out of work to do
    // - controller/executor's Run() after running out of work to do will call OnNotifyPaused()
    // - OnNotifyPaused() if necessary calls hosts's OnCompletedAsync() and persists state, and at the end signals WorkflowHost that it is idle (OnNotifyIdle),
    //   in case of an exception during OnNotifyPaused() it can abort/cancel/terminate,
    //   if cancel/terminate called and additional work should be done, it calls Run() instead of signaling WorkflowHost that it is idle
    // - in case of unhandled exception in the workflow, controller/executor calls OnNotifyUnhandledException()
    // - OnNotifyUnhandledException() notifies the host (OnUnhandledExceptionAsync) and by default aborts, but completion with cancel or terminate also available,
    //   at the end it calls WFI's OnNotifyPaused() (see above)

    // The ActivityExecutor's protocol for endgame is weird
    // - normally it calls OnNotifyPaused()
    // - in case of unhandled exception, calls OnNotifyUnhandledException() (and WFI can decide how to handle it, Abort, Cancel or Terminate)
    // - in case of exception inside the ActivityExecutor (eg. during persistence), it doesn't propagate the Fault through the workflow, but calls OnRequestAbort() AND OnNotifyPaused(),
    //   and aborts, WFI can't decide how to handle it (nor Cancel nor Terminate is possible)
    // The protocol between WorkflowInstance and WorkflowHost is simplified
    // - when WFI runs out of work to do, it always calls OnNotifyIdle() (whether OnNotifyPaused() or OnNotifyUnhandledException() is the last callout from ActivityExecutor)
    // - optionally, if there were unhandled exceptions, it calls OnUnhandledExceptionAsync() before OnNotifyIdle() (whether OnNotifyUnhandledException() or OnRequestAbort()
    //   contains the exception), this way WorkflowHost can redirect the exception to the current TaskCompletionSource (if there are any), this is the only way to complete them, 
    //   due to ActivityExecutor skips the normal Fault propagation, none of the activities can complete the TaskCompletionSource

    // It is important to understand, that WFI has 4 states: Runnable, Idle, Complete and Aborted
    // - Complete has 3 substates: Closed (successfully completed), Canceled, Faulted (ie. Terminate called on it by unhandled exception or by Terminate activity)
    // - Aborted is not a "real" state, you can't even save/persist the WFI in this state, it means it's state got invalid (due to an unhandled exception),
    //   and due to this invalid state you have to recreate the instance from the latest known/saved state to Run() it again

    /// <summary>
    /// WorkflowHost and WorkflowInstance are the main classes responsible to execute System.Activities workflows above an Orleans like single threaded reentrant scheduler.
    /// These classes convert the original persistence and timer API to an Orleans compatible persistence and reminder API, but remain compatible with legacy extensions and activities.
    /// These classes also implement an operation specific API to back grain's incoming and outgoing operations, in a way, where the main operation logic remains outside of activities,
    /// those are part of the grain implementation without depending on any workflow related functionality.
    /// <para>For implementation details see the detailed comments in the source code!</para>
    /// </summary>
    public class WorkflowInstance : System.Activities.Hosting.WorkflowInstance, IWorkflowInstance, IActivityContext
    {
        public static bool IsCompleted(IDictionary<XName, InstanceValue> instanceValues)
            => WorkflowStatus.IsCompleted(instanceValues[WorkflowNamespace.Status].Value as string);

        #region protected/private fields

        protected IWorkflowInstanceCallback host;

        private DurableReminderExtension durableReminderExtension;

        #endregion

        #region ctor

        public WorkflowInstance(IWorkflowInstanceCallback host, Activity workflowDefinition, WorkflowIdentity definitionIdentity)
            : base(workflowDefinition, definitionIdentity)
        {
            this.host = host;

            this.SynchronizationContext = new SynchronizationContext();
        }

        #endregion

        #region IActivityContext members

        public IParameters Parameters => this.host.Parameters;

        // It is common with IWorkflowInstance.
        //public WorkflowInstanceState WorkflowInstanceState { get; }

        // IsStarting is true from Start/Initialize up to the first persistable idle moment, when by default the first persistence is skipped
        // Save and Load preserves the value of IsStarting.
        public bool IsStarting { get; private set; }

        // IsReloaded is true from a Load with Runnable state up to the first Save.
        public bool IsReloaded { get; private set; }

        public bool TrackingEnabled => this.Controller.TrackingEnabled;

        public Task<BookmarkResumptionResult> ResumeBookmarkThroughHostAsync(Bookmark bookmark, object value, TimeSpan timeout)
            => this.host.ResumeBookmarkAsync(bookmark, value, timeout);

        public Task AbortThroughHostAsync(Exception reason)
            => this.host.AbortAsync(reason);

        public async Task<bool> NotifyHostOnUnhandledExceptionAsync(Exception exception, Activity source)
            => await ExecuteWithExceptionTrackingAsync(async () =>
            {
                await IfHasPendingThenFlushTrackingRecordsAsync();
                await this.host.OnUnhandledExceptionAsync(exception, source);
            }) == null;

        public Task<Func<Task<TResponseResult>>> OnOperationAsync<TRequestParameter, TResponseResult>(string operationName, TRequestParameter requestParameter)
            => this.host.OnOperationAsync<TRequestParameter, TResponseResult>(operationName, requestParameter);

        public Task<Func<Task>> OnOperationAsync<TRequestParameter>(string operationName, TRequestParameter requestParameter)
            => this.host.OnOperationAsync<TRequestParameter>(operationName, requestParameter);

        public Task<Func<Task<TResponseResult>>> OnOperationAsync<TResponseResult>(string operationName)
            => this.host.OnOperationAsync<TResponseResult>(operationName);

        public Task<Func<Task>> OnOperationAsync(string operationName)
            => this.host.OnOperationAsync(operationName);

        public async Task RegisterOrUpdateReminderAsync(string reminderName, TimeSpan dueTime)
        {
            if (this.Controller.TrackingEnabled)
            {
                this.Controller.Track(new WorkflowInstanceReminderRecord(this.Id, this.WorkflowDefinition.DisplayName,
                    Tracking.WorkflowInstanceStates.ReminderRegistered, reminderName, this.DefinitionIdentity));
                await IfHasPendingThenFlushTrackingRecordsAsync();
            }
            await this.host.RegisterOrUpdateReminderAsync(reminderName, dueTime);
        }

        public async Task UnregisterReminderAsync(string reminderName)
        {
            if (this.Controller.TrackingEnabled)
            {
                this.Controller.Track(new WorkflowInstanceReminderRecord(this.Id, this.WorkflowDefinition.DisplayName,
                    Tracking.WorkflowInstanceStates.ReminderUnregistered, reminderName, this.DefinitionIdentity));
                await IfHasPendingThenFlushTrackingRecordsAsync();
            }
            await this.host.UnregisterReminderAsync(reminderName);
        }

        public Task<IEnumerable<string>> GetRemindersAsync()
            => this.host.GetRemindersAsync();

        #endregion

        #region IWorkflowInstance members

        public void Start(IDictionary<string, object> inputArguments, IEnumerable<object> extensions)
        {
            RegisterExtensions(extensions);
            Start(inputArguments);
        }

        // TODO Support updates with DynamicUpdateMap.
        public async Task LoadAsync(IDictionary<XName, InstanceValue> instanceValues, IEnumerable<object> extensions)
        {
            RegisterExtensions(extensions);
            await LoadAsync(instanceValues);
        }

        public Task DeactivateAsync()
        {
            if (this.Controller.TrackingEnabled)
            {
                this.Controller.Track(new WorkflowInstanceRecord(this.Id, this.WorkflowDefinition.DisplayName,
                    Tracking.WorkflowInstanceStates.Deactivated, this.DefinitionIdentity));
                return IfHasPendingThenFlushTrackingRecordsAsync();
            }
            else
                return TaskConstants.Completed;
        }

        public WorkflowInstanceState WorkflowInstanceState =>
            this.Controller.State;

        public async Task AbortAsync(Exception reason)
        {
            await IfHasPendingThenFlushTrackingRecordsAsync();
            // We can't call Abort() on the persistence pipeline, because we can't be reentrant, when we are here, no pipeline operations are running.
            this.Controller.Abort(reason);
            // Call it, because RunAsync() won't be called after Abort().
            await IfHasPendingThenFlushTrackingRecordsAsync();
        }

        public Task ScheduleCancelAsync()
        {
            this.Controller.ScheduleCancel();
            return IfHasPendingThenFlushTrackingRecordsAsync();
        }

        public Task TerminateAsync(Exception reason)
        {
            this.Controller.Terminate(reason);
            return IfHasPendingThenFlushTrackingRecordsAsync();
        }

        public async Task<BookmarkResumptionResult> ScheduleBookmarkResumptionAsync(Bookmark bookmark, object value)
        {
            var resumptionResult = this.Controller.ScheduleBookmarkResumption(bookmark, value);
            if (resumptionResult == BookmarkResumptionResult.Success)
                await IfHasPendingThenFlushTrackingRecordsAsync();
            return resumptionResult;
        }

        public Task<BookmarkResumptionResult> ScheduleOperationBookmarkResumptionAsync(string operationName, object value)
            => ScheduleBookmarkResumptionAsync(new Bookmark(operationName), value);

        public async Task<BookmarkResumptionResult> ScheduleReminderBookmarkResumptionAsync(string reminderName)
        {
            var result = BookmarkResumptionResult.Success;
            if (!this.durableReminderExtension.IsReactivationReminder(reminderName))
            {
                result = await ScheduleBookmarkResumptionAsync(this.durableReminderExtension.GetBookmark(reminderName), null);
                if (result != BookmarkResumptionResult.NotReady || this.WorkflowInstanceState == WorkflowInstanceState.Complete) // Success || NotFound || NotReady && Complete
                    this.durableReminderExtension.UnregisterReminder(reminderName);
            }
            return result;
        }

        public Task RunAsync()
        {
            this.Controller.Run();
            return IfHasPendingThenFlushTrackingRecordsAsync();
        }

        #endregion

        #region notification methods

        // Used by OnNotifyPausedAsync().
        protected async Task OnPausedAsync()
        {
            var notificationParticipants = GetExtensions<INotificationParticipant>();
            // If the notificationParticipants throw during OnPausedAsync, the pipeline will rethrow, and the controller/executor will abort.
            if (notificationParticipants.Any())
            {
                var notificationPipeline = new NotificationPipeline(notificationParticipants);
                await notificationPipeline.OnPausedAsync(this.Parameters.ExtensionsPersistenceTimeout);
            }
        }

        #endregion

        #region persistence methods

        // Used by IWorkflowInstance.Start() and IWorkflowInstance.LoadAsync()
        protected void RegisterExtensions(IEnumerable<object> extensions)
        {
            var extensionManager = new WorkflowInstanceExtensionManager();

            extensionManager.Add(new ActivityContextExtension(this));

            // Add it in advance to prevent existing TimerExtension dependent activities to create DurableTimerExtension.
            this.durableReminderExtension = new DurableReminderExtension(this);
            extensionManager.Add(this.durableReminderExtension);

            if (extensions != null)
                foreach (var extension in extensions)
                    extensionManager.Add(extension);

            RegisterExtensionManager(extensionManager);
        }

        // Used by IWorkflowInstance.Start().
        protected void Start(IDictionary<string, object> inputArguments)
        {
            Initialize(inputArguments, default);
            this.IsStarting = true;
        }

        // Used by OnPersist() and OnNotifyPaused().
        protected async Task SaveAsync()
        {
            if (this.Controller.TrackingEnabled)
            {
                this.Controller.Track(new WorkflowInstanceRecord(this.Id, this.WorkflowDefinition.DisplayName,
                    System.Activities.Tracking.WorkflowInstanceStates.Persisted, this.DefinitionIdentity));
                await IfHasPendingThenFlushTrackingRecordsAsync();
            }

            var instanceValues = SaveWorkflow();
            this.IsReloaded = false;

            // Yes, IEnumerable<object> is ugly, but there is nothing common in IPersistenceParticipant and PersistenceParticipant.
            var persistenceParticipants =
                ((IEnumerable<object>)GetExtensions<System.Activities.Persistence.PersistenceParticipant>())
                .Concat(
                ((IEnumerable<object>)GetExtensions<IPersistenceParticipant>()));
            // If the persistenceParticipants throw during OnSaveAsync() and OnLoadAsync(), the pipeline will rethrow, and the controller/executor will abort.
            if (persistenceParticipants.Any())
            {
                var persistencePipeline = new PersistencePipeline(persistenceParticipants, instanceValues, this.Parameters.PersistWriteOnlyValues);
                persistencePipeline.Collect();
                persistencePipeline.Map();
                await persistencePipeline.OnSaveAsync(this.Parameters.ExtensionsPersistenceTimeout);
                await this.host.SaveAsync(instanceValues);
                await persistencePipeline.OnSavedAsync(this.Parameters.ExtensionsPersistenceTimeout);
            }
            else
                await this.host.SaveAsync(instanceValues);
        }

        // Used by IWorkflowInstance.LoadAsync()
        protected async Task LoadAsync(IDictionary<XName, InstanceValue> instanceValues)
        {
            Initialize(LoadWorkflow(instanceValues), default);
            if (this.Controller.State == WorkflowInstanceState.Runnable)
                this.IsReloaded = true;

            // Yes, IEnumerable<object> is ugly, but there is nothing common in IPersistenceParticipant and PersistenceParticipant.
            var persistenceParticipants =
                ((IEnumerable<object>)GetExtensions<System.Activities.Persistence.PersistenceParticipant>())
                .Concat(
                ((IEnumerable<object>)GetExtensions<IPersistenceParticipant>()));
            // If the persistenceParticipants throw during OnSaveAsync() and OnLoadAsync(), the pipeline will rethrow, and the controller/executor will abort.
            if (persistenceParticipants.Any())
            {
                var persistencePipeline = new PersistencePipeline(persistenceParticipants, instanceValues);
                await persistencePipeline.OnLoadAsync(this.Parameters.ExtensionsPersistenceTimeout);
                persistencePipeline.Publish();
            }
        }

        private IDictionary<XName, InstanceValue> SaveWorkflow()
        {
            if (this.Controller.State == WorkflowInstanceState.Aborted)
                throw new InvalidOperationException("Cannot generate data for an aborted instance.");

            var instanceValues = new Dictionary<XName, InstanceValue>(10);

            if (this.Parameters.PersistWriteOnlyValues)
            {
                instanceValues[WorkflowNamespace.LastUpdate] = new InstanceValue(DateTime.UtcNow, InstanceValueOptions.WriteOnly | InstanceValueOptions.Optional);
                instanceValues[WorkflowNamespace.Bookmarks] = new InstanceValue(this.Controller.GetBookmarks(), InstanceValueOptions.WriteOnly | InstanceValueOptions.Optional);
                foreach (var mappedVariable in this.Controller.GetMappedVariables())
                    instanceValues[WorkflowNamespace.VariablesPath.GetName(mappedVariable.Key)] = new InstanceValue(mappedVariable.Value, InstanceValueOptions.WriteOnly | InstanceValueOptions.Optional);
            }

            if (this.Controller.State != WorkflowInstanceState.Complete) // Idle || Runnable
            {
                instanceValues[WorkflowNamespace.Workflow] = new InstanceValue(this.Controller.PrepareForSerialization());
                if (this.IsStarting)
                    instanceValues[WorkflowNamespace.IsStarting] = new InstanceValue(true);
                instanceValues[WorkflowNamespace.Status] = new InstanceValue(this.Controller.State == WorkflowInstanceState.Idle ? WorkflowStatus.Idle : WorkflowStatus.Executing);
            }
            else // Complete
            {
                if (this.Parameters.PersistWriteOnlyValues)
                    instanceValues[WorkflowNamespace.Workflow] = new InstanceValue(this.Controller.PrepareForSerialization(), InstanceValueOptions.Optional);

                var completionState = this.Controller.GetCompletionState(out var outputs, out var terminationException);

                if (completionState == ActivityInstanceState.Closed)
                {
                    instanceValues[WorkflowNamespace.Status] = new InstanceValue(WorkflowStatus.Closed);
                    if (outputs != null)
                        foreach (var output in outputs)
                            instanceValues[WorkflowNamespace.OutputPath.GetName(output.Key)] = new InstanceValue(output.Value);
                }
                else if (completionState == ActivityInstanceState.Canceled)
                {
                    instanceValues[WorkflowNamespace.Status] = new InstanceValue(WorkflowStatus.Canceled);
                }
                else // Faulted
                {
                    instanceValues[WorkflowNamespace.Status] = new InstanceValue(WorkflowStatus.Faulted);
                    instanceValues[WorkflowNamespace.Exception] = new InstanceValue(terminationException);
                }
            }
            return instanceValues;
        }

        private object LoadWorkflow(IDictionary<XName, InstanceValue> instanceValues)
        {
            this.IsStarting = instanceValues.TryGetValue(WorkflowNamespace.IsStarting, out var value) && (bool)value.Value;
            return instanceValues[WorkflowNamespace.Workflow].Value;
        }

        #endregion

        #region tracking methods

        protected Task IfHasPendingThenFlushTrackingRecordsAsync()
        {
            if (this.Controller.HasPendingTrackingRecords)
                return AsyncFactory.FromApm<TimeSpan>(this.Controller.BeginFlushTrackingRecords, this.Controller.EndFlushTrackingRecords, this.Parameters.TrackingTimeout);
            else
                return TaskConstants.Completed;
        }

        // Used by OnNotifyUnhandledExceptionAsync() and OnNotifyPausedAsync().
        protected async Task<Exception> ExecuteWithExceptionTrackingAsync(Action actionToExecute)
        {
            try
            {
                actionToExecute();
                return null;
            }
            catch (Exception e)
            {
                if (this.Controller.TrackingEnabled)
                    try
                    {
                        // At least leave some trace of this exception 
                        this.Controller.Track(new WorkflowInstanceExceptionRecord(
                            this.Id, this.WorkflowDefinition.DisplayName, e, this.DefinitionIdentity));
                        await IfHasPendingThenFlushTrackingRecordsAsync();
                    }
                    catch
                    { }
                return e;
            }
        }

        // Used by OnNotifyUnhandledExceptionAsync() and OnNotifyPausedAsync().
        protected async Task<Exception> ExecuteWithExceptionTrackingAsync(Func<Task> asyncActionToExecute)
        {
            try
            {
                await asyncActionToExecute();
                return null;
            }
            catch (Exception e)
            {
                if (this.Controller.TrackingEnabled)
                    try
                    {
                        // At least leave some trace of this exception 
                        this.Controller.Track(new WorkflowInstanceExceptionRecord(
                            this.Id, this.WorkflowDefinition.DisplayName, e, this.DefinitionIdentity));
                        await IfHasPendingThenFlushTrackingRecordsAsync();
                    }
                    catch
                    { }
               return e;
            }
        }

        #endregion

        #region WorkflowInstance abstract method implementations

        public override Guid Id => this.host.PrimaryKey;

        #region instance keys

        // TODO Can we use instance keys for anything under Orleans at all?
        protected override bool SupportsInstanceKeys => false;

        protected override IAsyncResult OnBeginAssociateKeys(ICollection<InstanceKey> keys, AsyncCallback callback, object state) => throw new NotImplementedException();

        protected override void OnEndAssociateKeys(IAsyncResult result) => throw new NotImplementedException();

        protected override void OnDisassociateKeys(ICollection<InstanceKey> keys) => throw new NotImplementedException();

        #endregion

        #region persistence

        protected override IAsyncResult OnBeginPersist(AsyncCallback callback, object state)
            => AsyncFactory.ToBegin(SaveAsync(), callback, state);

        protected override void OnEndPersist(IAsyncResult result)
            => AsyncFactory.ToEnd(result);

        #endregion

        #region bookmark resumption

        protected override IAsyncResult OnBeginResumeBookmark(Bookmark bookmark, object value, TimeSpan timeout, AsyncCallback callback, object state)
            => AsyncFactory<BookmarkResumptionResult>.ToBegin(this.host.ResumeBookmarkAsync(bookmark, value, timeout), callback, state);

        protected override BookmarkResumptionResult OnEndResumeBookmark(IAsyncResult result)
            => AsyncFactory<BookmarkResumptionResult>.ToEnd(result);

        #endregion

        #region notification

        // - after controller/executor Run() has called, when it runs out of work or unhandled exception occurs,
        //   it calls OnNotifyPaused() or OnNotifyUnhandledException() respectively
        // - these methods run fire and forget,
        //   every incoming operation/reminder/abort/cancel/terminate are await-ing the idle reset event in the host, we only have to guarantee we will set it later,
        //   until we set it, NO other workflow "threads" are running, even if the grain is reentrant,
        //   currently we are on the tail of the previous operation/reminder/cancel/terminate that called Run(),
        //   and the controller/executor has run out of work to do, ie. got paused/idle
        // - let the controller/executor to finish first with Task.Yield(), we can call even the controller/executor Run() later
        // - OnRequestAbort() is a special case, there are situations (eg. persistence) when an unhandled exception automatically causes an abort,
        //   in this case OnRequestAbort() will be called instead of OnNotifyUnhandledException(),
        //   and later OnNotifyPaused() in an already aborted state

        // Callback from the controller/executor at the end.
        protected override void OnNotifyUnhandledException(Exception exception, Activity source, string sourceInstanceId)
            => Task.Factory.StartNew(async () =>
            {
                await Task.Yield();
                await OnNotifyUnhandledExceptionAsync(exception, source, sourceInstanceId);
            });

        // The TAP async version of OnNotifyUnhandledException() above.
        protected async Task OnNotifyUnhandledExceptionAsync(Exception exception, Activity source, string sourceInstanceId)
        {
            var unhandledExceptionAction = this.Parameters.UnhandledExceptionAction;
            if (! await NotifyHostOnUnhandledExceptionAsync(exception, source))
                // If the host can't handle it, the instance will abort, independently from the configuration.
                unhandledExceptionAction = UnhandledExceptionAction.Abort;
            // TODO Do we really need to protect againts exceptions below? Theoretically Controller won't throw now.
            await ExecuteWithExceptionTrackingAsync(() =>
            {
                switch (unhandledExceptionAction)
                {
                    default:
                    case UnhandledExceptionAction.Abort:
                        return AbortAsync(exception);
                    case UnhandledExceptionAction.Cancel:
                        return ScheduleCancelAsync();
                    case UnhandledExceptionAction.Terminate:
                        return TerminateAsync(exception);
                }
            }); 
            // Continue with the final processing.
            await OnNotifyPausedAsync();
        }

        // - in contrast with the OnNotifyUnhandledException(), controller/executor will call OnNotifyPaused() after this, this is not a final callback,
        //   so we store the exception and we will handle it on the final callback
        // - we don't call Abort() on the persistence pipeline, any potential exception in the pipeline IO operations already caused Abort in the pipeline,
        //   or the pipeline already finished it's operations successfully
        // - we don't call Controller.Abort(), because controller/executor is still running, and will abort automatically
        // - we don't call tracking, because that is async, and controller/executor is already tracking it's activities
        protected override void OnRequestAbort(Exception reason)
            => this.onRequestAbortReason = reason;

        // Callback from the controller/executor at the end.
        protected override void OnNotifyPaused()
            => Task.Factory.StartNew(async () =>
            {
                await Task.Yield();
                await OnNotifyPausedAsync();
            });

        private Exception onRequestAbortReason;
        private bool hasRaisedCompleted;

        // The TAP async version of OnNotifyPaused() above.
        protected async Task OnNotifyPausedAsync()
        {
            var workflowInstanceState = this.Controller.State;
            if (workflowInstanceState == WorkflowInstanceState.Aborted)
            {
                // If there were a OnRequestAbort() previously, notify the host now.
                if (this.onRequestAbortReason != null)
                    await NotifyHostOnUnhandledExceptionAsync(this.onRequestAbortReason, null);
            }
            else
            {
                try
                {
                    // If it's completed, call OnCompletedAsync() on host.
                    if (workflowInstanceState == WorkflowInstanceState.Complete
                        && !this.hasRaisedCompleted)
                    {
                        await IfHasPendingThenFlushTrackingRecordsAsync();
                        var completionState = this.Controller.GetCompletionState(out var outputs, out var terminationException);
                        await this.host.OnCompletedAsync(completionState, outputs, terminationException);
                        this.hasRaisedCompleted = true;
                    }

                    // Call OnPausedAsync() on INotificationParticipant extensions.
                    await OnPausedAsync();

                    // Track the Idle state.
                    if (workflowInstanceState == WorkflowInstanceState.Idle
                        && this.Controller.TrackingEnabled)
                    {
                        this.Controller.Track(new WorkflowInstanceRecord(this.Id, this.WorkflowDefinition.DisplayName,
                            System.Activities.Tracking.WorkflowInstanceStates.Idle, this.DefinitionIdentity));
                        await IfHasPendingThenFlushTrackingRecordsAsync();
                    }

                    // If required, save state.
                    if (this.Controller.IsPersistable)
                    {
                        if (IdlePersistenceModeExtensions.ShouldSave(this.Parameters.IdlePersistenceMode, workflowInstanceState, this.IsStarting))
                            await SaveAsync();
                        this.IsStarting = false;
                    }
                }
                catch (Exception e)
                {
                    // Notify host, this can be eg. a serialization exception!
                    await NotifyHostOnUnhandledExceptionAsync(e, null);
                    // TODO Do we really need to protect againts exceptions below? Theoretically Controller won't throw now.
                    // The instance will abort, independently from the configuration.
                    await ExecuteWithExceptionTrackingAsync(() =>
                    {
                        return AbortAsync(e);
                    });
                    workflowInstanceState = this.Controller.State;
                }
            }
            // finally
            // TODO Do we really need to protect againts exceptions below? Theoretically Controller won't throw now.
            await ExecuteWithExceptionTrackingAsync(() =>
            {
                // At this point it is possible, that Cancel/Teminate was called by OnNotifyUnhandledException() or OnNotifyPaused(),
                // and controller/executor Run() should be called, in this case we won't set the reset event,
                // the Run() will result a callback to OnNotifyPaused() or OnNotifyUnhandledException() again.
                if (workflowInstanceState == WorkflowInstanceState.Runnable)
                    return RunAsync();
                else
                {
                    this.host.OnNotifyIdle();
                    return TaskConstants.Completed;
                }
            });
        }

        #endregion

        #endregion
    }
}
