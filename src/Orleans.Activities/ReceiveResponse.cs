using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.ComponentModel;
using System.Drawing;
using Orleans.Activities.Extensions;
using Orleans.Activities.Helpers;
using Orleans.Activities.Designers;
using Orleans.Activities.Tracking;

namespace Orleans.Activities
{
    // Called by SendRequestReceiveResponseScope, to create the SendRequestReceiveResponseScopeExecutionPropertyFactory with the proper TResponseResult type.
    public interface IReceiveResponse
    {
        Type ResponseResultType { get; }
        Func<SendRequestReceiveResponseScopeExecutionProperty> CreateSendRequestReceiveResponseScopeExecutionPropertyFactory();
    }

    /// <summary>
    /// Completes an outgoing request.
    /// The request's result delegate is only executed if this activity is able to complete the outgoing request.
    /// </summary>
    [Designer(typeof(ReceiveResponseDesigner))]
    [ToolboxBitmap(typeof(ReceiveResponse), nameof(ReceiveResponse) + ".png")]
    [Description("Completes an outgoing request. " +
        "The request's result delegate is only executed if this activity is able to complete the outgoing request.")]
    public sealed class ReceiveResponse : NativeActivity, IReceiveResponse
    {
        // Called by SendRequestReceiveResponseScope, to select the appropriate OperationNames for the SendRequest activity.
        Type IReceiveResponse.ResponseResultType => typeof(void);

        // Called by SendRequestReceiveResponseScope, to create the SendRequestReceiveResponseScopeExecutionPropertyFactory with the proper TResponseResult type.
        // Later SendRequest will set the Task in it to receive the result or let scope consume and propagate unhandled exceptions.
        Func<SendRequestReceiveResponseScopeExecutionProperty> IReceiveResponse.CreateSendRequestReceiveResponseScopeExecutionPropertyFactory()
            => () => new SendRequestReceiveResponseScopeExecutionPropertyWithoutResult();

        private ActivityFunc<Task<Func<Task>>, Func<Task>> responseResultWaiter;
        private ActivityAction<Func<Task>> responseResultEvaluator;

        protected override bool CanInduceIdle => true;

        public ReceiveResponse()
        {
            this.responseResultWaiter = TaskFuncTaskWaiter.CreateActivityDelegate();
            this.responseResultEvaluator = TaskFuncEvaluator.CreateActivityDelegate();
            this.Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            this.Constraints.Add(OperationActivityHelper.VerifyParentIsSendRequestReceiveResponseScope());
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationDelegate(this.responseResultWaiter);
            metadata.AddImplementationDelegate(this.responseResultEvaluator);
            base.CacheMetadata(metadata);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var executionProperty =
                context.GetSendRequestReceiveResponseScopeExecutionPropertyWithoutResult();
            executionProperty.AssertIsStarted();

            // Schedules an awaiter for the outgoing request, ie. the appropriate TWorkflowCallbackInterface operation.
            context.ScheduleFunc(this.responseResultWaiter, executionProperty.OnOperationTask, this.WaiterCompletionCallback);
            executionProperty.OnOperationTaskWaiterIsScheduled();
        }

        private void WaiterCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance, Func<Task> result)
        {
            // When the outgoing request is completed, schedules the request's result delegate.
            if (completedInstance.State == ActivityInstanceState.Closed)
                context.ScheduleAction(this.responseResultEvaluator, result, this.EvaluatorCompletionCallback);
        }
    
        private void EvaluatorCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance)
        {
            // The request's result delegate completed.
            if (completedInstance.State == ActivityInstanceState.Closed)
                if (context.GetActivityContext().TrackingEnabled)
                    context.Track(new ReceiveResponseRecord());
        }
    }

    /// <summary>
    /// Completes an outgoing request, and sets the ResponseResult of the execution.
    /// The request's result delegate is only executed if this activity is able to complete the outgoing request.
    /// </summary>
    /// <typeparam name="TResponseResult"></typeparam>
    [Designer(typeof(ReceiveResponseGenericDesigner))]
    [ToolboxBitmap(typeof(ReceiveResponse<>), nameof(ReceiveResponse) + ".png")]
    [Description("Completes an outgoing request, and sets the ResponseResult of the execution. " +
        "The request's result delegate is only executed if this activity is able to complete the outgoing request.")]
    public sealed class ReceiveResponse<TResponseResult> : NativeActivity, IReceiveResponse
    {
        // Called by SendRequestReceiveResponseScope, to select the appropriate OperationNames for the SendRequest activity.
        Type IReceiveResponse.ResponseResultType => typeof(TResponseResult);

        // Called by SendRequestReceiveResponseScope, to create the SendRequestReceiveResponseScopeExecutionPropertyFactory with the proper TResponseResult type.
        // Later SendRequest will set the Task in it to receive the result or let scope consume and propagate unhandled exceptions.
        Func<SendRequestReceiveResponseScopeExecutionProperty> IReceiveResponse.CreateSendRequestReceiveResponseScopeExecutionPropertyFactory()
            => () => new SendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult>();

        [RequiredArgument]
        [Category(Constants.RequiredCategoryName)]
        [Description("The result of the Func<Task<TResponseResult>> delegate of the outgoing TWorkflowCallbackInterface operation will be stored here.")]
        public OutArgument<TResponseResult> ResponseResult { get; set; }

        private ActivityFunc<Task<Func<Task<TResponseResult>>>, Func<Task<TResponseResult>>> responseResultWaiter;
        private ActivityFunc<Func<Task<TResponseResult>>, TResponseResult> responseResultEvaluator;

        protected override bool CanInduceIdle => true;

        public ReceiveResponse()
        {
            this.responseResultWaiter = TaskFuncTaskWaiter<TResponseResult>.CreateActivityDelegate();
            this.responseResultEvaluator = TaskFuncEvaluator<TResponseResult>.CreateActivityDelegate();
            this.Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            this.Constraints.Add(OperationActivityHelper.VerifyParentIsSendRequestReceiveResponseScope());
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationDelegate(this.responseResultWaiter);
            metadata.AddImplementationDelegate(this.responseResultEvaluator);
            base.CacheMetadata(metadata);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var executionProperty = context.GetSendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult>();
            executionProperty.AssertIsStarted();

            // Schedules an awaiter for the outgoing request, ie. the appropriate TWorkflowCallbackInterface operation.
            context.ScheduleFunc(this.responseResultWaiter, executionProperty.OnOperationTask, this.WaiterCompletionCallback);
            executionProperty.OnOperationTaskWaiterIsScheduled();
        }

        private void WaiterCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance, Func<Task<TResponseResult>> result)
        {
            // When the outgoing request is completed, schedules the request's result delegate.
            if (completedInstance.State == ActivityInstanceState.Closed)
                context.ScheduleFunc(this.responseResultEvaluator, result, this.EvaluatorCompletionCallback);
        }

        private void EvaluatorCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance, TResponseResult result)
        {
            // The request's result delegate completed.
            if (completedInstance.State == ActivityInstanceState.Closed)
            {
                // Sets the result of the outgoing request's processing.
                this.ResponseResult.Set(context, result);

                if (context.GetActivityContext().TrackingEnabled)
                    context.Track(new ReceiveResponseRecord(result));
            }
        }
    }
}
