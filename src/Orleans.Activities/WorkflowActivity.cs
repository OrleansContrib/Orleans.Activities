﻿using System;
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
    /// The de facto "root" activity to get the TWorkflowInterface and TWorkflowCallbackInterface types.
    /// <para>Due to the limitations of the designer, the base class of the XAML designed activity, ie. the workflow must be Activity.
    /// If we use the general ReceiveRequest, SendResponse, SendRequest, ReceiveResponse activities, a WorkflowActivity must be the top activity under the workflow's "root" activity.
    /// See <see cref="WorkflowGrain{TGrain, TGrainState, TWorkflowInterface, TWorkflowCallbackInterface}"/> for the description of the type parameters.</para>  
    /// </summary>
    /// <typeparam name="TWorkflowInterface"></typeparam>
    /// <typeparam name="TWorkflowCallbackInterface"></typeparam>
#pragma warning disable IDE0009 // Member access should be qualified.
    [ContentProperty(nameof(Body))]
#pragma warning restore IDE0009 // Member access should be qualified.
    [Designer(typeof(WorkflowActivityDesigner))]
    [ToolboxBitmap(typeof(WorkflowActivity<,>), nameof(WorkflowActivity<TWorkflowInterface, TWorkflowCallbackInterface>) + ".png")]
    [Description("The de facto \"root\" activity to get the TWorkflowInterface and TWorkflowCallbackInterface types.\n" +
        "Due to the limitations of the designer, the base class of the XAML designed activity, ie. the workflow must be Activity. " +
        "If we use the general ReceiveRequest, SendResponse, SendRequest, ReceiveResponse activities, a WorkflowActivity must be the top activity under the workflow's \"root\" activity.\nSee WorkflowGrain<TGrainState, TWorkflowInterface, TWorkflowCallbackInterface> for the description of the type parameters.")]
    public sealed class WorkflowActivity<TWorkflowInterface, TWorkflowCallbackInterface> : NativeActivity, IWorkflowActivity<TWorkflowInterface, TWorkflowCallbackInterface>
        where TWorkflowInterface : class
        where TWorkflowCallbackInterface : class
    {
        [DefaultValue(null)]
        [Browsable(false)]
        public Activity Body { get; set; }

        public WorkflowActivity()
        {
            this.Constraints.Add(WorkflowActivityHelper.VerifyWorkflowInterface<TWorkflowInterface>());
            this.Constraints.Add(WorkflowActivityHelper.VerifyWorkflowCallbackInterface<TWorkflowCallbackInterface>());
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddChild(this.Body);
            base.CacheMetadata(metadata);
        }

        protected override void Execute(NativeActivityContext context)
        {
            if (this.Body != null)
                context.ScheduleActivity(this.Body);
        }
    }
}
