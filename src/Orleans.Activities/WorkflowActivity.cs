using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Markup;
using Orleans.Activities.Designers;
using Orleans.Activities.Helpers;

namespace Orleans.Activities
{
    /// <summary>
    /// The de facto "root" activity to get the TAffector and TEffector types.
    /// <para>Due to the limitations of the designer, the base class of the XAML designed activity, ie. the workflow must be Activity.
    /// If we use the general ReceiveRequest, SendResponse, SendRequest, ReceiveResponse activities, a WorkflowActivity must be the top activity under the workflow's "root" activity.
    /// See <see cref="WorkflowGrain{TWorkflowState, TAffector, TEffector}"/> for the description of the type parameters.</para>  
    /// </summary>
    /// <typeparam name="TAffector"></typeparam>
    /// <typeparam name="TEffector"></typeparam>
    [ContentProperty(nameof(Body))]
    [Designer(typeof(WorkflowActivityDesigner))]
    [ToolboxBitmap(typeof(WorkflowActivity<,>), nameof(WorkflowActivity<TAffector, TEffector>) + ".png")]
    public sealed class WorkflowActivity<TAffector, TEffector> : NativeActivity, IWorkflowActivity<TAffector, TEffector>
        where TAffector : class
        where TEffector : class
    {
        [DefaultValue(null)]
        [Browsable(false)]
        public Activity Body { get; set; }

        public WorkflowActivity()
        {
            Constraints.Add(WorkflowActivityHelper.VerifyAffector<TAffector>());
            Constraints.Add(WorkflowActivityHelper.VerifyEffector<TEffector>());
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddChild(Body);
            base.CacheMetadata(metadata);
        }

        protected override void Execute(NativeActivityContext context)
        {
            if (Body != null)
                context.ScheduleActivity(Body);
        }
    }
}
