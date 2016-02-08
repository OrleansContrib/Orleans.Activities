using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.ComponentModel;
using System.Drawing;
using Orleans.Activities.Designers;
using Orleans.Activities.Extensions;

namespace Orleans.Activities
{
    /// <summary>
    /// Gets the IsReloaded property of the workflow. IsReloaded is true from a Load with Runnable state up to the first Save.
    /// <para>A Load with Runnable state tipically means a Reload after a failure, when previously the workflow was persisted explicitly before a critical step.</para>
    /// </summary>
    [Designer(typeof(GetIsReloadedDesigner))]
    [ToolboxBitmap(typeof(GetIsReloaded), nameof(GetIsReloaded) + ".png")]
    [Description("Gets the IsReloaded property of the workflow. IsReloaded is true from a Load with Runnable state up to the first Save.\n" +
        "A Load with Runnable state tipically means a Reload after a failure, when previously the workflow was persisted explicitly before a critical step.")]
    public sealed class GetIsReloaded : CodeActivity
    {
        [RequiredArgument]
        [Category(Constants.RequiredCategoryName)]
        [Description("It's true, if the workflow is loaded in a Runnable state (tipically after an Abort, caused by an unhandled exception). Remains true until persisted.")]
        public OutArgument<Boolean> IsReloaded { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            IsReloaded.Set(context, context.GetActivityContext().IsReloaded);
        } 
    }
}
