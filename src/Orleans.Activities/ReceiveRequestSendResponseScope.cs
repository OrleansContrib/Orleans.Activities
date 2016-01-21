using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Markup;
using Orleans.Activities.Extensions;
using Orleans.Activities.Helpers;
using Orleans.Activities.Designers;

namespace Orleans.Activities
{
    /// <summary>
    /// Scope for ReceiveRequest and SendResponse activities.
    /// <para>This is persistable, worst case we can't send back the response, but we can store it as idempotent response, and the next client call can receive it automatically.
    /// If the client repeats the request during execution of the scope, the incoming request will wait for the execution of the current operation, until the workflow is idle again, even when the grain is reentrant.</para>
    /// </summary>
    [ContentProperty("Body")]
    [Designer(typeof(ReceiveRequestSendResponseScopeDesigner))]
    [ToolboxBitmap(typeof(ReceiveRequestSendResponseScope), nameof(ReceiveRequestSendResponseScope) + ".png")]
    public sealed class ReceiveRequestSendResponseScope : NativeActivity
    {
        // An elaborate setter for the private receiveRequestSendResponseScopeExecutionPropertyFactory field, used by the ReceiveRequestSendResponseScopeHelper's constraints.
        public sealed class ReceiveRequestSendResponseScopeExecutionPropertyFactorySetter : CodeActivity
        {
            public InArgument<ISendResponse> ISendResponse { get; set; }
            
            public InArgument<ReceiveRequestSendResponseScope> ReceiveRequestSendResponseScope { get; set; }

            protected override void Execute(CodeActivityContext context)
            {
                ReceiveRequestSendResponseScope.Get(context).receiveRequestSendResponseScopeExecutionPropertyFactory =
                    ISendResponse.Get(context).CreateReceiveRequestSendResponseScopeExecutionPropertyFactory();
            }
        }

        public const string ExecutionPropertyName = "ReceiveRequestSendResponseScope.ExecutionProperty";

        [DefaultValue(null)]
        [Browsable(false)]
        public Activity Body { get; set; }

        // The execution property's proper type depends on the ResponseParameter's type of the SendResponse activity,
        // we create the factory within a constraint that visits the children of this scope and searches for the ISendResponse to create the factory.
        // The created execution property is persisted with the workflow.
        private Func<ReceiveRequestSendResponseScopeExecutionProperty> receiveRequestSendResponseScopeExecutionPropertyFactory;

        public ReceiveRequestSendResponseScope()
        {
            Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            Constraints.Add(ReceiveRequestSendResponseScopeHelper.VerifyReceiveRequestSendResponseScopeChildren());
            Constraints.Add(ReceiveRequestSendResponseScopeHelper.SetReceiveRequestSendResponseScopeExecutionPropertyFactory());
        }
                
        protected override void Execute(NativeActivityContext context)
        {
            if (Body != null)
            {
                ReceiveRequestSendResponseScopeExecutionProperty executionProperty = receiveRequestSendResponseScopeExecutionPropertyFactory();
                context.Properties.Add(ExecutionPropertyName, executionProperty);
                context.ScheduleActivity(Body, BodyCompletionCallback, BodyFaultCallback);
            }
        }

        private void BodyCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance)
        {
            if (completedInstance.State == ActivityInstanceState.Canceled)
                context.GetReceiveRequestSendResponseScopeExecutionProperty().TrySetTaskCompletionSourceCanceled();
        }

        // TODO: we propagate an unhandled exception during an operation back to the task, should we handle it differently?
        private void BodyFaultCallback(NativeActivityFaultContext context, Exception propagatedException, ActivityInstance propagatedFrom)
        {
            if (!context.GetReceiveRequestSendResponseScopeExecutionProperty().TrySetTaskCompletionSourceException(propagatedException))
                // this will add a WorkflowInstanceAbortedRecord with the reason
                context.Abort(propagatedException);
            else
                // this won't add any WorkflowInstanceAbortedRecord at all
                context.Abort();
            context.HandleFault();
        }
    }
}
