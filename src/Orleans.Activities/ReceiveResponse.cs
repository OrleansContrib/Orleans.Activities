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
    public static class ReceiveResponseExtensions
    {
        public static bool IsReceiveResponse(this Activity activity)
        {
            Type type = activity.GetType();
            return type == typeof(ReceiveResponse) || type.IsGenericTypeOf(typeof(ReceiveResponse<>));
        }
    }

    // Called by SendRequestReceiveResponseScope, to create the SendRequestReceiveResponseScopeExecutionPropertyFactory with the proper TResponseResult type.
    public interface IReceiveResponse
    {
        Func<SendRequestReceiveResponseScopeExecutionProperty> CreateSendRequestReceiveResponseScopeExecutionPropertyFactory();
    }

    /// <summary>
    /// Completes an outgoing request.
    /// The request's result delegate is only executed if this activity is able to complete the outgoing request.
    /// </summary>
    [Designer(typeof(ReceiveResponseDesigner))]
    [ToolboxBitmap(typeof(ReceiveResponse), nameof(ReceiveResponse) + ".png")]
    public sealed class ReceiveResponse : NativeActivity, IReceiveResponse
    {
        private ActivityFunc<Task<Func<Task>>, Func<Task>> responseResultWaiter;
        private ActivityAction<Func<Task>> responseResultEvaluator;

        protected override bool CanInduceIdle => true;

        public ReceiveResponse()
        {
            responseResultWaiter = TaskFuncTaskWaiter.CreateActivityDelegate();
            responseResultEvaluator = TaskFuncEvaluator.CreateActivityDelegate();
            Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            Constraints.Add(SendRequestReceiveResponseScopeHelper.VerifyParentIsSendRequestReceiveResponseScope());
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationDelegate(responseResultWaiter);
            metadata.AddImplementationDelegate(responseResultEvaluator);
            base.CacheMetadata(metadata);
        }

        // Called by SendRequestReceiveResponseScope, to create the SendRequestReceiveResponseScopeExecutionPropertyFactory with the proper TResponseResult type.
        // Later SendRequest will set the Task in it to receive the result or let scope consume and propagate unhandled exceptions.
        Func<SendRequestReceiveResponseScopeExecutionProperty> IReceiveResponse.CreateSendRequestReceiveResponseScopeExecutionPropertyFactory() =>
            () => new SendRequestReceiveResponseScopeExecutionPropertyWithoutResult();

        protected override void Execute(NativeActivityContext context)
        {
            SendRequestReceiveResponseScopeExecutionPropertyWithoutResult executionProperty =
                context.GetSendRequestReceiveResponseScopeExecutionPropertyWithoutResult();
            executionProperty.AssertIsStarted();

            // Schedules an awaiter for the outgoing request, ie. the appropriate TEffector operation.
            context.ScheduleFunc(responseResultWaiter, executionProperty.OnOperationTask, WaiterCompletionCallback);
            executionProperty.OnOperationTaskWaiterIsScheduled();
        }

        private void WaiterCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance, Func<Task> result)
        {
            // When the outgoing request is completed, schedules the request's result delegate.
            if (completedInstance.State == ActivityInstanceState.Closed)
                context.ScheduleAction(responseResultEvaluator, result, EvaluatorCompletionCallback);
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
    public sealed class ReceiveResponse<TResponseResult> : NativeActivity, IReceiveResponse
        where TResponseResult : class
    {
        [RequiredArgument]
        [Category(Constants.RequiredCategoryName)]
        [Description("The result of the Func<Task<TResponseResult>> delegate of the outgoing TEffector operation will be stored here.")]
        public OutArgument<TResponseResult> ResponseResult { get; set; }

        private ActivityFunc<Task<Func<Task<TResponseResult>>>, Func<Task<TResponseResult>>> responseResultWaiter;
        private ActivityFunc<Func<Task<TResponseResult>>, TResponseResult> responseResultEvaluator;

        protected override bool CanInduceIdle => true;

        public ReceiveResponse()
        {
            responseResultWaiter = TaskFuncTaskWaiter<TResponseResult>.CreateActivityDelegate();
            responseResultEvaluator = TaskFuncEvaluator<TResponseResult>.CreateActivityDelegate();
            Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            Constraints.Add(SendRequestReceiveResponseScopeHelper.VerifyParentIsSendRequestReceiveResponseScope());
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationDelegate(responseResultWaiter);
            metadata.AddImplementationDelegate(responseResultEvaluator);
            base.CacheMetadata(metadata);
        }

        // Called by SendRequestReceiveResponseScope, to create the SendRequestReceiveResponseScopeExecutionPropertyFactory with the proper TResponseResult type.
        // Later SendRequest will set the Task in it to receive the result or let scope consume and propagate unhandled exceptions.
        Func<SendRequestReceiveResponseScopeExecutionProperty> IReceiveResponse.CreateSendRequestReceiveResponseScopeExecutionPropertyFactory() =>
            () => new SendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult>();

        protected override void Execute(NativeActivityContext context)
        {
            SendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult> executionProperty =
                context.GetSendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult>();
            executionProperty.AssertIsStarted();

            // Schedules an awaiter for the outgoing request, ie. the appropriate TEffector operation.
            context.ScheduleFunc(responseResultWaiter, executionProperty.OnOperationTask, WaiterCompletionCallback);
            executionProperty.OnOperationTaskWaiterIsScheduled();
        }

        private void WaiterCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance, Func<Task<TResponseResult>> result)
        {
            // When the outgoing request is completed, schedules the request's result delegate.
            if (completedInstance.State == ActivityInstanceState.Closed)
                context.ScheduleFunc(responseResultEvaluator, result, EvaluatorCompletionCallback);
        }

        private void EvaluatorCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance, TResponseResult result)
        {
            // The request's result delegate completed.
            if (completedInstance.State == ActivityInstanceState.Closed)
            {
                // Sets the result of the outgoing request's processing.
                ResponseResult.Set(context, result);

                if (context.GetActivityContext().TrackingEnabled)
                    context.Track(new ReceiveResponseRecord(result));
            }
        }
    }
}
