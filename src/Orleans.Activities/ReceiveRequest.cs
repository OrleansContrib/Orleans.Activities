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
using Orleans.Activities.Tracking;

namespace Orleans.Activities
{
    public static class ReceiveRequestExtensions
    {
        // Reads out the parameters of the incoming operation, ie. the bookmark. WorkflowHost sends a TaskCompletionSource and a Func<Task> or Func<Task<>> in an array.
        public static void GetOperationParameters<TTask>(object value, out object taskCompletionSource, out Func<TTask> requestResultTaskFunc)
        {
            object[] parameters = value as object[];
            if (parameters == null || parameters.Length != 2 || parameters[0] == null || parameters[1] == null)
                throw new ArgumentException("ReceiveRequest has invalid parameters.");
            if (!parameters[0].GetType().IsGenericTypeOf(typeof(TaskCompletionSource<>)))
                throw new ArgumentException($"ReceiveRequest's taskCompletionSource is '{parameters[0].GetType().GetFriendlyName()}' and not '{typeof(TaskCompletionSource<>).GetFriendlyName()}'.");
            if (!(parameters[1] is Func<TTask>))
                throw new ArgumentException($"ReceiveRequest's RequestResult type is '{parameters[1].GetType().GetFriendlyName()}' and not '{typeof(Func<TTask>).GetFriendlyName()}', use the proper ReceiveRequest or ReceiveRequest<> activity.");

            taskCompletionSource = parameters[0];
            requestResultTaskFunc = parameters[1] as Func<TTask>;
        }
    }

    // Used by ReceiveRequestSendResponseScope.
    public interface IReceiveRequest
    {
        Type RequestResultType { get; }
    }

    /// <summary>
    /// Receives an incoming request by executing the request result delegate created by the appropriate TWorkflowInterface operation.
    /// The receiving delegate is only executed if this activity is able to accept the incoming request.
    /// </summary>
    [Designer(typeof(ReceiveRequestDesigner))]
    [ToolboxBitmap(typeof(ReceiveRequest), nameof(ReceiveRequest) + ".png")]
    [Description("Receives an incoming request by executing the request result delegate created by the appropriate TWorkflowInterface operation. " +
        "The receiving delegate is only executed if this activity is able to accept the incoming request.")]
    public sealed class ReceiveRequest : NativeActivity, IOperationActivity, IReceiveRequest
    {
        // TODO add combobox to the properties window also
        [Category(Constants.RequiredCategoryName)]
        public string OperationName { get; set; }

        // TODO can we use Attached Properties instead of non-serialized "design time" properties???
        // Set by validation constraints, used be the designer. This is a design time only property.
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ObservableCollection<string> OperationNames { get; }

        // Called by ReceiveRequestSendResponseScope, to select the appropriate OperationNames for the ReceiveRequest activity.
        Type IReceiveRequest.RequestResultType => typeof(void);

        private ActivityAction<Func<Task>> requestResultEvaluator;

        protected override bool CanInduceIdle => true;

        public ReceiveRequest()
        {
            OperationNames = new ObservableCollection<string>();
            requestResultEvaluator = TaskFuncEvaluator.CreateActivityDelegate();
            Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            Constraints.Add(OperationActivityHelper.VerifyParentIsReceiveRequestSendResponseScope());
            Constraints.Add(OperationActivityHelper.VerifyIsOperationNameSetAndValid());
        }
        
        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationDelegate(requestResultEvaluator);
            base.CacheMetadata(metadata);
        }

        protected override void Execute(NativeActivityContext context)
        {
            context.GetReceiveRequestSendResponseScopeExecutionProperty().Initialize(OperationName);
            context.CreateBookmark(OperationName, BookmarkResumptionCallback);
        }
        
        private void BookmarkResumptionCallback(NativeActivityContext context, Bookmark bookmark, object value)
        {
            object taskCompletionSource;
            Func<Task> requestResultTaskFunc;
            ReceiveRequestExtensions.GetOperationParameters(value, out taskCompletionSource, out requestResultTaskFunc);

            // Initializes the execution property held by the scope. SendResponse or the scope will use it (the scope for propagating any exception).
            context.GetReceiveRequestSendResponseScopeExecutionProperty().Initialize(taskCompletionSource);
            // Schedules the receiving delegate.
            context.ScheduleAction(requestResultEvaluator, requestResultTaskFunc, EvaluatorCompletionCallback);
        }

        private void EvaluatorCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance)
        {
            // The receiving delegate completed.
            if (completedInstance.State == ActivityInstanceState.Closed)
                if (context.GetActivityContext().TrackingEnabled)
                    context.Track(new ReceiveRequestRecord(OperationName));
        }
    }

    /// <summary>
    /// Receives an incoming request by executing the request result delegate created by the appropriate TWorkflowInterface operation, and sets the RequestResult of the execution.
    /// The receiving delegate is only executed if this activity is able to accept the incoming request.
    /// </summary>
    /// <typeparam name="TRequestResult"></typeparam>
    [Designer(typeof(ReceiveRequestGenericDesigner))]
    [ToolboxBitmap(typeof(ReceiveRequest<>), nameof(ReceiveRequest) + ".png")]
    [Description("Receives an incoming request by executing the request result delegate created by the appropriate TWorkflowInterface operation, and sets the RequestResult of the execution. " +
        "The receiving delegate is only executed if this activity is able to accept the incoming request.")]
    public sealed class ReceiveRequest<TRequestResult> : NativeActivity, IOperationActivity, IReceiveRequest
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
        [Description("The result of the Func<Task<TRequestResult>> delegate of the incoming TWorkflowInterface operation will be stored here.")]
        public OutArgument<TRequestResult> RequestResult { get; set; }

        // Called by ReceiveRequestSendResponseScope, to select the appropriate OperationNames for the ReceiveRequest activity.
        Type IReceiveRequest.RequestResultType => typeof(TRequestResult);

        private ActivityFunc<Func<Task<TRequestResult>>, TRequestResult> requestResultEvaluator;

        protected override bool CanInduceIdle => true;

        public ReceiveRequest()
        {
            OperationNames = new ObservableCollection<string>();
            requestResultEvaluator = TaskFuncEvaluator<TRequestResult>.CreateActivityDelegate();
            Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            Constraints.Add(OperationActivityHelper.VerifyParentIsReceiveRequestSendResponseScope());
            Constraints.Add(OperationActivityHelper.VerifyIsOperationNameSetAndValid());
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationDelegate(requestResultEvaluator);
            base.CacheMetadata(metadata);
        }

        protected override void Execute(NativeActivityContext context)
        {
            context.GetReceiveRequestSendResponseScopeExecutionProperty().Initialize(OperationName);
            context.CreateBookmark(OperationName, BookmarkResumptionCallback);
        }

        private void BookmarkResumptionCallback(NativeActivityContext context, Bookmark bookmark, object value)
        {
            object taskCompletionSource;
            Func<Task<TRequestResult>> requestResultTaskFunc;
            ReceiveRequestExtensions.GetOperationParameters(value, out taskCompletionSource, out requestResultTaskFunc);

            // Initializes the execution property held by the scope. SendResponse or the scope will use it (the scope for propagating any exception).
            context.GetReceiveRequestSendResponseScopeExecutionProperty().Initialize(taskCompletionSource);
            // Schedules the receiving delegate.
            context.ScheduleFunc(requestResultEvaluator, requestResultTaskFunc, EvaluatorCompletionCallback);
        }

        private void EvaluatorCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance, TRequestResult result)
        {
            // The receiving delegate completed.
            if (completedInstance.State == ActivityInstanceState.Closed)
            {
                // Sets the result of the incoming request's processing.
                RequestResult.Set(context, result);

                if (context.GetActivityContext().TrackingEnabled)
                    context.Track(new ReceiveRequestRecord(OperationName, result));
            }
        }
    }
}
