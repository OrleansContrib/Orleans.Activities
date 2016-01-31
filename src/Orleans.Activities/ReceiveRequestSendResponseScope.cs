using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Statements;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.Serialization;
using System.Windows.Markup;
using Orleans.Activities.Designers;
using Orleans.Activities.Extensions;
using Orleans.Activities.Helpers;
using Orleans.Activities.Hosting;

namespace Orleans.Activities
{
    // In summary:
    // - If the body faults, the current operation's taskCompletionSource will be faulted (if there is any),
    //   and the workflow will Abort/Cancel/Teminate itself based on Parameters.UnhandledExceptionAction.
    // - If the body closes or cancels (but not faults), and the operation's taskCompletionSource is not completed yet or there isn't any
    //   - the operation will be stored as canceled in PreviousResponseParameterExtension (if idempotent), and
    //   - if there is a taskCompletionSource, it will be canceled.

    /// <summary>
    /// Scope for ReceiveRequest and SendResponse activities.
    /// <para>This is persistable, worst case we can't send back the response, but we can store it as idempotent response, and the next client call can receive it automatically.
    /// If the client repeats the request during execution of the scope, the incoming request will wait for the execution of the current operation.
    /// If the workflow goes idle during the execution of the operation and the grain is reentrant, the repeated request will get an InvalidOperationException, due to
    /// the workflow doesn't wait for operation already, but didn't produced the response yet.</para>
    /// <para>The main responsibility of this activity, to cancel the operation even if the scope completes without executing the SendResponse activity.
    /// In the workflow, this is possible, to not execute the SendResponse, there is no "not all code paths return a value" error for it.</para>
    /// <para>In case of an exception, the scope catches it and behaves like a root activity, calls OnUnhandledException on the host and Abort/Cancel/Terminate the workflow.
    /// In this case all other open operations' taskCompletionSource will be faulted also.</para>
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

        // Private activity to cancel the operations' taskCompletionSource in case it is not completed yet.
        private class ProtectClosedOrCanceledOperation : NativeActivity
        {
            private Activity persist;

            public ProtectClosedOrCanceledOperation()
            {
                persist = new Persist();
            }

            protected override void CacheMetadata(NativeActivityMetadata metadata)
            {
                metadata.AddImplementationChild(persist);
                base.CacheMetadata(metadata);
            }

            protected override void Execute(NativeActivityContext context)
            {
                ReceiveRequestSendResponseScopeExecutionProperty executionProperty = context.GetReceiveRequestSendResponseScopeExecutionProperty();
                if (!executionProperty.Faulted && !executionProperty.IsInitializedAndCompleted)
                {
                    // The taskCompletionSource from an incoming request is not completed (we don't have it or we have but not completed).
                    if (!executionProperty.IsInitializedButNotCompleted)
                    {
                        // We don't have taskCompletionSource from an incoming request.
                        if (executionProperty.Idempotent
                            && context.GetPreviousResponseParameterExtension().TrySetResponseCanceled(executionProperty.OperationName))
                            context.ScheduleActivity(persist);
                    }
                    else
                    {
                        // We have taskCompletionSource from an incoming request, after setting the response in the extension we cancel the taskCompletionSource also.
                        if (executionProperty.Idempotent
                            && context.GetPreviousResponseParameterExtension().TrySetResponseCanceled(executionProperty.OperationName))
                            context.ScheduleActivity(persist, PersistCompletionCallback);
                        else
                            executionProperty.TrySetTaskCompletionSourceCanceled();
                    }
                }
            }

            private void PersistCompletionCallback(NativeActivityContext context, ActivityInstance completedInstance)
            {
                context.GetReceiveRequestSendResponseScopeExecutionProperty().TrySetTaskCompletionSourceCanceled();
            }
        }

        // Private activity to fault the operations' taskCompletionSource in case of fault propagation.
        private class ProtectFaultedOperation : TaskAsyncNativeActivity<UnhandledExceptionAction>
        {
            [RequiredArgument]
            public InArgument<Exception> PropagatedException { get; set; }

            private Activity cancelWorkflow;
            private ActivityAction<Exception> terminateWorkflow;

            public ProtectFaultedOperation()
            {
                cancelWorkflow = new CancelWorkflow();
                DelegateInArgument<Exception> exception = new DelegateInArgument<Exception>();
                terminateWorkflow = new ActivityAction<Exception>
                {
                    Argument = exception,
                    Handler = new TerminateWorkflow
                    {
                        Exception = exception,
                    },
                };
            }

            protected override void CacheMetadata(NativeActivityMetadata metadata)
            {
                metadata.AddImplementationChild(cancelWorkflow);
                metadata.AddImplementationDelegate(terminateWorkflow);
                base.CacheMetadata(metadata);
            }

            // We behave like an UnhandledException.
            protected override async Task<UnhandledExceptionAction> ExecuteAsync(NativeActivityContext context)
            {
                Exception propagatedException = PropagatedException.Get(context);
                IActivityContext activityContext = context.GetActivityContext();
                UnhandledExceptionAction unhandledExceptionAction = activityContext.Parameters.UnhandledExceptionAction;

                if (unhandledExceptionAction != UnhandledExceptionAction.Abort
                    && ! await activityContext.NotifyHostOnUnhandledExceptionAsync(propagatedException, null))
                    // If the host can't handle it, the instance will abort, independently from the configuration.
                    unhandledExceptionAction = UnhandledExceptionAction.Abort;
                return unhandledExceptionAction;
            }

            protected override void PostExecute(NativeActivityContext context, UnhandledExceptionAction unhandledExceptionAction)
            {
                Exception propagatedException = PropagatedException.Get(context);
                ReceiveRequestSendResponseScopeExecutionProperty executionProperty = context.GetReceiveRequestSendResponseScopeExecutionProperty();

                // Due to this activity will handle the fault, the Finally activity in the TryCatch will be executed also,
                // we have to prevent canceling the operation and the stored previous response in the extension.
                executionProperty.Faulted = true;

                switch (unhandledExceptionAction)
                {
                    default:
                    case UnhandledExceptionAction.Abort:
                        // This will implicitly also call OnUnhandledException.
                        context.Abort(propagatedException);
                        break;
                    case UnhandledExceptionAction.Cancel:
                        // Don't wait for completion, that is critical only for the persistence to happen before the taskCompletionSource of the request is completed.
                        context.ScheduleActivity(cancelWorkflow);
                        break;
                    case UnhandledExceptionAction.Terminate:
                        // Don't wait for completion, that is critical only for the persistence to happen before the taskCompletionSource of the request is completed.
                        context.ScheduleAction(terminateWorkflow, propagatedException);
                        break;
                }
            }
        }

        public const string ExecutionPropertyName = "ReceiveRequestSendResponseScope.ExecutionProperty";

        [DefaultValue(null)]
        [Browsable(false)]
        public Activity Body { get { return tryCatch.Try; } set { tryCatch.Try = value; } }

        private TryCatch tryCatch;

        // The execution property's proper type depends on the ResponseParameter's type of the SendResponse activity,
        // we create the factory within a constraint that visits the children of this scope and searches for the ISendResponse to create the factory.
        // The created execution property is persisted with the workflow.
        private Func<ReceiveRequestSendResponseScopeExecutionProperty> receiveRequestSendResponseScopeExecutionPropertyFactory;

        public ReceiveRequestSendResponseScope()
        {
            DelegateInArgument<Exception> propagatedException = new DelegateInArgument<Exception>();
            tryCatch = new TryCatch
            {
                //Try = Body, // it is already set above
                Catches =
                {
                    new Catch<Exception>
                    {
                        Action = new ActivityAction<Exception>
                        {
                            Argument = propagatedException,
                            Handler = new ProtectFaultedOperation
                            {
                                PropagatedException = propagatedException,
                            },
                        },
                    },
                },
                // The Finally works differently here than in C#, it is called only when the Try/Catch blocks complete.
                // In case of exception, ProtectFaultedOperation will handle the exception and will close, won't fault.
                // So this Finally is called in case of Close, Cancel or Fault, only in case of Close or Cancel, we should check the operation task's completion state.
                Finally = new ProtectClosedOrCanceledOperation(),
            };

            Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            Constraints.Add(ReceiveRequestSendResponseScopeHelper.VerifyReceiveRequestSendResponseScopeChildren());
            Constraints.Add(ReceiveRequestSendResponseScopeHelper.SetReceiveRequestSendResponseScopeExecutionPropertyFactory());
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddChild(tryCatch);
            //base.CacheMetadata(metadata); // it would add the public Body activity twice
        }

        protected override void Execute(NativeActivityContext context)
        {
            if (Body != null)
            {
                ReceiveRequestSendResponseScopeExecutionProperty executionProperty = receiveRequestSendResponseScopeExecutionPropertyFactory();
                context.Properties.Add(ExecutionPropertyName, executionProperty);
                context.ScheduleActivity(tryCatch);
            }
        }
    }
}
