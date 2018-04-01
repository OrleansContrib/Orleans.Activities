using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Statements;
using System.ComponentModel;
using System.Drawing;
using Orleans.Activities.Designers;
using Orleans.Activities.Extensions;
using Orleans.Activities.Helpers;
using Orleans.Activities.Tracking;

namespace Orleans.Activities
{
    // Used by ReceiveRequestSendResponseScope.
    public interface ISendResponse
    {
        Type ResponseParameterType { get; }
        Func<ReceiveRequestSendResponseScopeExecutionProperty> CreateReceiveRequestSendResponseScopeExecutionPropertyFactory();
    }

    /// <summary>
    /// Completes an incoming request.
    /// </summary>
    [Designer(typeof(SendResponseDesigner))]
    [ToolboxBitmap(typeof(SendResponse), nameof(SendResponse) + ".png")]
    [Description("Completes an incoming request.")]
    public sealed class SendResponse : NativeActivity, ISendResponse
    {
        // Called by ReceiveRequestSendResponseScope, to select the appropriate OperationNames for the ReceiveRequest activity.
        Type ISendResponse.ResponseParameterType => typeof(void);

        // Called by ReceiveRequestSendResponseScope, to create the ReceiveRequestSendResponseScopeExecutionPropertyFactory with the proper TResponseParameter type.
        // Later ReceiveRequest will set the TaskCompletionSource in it to send back the result or let scope propagate unhandled exceptions.
        Func<ReceiveRequestSendResponseScopeExecutionProperty> ISendResponse.CreateReceiveRequestSendResponseScopeExecutionPropertyFactory()
            => () => new ReceiveRequestSendResponseScopeExecutionProperty<object>(this.Idempotent);

        [Category(Constants.RequiredCategoryName)]
        [Description("The fact, that the response is already sent, will be persisted. If the client repeats the operation later, it will receive a OperationRepeatedException.")]
        public bool Idempotent { get; set; }

        [Category(Constants.RequiredCategoryName)]
        [Description("If the workflow is reloaded and there is no TaskCompletionSource to set the result on the operation, SendResponse will throw InvalidOperationException.")]
        public bool ThrowIfReloaded { get; set; }

        private Activity persist;

        public SendResponse()
        {
            this.persist = new Persist();
            this.Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            this.Constraints.Add(OperationActivityHelper.VerifyParentIsReceiveRequestSendResponseScope());
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationChild(this.persist);
            base.CacheMetadata(metadata);
        }

        // We must persist before sending the response if we are idempotent, but this will cause double persist if the workflow goes idle after the response is sent.
        // TODO: can we do anything against this???
        protected override void Execute(NativeActivityContext context)
        {
            var executionProperty = context.GetReceiveRequestSendResponseScopeExecutionProperty<object>();
            executionProperty.AssertIsInitialized();

            if (this.Idempotent)
            {
                context.GetPreviousResponseParameterExtension().SetResponseParameter(
                    executionProperty.OperationName, typeof(void), null);
                context.ScheduleActivity(this.persist, this.PersistCompletionCallback);
            }
            else
                SetTaskCompletionSourceResult(executionProperty, context);
        }

        private void PersistCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance)
        {
            if (completedInstance.State == ActivityInstanceState.Closed)
                SetTaskCompletionSourceResult(context.GetReceiveRequestSendResponseScopeExecutionProperty<object>(), context);
        }

        private void SetTaskCompletionSourceResult(ReceiveRequestSendResponseScopeExecutionProperty<object> executionProperty, NativeActivityContext context)
        {
            executionProperty.SetTaskCompletionSourceResult(null, this.ThrowIfReloaded);

            if (context.GetActivityContext().TrackingEnabled)
                context.Track(new SendResponseRecord());
        }
    }

    /// <summary>
    /// Completes an incoming request and sends back a result of the operation.
    /// </summary>
    /// <typeparam name="TResponseParameter"></typeparam>
    [Designer(typeof(SendResponseGenericDesigner))]
    [ToolboxBitmap(typeof(SendResponse<>), nameof(SendResponse) + ".png")]
    [Description("Completes an incoming request and sends back a result of the operation.")]
    public sealed class SendResponse<TResponseParameter> : NativeActivity, ISendResponse
    {
        // Called by ReceiveRequestSendResponseScope, to select the appropriate OperationNames for the ReceiveRequest activity.
        Type ISendResponse.ResponseParameterType => typeof(TResponseParameter);

        // Called by ReceiveRequestSendResponseScope, to create the ReceiveRequestSendResponseScopeExecutionPropertyFactory with the proper TResponseParameter type.
        // Later ReceiveRequest will set the TaskCompletionSource in it to send back the result or let scope propagate unhandled exceptions.
        Func<ReceiveRequestSendResponseScopeExecutionProperty> ISendResponse.CreateReceiveRequestSendResponseScopeExecutionPropertyFactory()
            => () => new ReceiveRequestSendResponseScopeExecutionProperty<TResponseParameter>(this.Idempotent);

        [RequiredArgument]
        [Category(Constants.RequiredCategoryName)]
        [Description("ResponseParameter has to be serializable if the operation is idempotent.")]
        public InArgument<TResponseParameter> ResponseParameter { get; set; }

        [Category(Constants.RequiredCategoryName)]
        [Description("The fact, that the response is already sent, will be persisted together with the ResponseParameter. If the client repeats the operation later, it will receive a OperationRepeatedException with the previous ResponseParameter in it.")]
        public bool Idempotent { get; set; }

        [Category(Constants.RequiredCategoryName)]
        [Description("If the workflow is reloaded and there is no TaskCompletionSource to set the result on the operation, SendResponse will throw InvalidOperationException.")]
        public bool ThrowIfReloaded { get; set; }

        private Activity persist;

        public SendResponse()
        {
            this.persist = new Persist();
            this.Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            this.Constraints.Add(OperationActivityHelper.VerifyParentIsReceiveRequestSendResponseScope());
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationChild(this.persist);
            base.CacheMetadata(metadata);
        }

        // We must persist before sending the response if we are idempotent, but this will cause double persist if the workflow goes idle after the response is sent.
        // TODO: can we do anything against this???
        protected override void Execute(NativeActivityContext context)
        {
            var executionProperty = context.GetReceiveRequestSendResponseScopeExecutionProperty<TResponseParameter>();
            executionProperty.AssertIsInitialized();

            var responseParameter = this.ResponseParameter.Get(context);
            if (this.Idempotent)
            {
                context.GetPreviousResponseParameterExtension().SetResponseParameter(
                    executionProperty.OperationName, typeof(TResponseParameter), responseParameter);
                context.ScheduleActivity(this.persist, this.PersistCompletionCallback);
            }
            else
                SetTaskCompletionSourceResult(executionProperty, responseParameter, context);
        }

        private void PersistCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance)
        {
            if (completedInstance.State == ActivityInstanceState.Closed)
                SetTaskCompletionSourceResult(context.GetReceiveRequestSendResponseScopeExecutionProperty<TResponseParameter>(), this.ResponseParameter.Get(context), context);
        }

        private void SetTaskCompletionSourceResult(ReceiveRequestSendResponseScopeExecutionProperty<TResponseParameter> executionProperty, TResponseParameter responseParameter, NativeActivityContext context)
        {
            executionProperty.SetTaskCompletionSourceResult(responseParameter, this.ThrowIfReloaded);

            if (context.GetActivityContext().TrackingEnabled)
                context.Track(new SendResponseRecord(responseParameter));
        }
    }
}
