using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Orleans.Activities.AsyncEx;

namespace Orleans.Activities.Notification
{
    /// <summary>
    /// Let extensions to get notified about workflow instance events.
    /// <para>These events are async, extensions can batch asnyc operations for these events, operations that are requested by sync activities.</para>
    /// </summary>
    public interface INotificationParticipant
    {
        Task OnPausedAsync(TimeSpan timeout);

        void Abort();
    }

    /// <summary>
    /// You don't need to use the abstract class, you can inherit from the interface also.
    /// </summary>
    public abstract class PersistenceIOParticipant : INotificationParticipant
    {
        public virtual Task OnPausedAsync(TimeSpan timeout) => TaskConstants.Completed;

        public virtual void Abort() { }
    }
}
