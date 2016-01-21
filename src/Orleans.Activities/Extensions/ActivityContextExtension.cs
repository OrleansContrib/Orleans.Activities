using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using Orleans.Activities.Hosting;

namespace Orleans.Activities.Extensions
{
    public static class ActivityContextExtensions
    {
        public static IActivityContext GetActivityContext(this ActivityContext context)
        {
            ActivityContextExtension activityContextExtension = context.GetExtension<ActivityContextExtension>();
            if (activityContextExtension == null)
                throw new ValidationException(nameof(ActivityContextExtension) + " is not found.");
            return activityContextExtension.ActivityContext;
        }
    }

    /// <summary>
    /// This extension is always created by the workflow instance, and this way the IActivityContext functions are always accessible by any activity.
    /// </summary>
    public class ActivityContextExtension
    {
        public IActivityContext ActivityContext { get; }

        public ActivityContextExtension(IActivityContext activityContext)
        {
            ActivityContext = activityContext;
        }
    }
}
