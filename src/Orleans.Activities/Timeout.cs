using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.ComponentModel;
using System.Drawing;
using Orleans.Activities.Extensions;
using Orleans.Activities.Designers;

namespace Orleans.Activities
{
    /// <summary>
    /// Compared to Delay activity that uses TimeSpan for duration, Timeout uses DateTime.
    /// </summary>
    [Designer(typeof(TimeoutDesigner))]
    [ToolboxBitmap(typeof(Timeout), nameof(Timeout) + ".png")]
    [Description("Compared to Delay activity that uses TimeSpan for duration, Timeout uses DateTime.")]
    public class Timeout : TimeoutBase
    {
        [RequiredArgument]
        [Category(Constants.RequiredCategoryName)]
        [Description("Timeout will expire at this time. If this is nearer than 1 minute, 1 minute delay will be used due to Reminder limitations.")]
        public InArgument<DateTime> Expire { get; set; }

        [Category(Constants.OptionalCategoryName)]
        [Description("This value is added to the delay time expressed by Expire argument to handle the time inaccuracy in different systems. If not set, the DefaultTimeoutDelay parameter will be used.")]
        public InArgument<TimeSpan?> Delay { get; set; }

        protected override TimeSpan CalculateDelay(NativeActivityContext context)
            => this.Expire.Get(context) + (this.Delay.Get(context) ?? context.GetActivityContext().Parameters.DefaultTimeoutDelay) - DateTime.UtcNow;
    }
}
