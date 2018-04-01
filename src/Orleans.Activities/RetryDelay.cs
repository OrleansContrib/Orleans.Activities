﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.ComponentModel;
using System.Drawing;
using Orleans.Activities.Configuration;
using Orleans.Activities.Extensions;
using Orleans.Activities.Designers;

namespace Orleans.Activities
{
    /// <summary>
    /// On each repetition increases the value of the delay up to a max. limit. It's practical for a degressive repetition.
    /// </summary>
    [Designer(typeof(RetryDelayDesigner))]
    [ToolboxBitmap(typeof(RetryDelay), nameof(RetryDelay) + ".png")]
    [Description("On each repetition increases the value of the delay up to a max. limit. It's practical for a degressive repetition.")]
    public class RetryDelay : TimeoutBase
    {
        [RequiredArgument]
        [Category(Constants.RequiredCategoryName)]
        [Description("Set to an uninitialized variable in the including scope. This variable stores the continuously increasing retry delay value.")]
        public InOutArgument<TimeSpan> DelayVariable { get; set; }

        [Category(Constants.OptionalCategoryName)]
        [Description("The retry delay starts from this value. If this is less than 1 minute, 1 minute delay will be used due to Reminder limitations. If not set, the DefaultRetryDelayStartValue parameter will be used.")]
        public InArgument<TimeSpan?> DelayStartValue { get; set; }

        [Category(Constants.OptionalCategoryName)]
        [Description("The retry delay time multiplicated with this value on each iteration. If not set, the DefaultRetryDelayDelayMultiplicator parameter will be used.")]
        public InArgument<float?> DelayMultiplicator { get; set; }

        [Category(Constants.OptionalCategoryName)]
        [Description("The maximum of the retry delay after several iterations. If this is less than 1 minute, 1 minute delay will be used due to Reminder limitations. If not set, the DefaultRetryDelayMaxValue parameter will be used.")]
        public InArgument<TimeSpan?> DelayMaxValue { get; set; }

        protected override TimeSpan CalculateDelay(NativeActivityContext context)
        {
            var parameters = context.GetActivityContext().Parameters;

            var delay = this.DelayVariable.Get(context);
            if (delay == default)
                delay = this.DelayStartValue.Get(context) ?? parameters.DefaultRetryDelayStartValue;
            else
            {
                var delayMaxValue = this.DelayMaxValue.Get(context) ?? parameters.DefaultRetryDelayMaxValue;
                if (delay < delayMaxValue)
                {
                    delay = TimeSpan.FromTicks((long)(delay.Ticks * (this.DelayMultiplicator.Get(context) ?? parameters.DefaultRetryDelayDelayMultiplicator)));
                    if (delay > delayMaxValue)
                        delay = delayMaxValue;
                }
            }
            this.DelayVariable.Set(context, delay);

            return delay;
        }
    }
}
