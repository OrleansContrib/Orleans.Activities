using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.ComponentModel;
using System.Drawing;
using Orleans.Activities.Designers;
using Orleans.Activities.Designers.Binding;
using Orleans.Activities.Extensions;
using Orleans.Activities.Helpers;
using Orleans.Activities.Hosting;
using Orleans.Activities.Tracking;

namespace Orleans.Activities
{
    // Used by ReceiveRequestSendResponseScope.
    public interface ISendRequest
    {
        Type RequestParameterType { get; }
    }

    /// <summary>
    /// Sends an outgoing request by calling the appropriate TWorkflowCallbackInterface operation.
    /// </summary>
    [Designer(typeof(SendRequestDesigner))]
    [ToolboxBitmap(typeof(SendRequest), nameof(SendRequest) + ".png")]
    [Description("Sends an outgoing request by calling the appropriate TWorkflowCallbackInterface operation.")]
    public sealed class SendRequest : NativeActivity, IOperationActivity, ISendRequest
    {
        // TODO add combobox to the properties window also
        [Category(Constants.RequiredCategoryName)]
        public string OperationName { get; set; }

        // TODO can we use Attached Properties instead of non-serialized "design time" properties???
        // Set by validation constraints, used be the designer. This is a design time only property.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ObservableCollection<string> OperationNames { get; }

        // Called by SendRequestReceiveResponseScope, to select the appropriate OperationNames for the SendRequest activity.
        Type ISendRequest.RequestParameterType => typeof(void);

        public SendRequest()
        {
            OperationNames = new ObservableCollection<string>();
            Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            Constraints.Add(OperationActivityHelper.VerifyParentIsSendRequestReceiveResponseScope());
            Constraints.Add(OperationActivityHelper.VerifyIsOperationNameSetAndValid());
        }

        // This will start/schedule the OnOperationAsync task, but won't wait for it, the task will be an implicit (single threaded reentrant) parallel activity.
        // The Scope is responsible to handle the outstanding task in case of Abort, Cancellation or Termination.
        protected override void Execute(NativeActivityContext context)
        {
            SendRequestReceiveResponseScopeExecutionProperty executionProperty = context.GetSendRequestReceiveResponseScopeExecutionProperty();
            IActivityContext activityContext = context.GetActivityContext();
            executionProperty.StartOnOperationAsync(activityContext, OperationName);

            if (activityContext.TrackingEnabled)
                context.Track(new SendRequestRecord(OperationName));
        }
    }

    /// <summary>
    /// Sends an outgoing request by calling the appropriate TWorkflowCallbackInterface operation with RequestParameter.
    /// </summary>
    /// <typeparam name="TRequestParameter"></typeparam>
    [Designer(typeof(SendRequestGenericDesigner))]
    [ToolboxBitmap(typeof(SendRequest<>), nameof(SendRequest) + ".png")]
    [Description("Sends an outgoing request by calling the appropriate TWorkflowCallbackInterface operation with RequestParameter.")]
    public sealed class SendRequest<TRequestParameter> : NativeActivity, IOperationActivity, ISendRequest
        where TRequestParameter : class
    {
        // TODO add combobox to the properties window also
        [Category(Constants.RequiredCategoryName)]
        public string OperationName { get; set; }

        // TODO can we use Attached Properties instead of non-serialized "design time" properties???
        // Set by validation constraints, used be the designer. This is a design time only property.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ObservableCollection<string> OperationNames { get; }

        [RequiredArgument]
        [Category(Constants.RequiredCategoryName)]
        [Description("The parameter of the outgoing TWorkflowCallbackInterface operation.")]
        public InArgument<TRequestParameter> RequestParameter { get; set; }

        // Called by SendRequestReceiveResponseScope, to select the appropriate OperationNames for the SendRequest activity.
        Type ISendRequest.RequestParameterType => typeof(TRequestParameter);

        public SendRequest()
        {
            OperationNames = new ObservableCollection<string>();
            Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            Constraints.Add(OperationActivityHelper.VerifyParentIsSendRequestReceiveResponseScope());
            Constraints.Add(OperationActivityHelper.VerifyIsOperationNameSetAndValid());
        }

        // This will start/schedule the OnOperationAsync task, but won't wait for it, the task will be an implicit (single threaded reentrant) parallel activity.
        // The Scope is responsible to handle the outstanding task in case of Abort, Cancellation or Termination.
        protected override void Execute(NativeActivityContext context)
        {
            SendRequestReceiveResponseScopeExecutionProperty executionProperty = context.GetSendRequestReceiveResponseScopeExecutionProperty();
            IActivityContext activityContext = context.GetActivityContext();
            TRequestParameter requestParameter = RequestParameter.Get(context);
            executionProperty.StartOnOperationAsync(activityContext, OperationName, requestParameter);

            if (activityContext.TrackingEnabled)
                context.Track(new SendRequestRecord(OperationName, requestParameter));
        }
    }
}
