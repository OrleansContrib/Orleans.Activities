using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Statements;
using Orleans.Activities.Extensions;

namespace Orleans.Activities
{
    /// <summary>
    /// Base class for time related activities (eg. RetryDelay and Timeout).
    /// <para>It requires DurableReminderExtension (TimerExtension implementation) to be already created.
    /// Original workflow activities in case of absence create the original DurableTimerExtension, that is incompatible with the Orleans reminder/persistence logic.</para>
    /// </summary>
    public abstract class TimeoutBase : NativeActivity
    {
        private Variable<Bookmark> timerExpiredBookmark = new Variable<Bookmark>();

        /// <summary>
        /// Implement it to calculate the delay for the activity.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected abstract TimeSpan CalculateDelay(NativeActivityContext context);

        protected override bool CanInduceIdle => true;

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationVariable(this.timerExpiredBookmark);
            metadata.RequireExtension<TimerExtension>();
            base.CacheMetadata(metadata);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var delay = CalculateDelay(context);
            if (delay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay is negative.");
            if (delay > TimeSpan.Zero)
            {
                var bookmark = context.CreateBookmark();
                context.GetTimerExtension().RegisterTimer(delay, bookmark);
                this.timerExpiredBookmark.Set(context, bookmark);
            }
        }

        protected override void Cancel(NativeActivityContext context)
        {
            var bookmark = this.timerExpiredBookmark.Get(context);
            if (bookmark != null)
            {
                context.GetTimerExtension().CancelTimer(bookmark);
                context.RemoveBookmark(bookmark);
            }
            context.MarkCanceled();
        }

        protected override void Abort(NativeActivityAbortContext context)
        {
            var bookmark = this.timerExpiredBookmark.Get(context);
            if (bookmark != null)
            {
                context.GetTimerExtension().CancelTimer(bookmark);
            }
            base.Abort(context);
        }
    }
}
