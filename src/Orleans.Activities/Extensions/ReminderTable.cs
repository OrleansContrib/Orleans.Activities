using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Hosting;
using System.Xml.Linq;
using Orleans.Activities.AsyncEx;
using Orleans.Activities.Hosting;

namespace Orleans.Activities.Extensions
{
    // TODO investigate the validity of these algorithms
    // TODO if it works, create standalone queues for the states below, because of performance
    // TODO alternative algorithm: register only 1 reminder per workflow (like the original TimerExtensions TimerExpirationTime used by SQL to reload the workflow),
    //      but in this case the registration (before/after save) seems to be tricky to be fail-safe
    //      (if shortens expiration, reregister before save??? if extends expiration, reregister after save??? what about non-persistence zones???)

    // - due to Reminder registration and unregistration is async, we can't register/unregister them in TimerExtension's OnRegisterTimer() and OnCancelTimer()
    // - due to Controller.GetBookmarks() returns only the named bookmarks, we have to save the bookmarks that have associated reminders
    //   to resume them and to recreate the cache described below on load
    // - due to reminder registration and unregistration doesn't happen in one transaction with the state persistence (not like in the original
    //   SqlWorkflowInstanceStore), we have to register reminders before and unregister them after state persistence to be fail-safe
    // - we store the registration/unregistration requests and during the OnPausedAsync, OnSavingAsync, OnSavedAsync "events" we register/unregister them
    // - it has the consequence, that the reminders are not created before the controller/executer gets idle,
    //   but due to single threadedness it wouldn't be able to resume them (these are plain old bookmarks)
    // - we always register them BEFORE save, so in the stored state, the stored controller/executor state and the registered reminders are correlate
    //   the only problem (when controller/executor state persistence aborts after reminders are registered), that maybe reminders are exist but the bookmarks are not
    //   (when the workflow is reloaded from a previous state), in that case we unregister the unnecessary reminders on load or after they fire
    //   this is a problem and extra activity only after serious failure, normal operation is not effected
    // - we always unregister them AFTER save (if cancel requested), so in worst case they will fire unnecessarily (see above)
    // - in case of nonpersistent idle, we also register the reminders (due to the OnPausedAsync "event"), but we can unregister them on idle also,
    //   because they are not saved
    // - the ReminderTable is never persisted with it's actual content, we don't save entries/bookmarks where the associated reminder will be unregistered during/after save!
    // - during save it registers a default reactivation reminder if the controller/executor state is runnable and there are no registered reminders for the instance,
    //   this way in case of failure (when the instance is aborted or the silo is crashed), the reactivation reminder will reactivate the grain/host/instance
    // - after save and load it unregisters the reactivation reminder if the controller/executor state is not runnable (ie. idle or completed)
    //   or there are registered reminders for the instance
    // - when the reactivation reminder fires, it is a no-op, it simply reactivates/reloads the grain/workflow if it's not activated or if it's aborted

    /// <summary>
    /// Used by <see cref="DurableReminderExtension"/>. Handles the persistence and notification (ie. idle) events related registration/unregistration functionality.
    /// </summary>
    public class ReminderTable
    {
        public enum ReminderState : int
        {
            NonExistent = 0,            // not a real, stored state, it is the result of a failed reminders.TryGetValue()
            RegisterAndSave,            // TimerExtension.OnRegisterTimer() called in NonExistent "state" - fresh new reminder, never saved, can be canceled during OnIdleAsync()
            ReregisterAndResave,        // TimerExtension.OnRegisterTimer() called in SaveAndUnregister state - previously saved, can NOT be canceled during OnIdleAsync()
            RegisteredButNotSaved,      // registered during OnIdleAsync(), but not saved yet
            RegisteredButNotResaved,    // registered during OnIdleAsync(), but not resaved yet
            RegisteredAndSaved,         // saved after registration
            SaveAndUnregister,          // TimerExtension.OnCancelTimer() called in RegisteredAndSaved state
            Unregister,                 // TimerExtension.OnCancelTimer() called in RegisteredButNotSaved state - never saved, can be canceled during OnIdleAsync()
        }

        // Transitions:           | Register               Unregister           OnPaused                              OnSaving                         OnSaved
        // -----------------------|----------------------------------------------------------------------------------------------------------------------------------------------
        // NonExistent            | ->RegisterAndSave      NOOP
        // RegisterAndSave        | ->RegisterAndSave      delete entry         register & ->RegisteredButNotSaved    register & ->RegisteredAndSaved  exception
        // ReregisterAndResave    | ->ReregisterAndResave  ->SaveAndUnregister  register & ->RegisteredButNotResaved  register & ->RegisteredAndSaved  exception
        // RegisteredButNotSaved  | exception              ->Unregister         NOOP                                  ->RegisteredAndSaved             exception
        // RegisteredButNotResaved| exception              ->SaveAndUnregister  NOOP                                  ->RegisteredAndSaved             exception
        // RegisteredAndSaved     | exception              ->SaveAndUnregister  NOOP                                  NOOP                             NOOP
        // SaveAndUnregister      | ->ReregisterAndResave  NOOP                 NOOP                                  NOOP                             unregister & delete entry
        // Unregister             | ->RegisterAndSave      NOOP                 unregister & delete entry             unregister & delete entry        exception

        protected class ReminderInfo
        {
            public Bookmark Bookmark { get; private set; }
            public DateTime DueTime { get; private set; }
            public ReminderState ReminderState { get; set; }

            public ReminderInfo(Bookmark bookmark, ReminderState reminderState)
                : this(bookmark, reminderState, default)
            { }

            public ReminderInfo(Bookmark bookmark, ReminderState reminderState, DateTime dueTime)
            {
                this.Bookmark = bookmark;
                this.DueTime = dueTime;
                this.ReminderState = reminderState;
            }
        }

        public static class WorkflowNamespace
        {
            private static readonly XNamespace RemindersPath = XNamespace.Get(Persistence.WorkflowNamespace.BaseNamespace + "/reminders");

            public static readonly XName Bookmarks = RemindersPath.GetName(nameof(Bookmarks));

            public static readonly XName Reactivation = RemindersPath.GetName(nameof(Reactivation));

            public static readonly string ReminderNameForReactivation = Reactivation.ToString();
            public static readonly string ReminderPrefixForBookmarks = "{" + RemindersPath.NamespaceName + "/bookmarks" + "}";
        }

        protected IActivityContext instance;
        protected IDictionary<string, ReminderInfo> reminders;
        protected bool hasReactivationReminder;

        public ReminderTable(IActivityContext instance)
        {
            this.instance = instance;
            this.reminders = new Dictionary<string, ReminderInfo>();
            //this.hasReactivationReminder = false;
        }

        public static bool IsReactivationReminder(string reminderName)
            => reminderName == WorkflowNamespace.ReminderNameForReactivation;

        public Bookmark GetBookmark(string reminderName)
        {
            if (this.reminders.TryGetValue(reminderName, out var reminderInfo))
                return reminderInfo.Bookmark;
            return null;
        }

        protected static string CreateReminderName(Bookmark bookmark)
            => WorkflowNamespace.ReminderPrefixForBookmarks + bookmark.ToString();

        public void RegisterOrUpdateReminder(Bookmark bookmark, TimeSpan dueTime)
        {
            var reminderName = CreateReminderName(bookmark);
            ReminderState reminderState;
            if (this.reminders.TryGetValue(reminderName, out var reminderInfo))
                reminderState = reminderInfo.ReminderState;
            else
                reminderState = ReminderState.NonExistent;
            switch (reminderState)
            {
                case ReminderState.NonExistent:
                case ReminderState.RegisterAndSave:
                case ReminderState.Unregister:
                    this.reminders[reminderName] = new ReminderInfo(bookmark, ReminderState.RegisterAndSave, DateTime.UtcNow + dueTime);
                    break;
                case ReminderState.ReregisterAndResave:
                case ReminderState.SaveAndUnregister:
                    this.reminders[reminderName] = new ReminderInfo(bookmark, ReminderState.ReregisterAndResave, DateTime.UtcNow + dueTime);
                    break;
                //case ReminderState.RegisteredButNotSaved:
                //case ReminderState.RegisteredButNotResaved:
                //case ReminderState.RegisteredAndSaved:
                default:
                    throw new InvalidOperationException($"Reminder '{reminderName}' can't be registered in state '{reminderState}'.");
            }
        }

        public void UnregisterReminder(Bookmark bookmark)
            => UnregisterReminder(CreateReminderName(bookmark));

        public void UnregisterReminder(string reminderName)
        {
            ReminderState reminderState;
            if (this.reminders.TryGetValue(reminderName, out var reminderInfo))
                reminderState = reminderInfo.ReminderState;
            else
                reminderState = ReminderState.NonExistent;
            switch (reminderState)
            {
                case ReminderState.NonExistent:
                case ReminderState.SaveAndUnregister:
                case ReminderState.Unregister:
                    break;
                case ReminderState.RegisterAndSave:
                    this.reminders.Remove(reminderName);
                    break;
                case ReminderState.ReregisterAndResave:
                case ReminderState.RegisteredButNotResaved:
                case ReminderState.RegisteredAndSaved:
                    reminderInfo.ReminderState = ReminderState.SaveAndUnregister;
                    break;
                case ReminderState.RegisteredButNotSaved:
                    reminderInfo.ReminderState = ReminderState.Unregister;
                    break;
                default:
                    throw new InvalidOperationException($"Reminder '{reminderName}' can't be unregistered in state '{reminderState}'.");
            }
        }

        protected static bool ParticipateInOnPaused(ReminderState reminderState)
            => reminderState == ReminderState.RegisterAndSave
            || reminderState == ReminderState.ReregisterAndResave
            || reminderState == ReminderState.Unregister;

        public async Task OnPausedAsync()
        {
            foreach (var kvp in this.reminders
                .Where((kvp) => ParticipateInOnPaused(kvp.Value.ReminderState))
                .ToList()) // because we will modify the dictionary
                switch (kvp.Value.ReminderState)
                {
                    case ReminderState.RegisterAndSave:
                        kvp.Value.ReminderState = ReminderState.RegisteredButNotSaved;
                        await this.instance.RegisterOrUpdateReminderAsync(kvp.Key, kvp.Value.DueTime - DateTime.UtcNow);
                        break;
                    case ReminderState.ReregisterAndResave:
                        kvp.Value.ReminderState = ReminderState.RegisteredButNotResaved;
                        await this.instance.RegisterOrUpdateReminderAsync(kvp.Key, kvp.Value.DueTime - DateTime.UtcNow);
                        break;
                    case ReminderState.Unregister:
                        this.reminders.Remove(kvp.Key);
                        await this.instance.UnregisterReminderAsync(kvp.Key);
                        break;
                    //case ReminderState.RegisteredButNotSaved:
                    //case ReminderState.RegisteredButNotResaved:
                    //case ReminderState.RegisteredAndSaved:
                    //case ReminderState.SaveAndUnregister:
                    default:
                        throw new InvalidOperationException($"Reminder '{kvp.Key}' is in state '{kvp.Value.ReminderState}' during OnPaused.");
                }
        }

        protected static bool ParticipateInCollectValues(ReminderState reminderState)
            // do not save bookmarks where the associated reminder will be unregistered during/after save
            => reminderState != ReminderState.SaveAndUnregister
            && reminderState != ReminderState.Unregister;

        public void CollectValues(out IDictionary<XName, object> readWriteValues, out IDictionary<XName, object> writeOnlyValues)
        {
            readWriteValues = null;
            writeOnlyValues = null;

            if (this.reminders.Count > 0)
            {
                readWriteValues = new Dictionary<XName, object>(1) {{ WorkflowNamespace.Bookmarks, this.reminders
                    .Where((kvp) => ParticipateInCollectValues(kvp.Value.ReminderState))
                    .Select((kvp) => kvp.Value.Bookmark)
                    .ToList()}};
            }
        }

        protected Task RegisterReactivationReminderIfRequired()
        {
            if (this.instance.WorkflowInstanceState == WorkflowInstanceState.Runnable
                && !this.reminders.Where((kvp) => kvp.Value.ReminderState == ReminderState.RegisteredAndSaved).Any())
            {
                // always update it on each save, not just when not yet registered
                this.hasReactivationReminder = true;
                return this.instance.RegisterOrUpdateReminderAsync(WorkflowNamespace.ReminderNameForReactivation, this.instance.Parameters.ReactivationReminderPeriod);
            }
            else
                return TaskConstants.Completed;
        }

        protected Task UnregisterReactivationReminderIfNotRequired()
        {
            if (this.hasReactivationReminder
                && (this.instance.WorkflowInstanceState != WorkflowInstanceState.Runnable
                    || this.reminders.Where((kvp) => kvp.Value.ReminderState == ReminderState.RegisteredAndSaved).Any()))
            {
                this.hasReactivationReminder = false;
                return this.instance.UnregisterReminderAsync(WorkflowNamespace.ReminderNameForReactivation);
            }
            else
                return TaskConstants.Completed;
        }

        protected static bool ParticipateInOnSaving(ReminderState reminderState)
            => reminderState != ReminderState.RegisteredAndSaved
            && reminderState != ReminderState.SaveAndUnregister;

        public async Task OnSavingAsync()
        {
            foreach (var kvp in this.reminders
                .Where((kvp) => ParticipateInOnSaving(kvp.Value.ReminderState))
                .ToList()) // because we will modify the dictionary
                switch (kvp.Value.ReminderState)
                {
                    case ReminderState.RegisterAndSave:
                    case ReminderState.ReregisterAndResave:
                        kvp.Value.ReminderState = ReminderState.RegisteredAndSaved;
                        await this.instance.RegisterOrUpdateReminderAsync(kvp.Key, kvp.Value.DueTime - DateTime.UtcNow);
                        break;
                    case ReminderState.RegisteredButNotSaved:
                    case ReminderState.RegisteredButNotResaved:
                        kvp.Value.ReminderState = ReminderState.RegisteredAndSaved;
                        break;
                    case ReminderState.Unregister:
                        this.reminders.Remove(kvp.Key);
                        await this.instance.UnregisterReminderAsync(kvp.Key);
                        break;
                    //case ReminderState.RegisteredAndSaved:
                    //case ReminderState.SaveAndUnregister:
                    default:
                        throw new InvalidOperationException($"Reminder '{kvp.Key}' is in state '{kvp.Value.ReminderState}' during OnSaving.");
                }

            await RegisterReactivationReminderIfRequired();
        }

        public async Task OnSavedAsync()
        {
            foreach (var kvp in this.reminders
                .Where((kvp) => kvp.Value.ReminderState != ReminderState.RegisteredAndSaved)
                .ToList()) // because we will modify the dictionary
                switch (kvp.Value.ReminderState)
                {
                    case ReminderState.SaveAndUnregister:
                        this.reminders.Remove(kvp.Key);
                        await this.instance.UnregisterReminderAsync(kvp.Key);
                        break;
                    //case ReminderState.RegisterAndSave:
                    //case ReminderState.ReregisterAndResave:
                    //case ReminderState.RegisteredButNotSaved:
                    //case ReminderState.RegisteredButNotResaved:
                    //case ReminderState.Unregister:
                    default:
                        throw new InvalidOperationException($"Reminder '{kvp.Key}' is in state '{kvp.Value.ReminderState}' during OnSaved.");
                }

            await UnregisterReactivationReminderIfNotRequired();
        }

        public async Task LoadAsync(IDictionary<XName, object> readWriteValues)
        {
            this.reminders.Clear();
            this.hasReactivationReminder = false;

            if (readWriteValues != null && readWriteValues.TryGetValue(WorkflowNamespace.Bookmarks, out var reminderBookmarks))
                foreach (var reminderBookmark in (reminderBookmarks as List<Bookmark>))
                    this.reminders[CreateReminderName(reminderBookmark)] = new ReminderInfo(reminderBookmark, ReminderState.RegisteredAndSaved);
            foreach (var reminderName in await this.instance.GetRemindersAsync())
                if (reminderName == WorkflowNamespace.ReminderNameForReactivation)
                    this.hasReactivationReminder = true;
                else if (reminderName.StartsWith(WorkflowNamespace.ReminderPrefixForBookmarks) // there can be other reminders
                    && !this.reminders.ContainsKey(reminderName))
                    await this.instance.UnregisterReminderAsync(reminderName);

            await UnregisterReactivationReminderIfNotRequired();
        }
    }
}
