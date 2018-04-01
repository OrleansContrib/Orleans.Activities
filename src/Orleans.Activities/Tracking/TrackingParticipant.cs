using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities.Tracking;
using Orleans.Activities.AsyncEx;

namespace Orleans.Activities.Tracking
{
    /// <summary>
    /// TAP async equivalent of the <see cref="System.Activities.Tracking.TrackingParticipant"/> abstract class.
    /// <para>Tracking participants are used all over the System.Activities code, we have to inherit from the abstract System.Activities.Tracking.TrackingParticipant class,
    /// so there is no interface-only version like in case of persistence or notification.</para>
    /// </summary>
    public abstract class TrackingParticipant : System.Activities.Tracking.TrackingParticipant
    {
        protected TrackingParticipant()
        { }

        protected sealed override IAsyncResult BeginTrack(TrackingRecord record, TimeSpan timeout, AsyncCallback callback, object state)
            => AsyncFactory.ToBegin(TrackAsync(record, timeout), callback, state);

        protected sealed override void EndTrack(IAsyncResult result)
            => AsyncFactory.ToEnd(result);

        protected sealed override void Track(TrackingRecord record, TimeSpan timeout)
            => throw new NotSupportedException("Track is not supported.");

        protected abstract Task TrackAsync(TrackingRecord record, TimeSpan timeout);
    }
}
