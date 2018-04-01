using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities.Persistence;
using System.Runtime.DurableInstancing;
using System.Xml.Linq;
using Orleans.Activities.Hosting;

namespace Orleans.Activities.Notification
{
    /// <summary>
    /// Handles the processing of the extensions that implement INotificationParticipant interface.
    /// </summary>
    public class NotificationPipeline
    {
        private readonly IEnumerable<INotificationParticipant> notificationParticipants;

        public NotificationPipeline(IEnumerable<INotificationParticipant> notificationParticipants)
            => this.notificationParticipants = notificationParticipants;

        // TODO Handle timeout correctly, ie. decrement remaining time in each for loop.
        public async Task OnPausedAsync(TimeSpan timeout)
        {
            try
            {
                foreach (var notificationParticipant in this.notificationParticipants)
                    await notificationParticipant.OnPausedAsync(timeout);
            }
            catch
            {
                Abort();
                // TODO Original pipeline seems to drop this to the floor, but the reference source is insufficient.
                throw;
            }
        }

        protected void Abort()
        {
            foreach (var notificationParticipant in this.notificationParticipants)
                notificationParticipant.Abort();
        }
    }
}
