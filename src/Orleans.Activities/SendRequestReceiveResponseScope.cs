using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Statements;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Markup;
using Orleans.Activities.Extensions;
using Orleans.Activities.Helpers;
using Orleans.Activities.Designers;

namespace Orleans.Activities
{
    /// <summary>
    /// Scope for SendRequest and ReceiveResponse activities.
    /// <para>The main responsibility of this activity, to await the outgoing TWorkflowCallbackInterface operation's task in case ReceiveResponse is unable to do this.
    /// This is necessary in case of a fault propagation, that can be the result of an unhandled exception.</para>
    /// </summary>
    [ContentProperty(nameof(Body))]
    [Designer(typeof(SendRequestReceiveResponseScopeDesigner))]
    [ToolboxBitmap(typeof(SendRequestReceiveResponseScope), nameof(SendRequestReceiveResponseScope) + ".png")]
    [Description("Scope for SendRequest and ReceiveResponse activities.\n" +
        "The main responsibility of this activity, to await the outgoing TWorkflowCallbackInterface operation's task in case ReceiveResponse is unable to do this. " +
        "This is necessary in case of a fault propagation, that can be the result of an unhandled exception.")]
    public sealed class SendRequestReceiveResponseScope : NativeActivity
    {
        // An elaborate setter for the private sendRequestReceiveResponseScopeExecutionPropertyFactory field, used by the SendRequestReceiveResponseScopeHelper's constraints.
        public sealed class SendRequestReceiveResponseScopeExecutionPropertyFactorySetter : CodeActivity
        {
            public InArgument<IReceiveResponse> IReceiveResponse { get; set; }

            public InArgument<SendRequestReceiveResponseScope> SendRequestReceiveResponseScope { get; set; }

            protected override void Execute(CodeActivityContext context)
            {
                SendRequestReceiveResponseScope.Get(context).sendRequestReceiveResponseScopeExecutionPropertyFactory =
                    IReceiveResponse.Get(context).CreateSendRequestReceiveResponseScopeExecutionPropertyFactory();
            }
        }

        // This activity is responsible to await the outgoing TWorkflowCallbackInterface operation's task in case ReceiveResponse is unable to do this.
        // This is necessary in case of a fault propagation, that can be the result of an unhandled exception.
        private sealed class ConditionalOperationTaskWaiter : NativeActivity
        {
            private ActivityAction<Task> responseResultWaiter;

            public ConditionalOperationTaskWaiter()
            {
                responseResultWaiter = TaskWaiter.CreateActivityDelegate();
            }

            protected override void CacheMetadata(NativeActivityMetadata metadata)
            {
                metadata.AddImplementationDelegate(responseResultWaiter);
                base.CacheMetadata(metadata);
            }

            protected override void Execute(NativeActivityContext context)
            {
                SendRequestReceiveResponseScopeExecutionProperty executionProperty = context.GetSendRequestReceiveResponseScopeExecutionProperty();
                if (executionProperty.UntypedOnOperationTask != null)
                {
                    // We await the task, but we won't execute the task's Func<Task> or Func<Task<TResponseResult>> return value, that is the ReceiveResponse activity's responsibility.
                    context.ScheduleAction(responseResultWaiter, executionProperty.UntypedOnOperationTask);
                    executionProperty.OnOperationTaskWaiterIsScheduled();
                }       
            }
        }

        public const string ExecutionPropertyName = "SendRequestReceiveResponseScope.ExecutionProperty";

        [DefaultValue(null)]
        [Browsable(false)]
        public Activity Body { get { return tryCatch.Try; } set { tryCatch.Try = value; } }

        private TryCatch tryCatch;

        private Variable<NoPersistHandle> noPersistHandle;

        // The execution property's proper type depends on the ResponseResult's type of the ReceiveResponse activity,
        // we create the factory within a constraint that visits the children of this scope and searches for the IReceiveResponse to create the factory.
        private Func<SendRequestReceiveResponseScopeExecutionProperty> sendRequestReceiveResponseScopeExecutionPropertyFactory;

        public SendRequestReceiveResponseScope()
        {
            tryCatch = new TryCatch
            {
                //Try = Body, // it is already set above
                Catches =
                {
                    new Catch<Exception>
                    {
                        Action = new ActivityAction<Exception>
                        {
                            Handler = new Sequence
                            {
                                Activities =
                                {
                                    new ConditionalOperationTaskWaiter(),
                                    new Rethrow(),
                                },
                            },
                        },
                    },
                },
                // The Finally works differently here than in C#, it is called only when the Try/Catch blocks complete.
                // In case of exception, those activities above will always Fault (the Body or additionally the operation task will throw).
                // This Finally is called only in case of Close or Cancel, in case of Cancel, we should also check the operation task's observed state.
                // A special case, when this scope is in an external TryCatch and that Catch handles the propagated exception,
                // in this case both Fault and Cancel happens, and we double-check the the operation task's observed state.
                Finally = new ConditionalOperationTaskWaiter(),
            };
            noPersistHandle = new Variable<NoPersistHandle>();
            Constraints.Add(OperationActivityHelper.VerifyParentIsWorkflowActivity());
            Constraints.Add(SendRequestReceiveResponseScopeHelper.VerifySendRequestReceiveResponseScopeChildren());
            Constraints.Add(SendRequestReceiveResponseScopeHelper.SetWorkflowCallbackInterfaceOperationNames());
            Constraints.Add(SendRequestReceiveResponseScopeHelper.SetSendRequestReceiveResponseScopeExecutionPropertyFactory());
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationVariable(noPersistHandle);
            metadata.AddChild(tryCatch);
            //base.CacheMetadata(metadata); // it would add the public Body activity twice
        }

        // SendRequest has scheduled the OnOperation task, but didn't wait for it, the task is an implicit (single threaded reentrant) parallel activity,
        // the Scope is responsible to handle the outstanding task in case of Abort, Cancellation or Termination (like a virtual Task.WhenAll() method).
        protected override void Execute(NativeActivityContext context)
        {
            if (Body != null)
            {
                // The noPersistHandle will be exited when the activity completes or aborts, because
                // the handle is an execution property in the background, with a scope on this activity, so it will be removed.
                // Don't add Exit() to the Completion and/or Fault callback, because if this scope is in an external TryCatch,
                // this will first Fault, then Canceled by the external TryCatch (if the fault propagation is handled), causing "unmatched exit" exception.
                noPersistHandle.Get(context).Enter(context);

                SendRequestReceiveResponseScopeExecutionProperty executionProperty = sendRequestReceiveResponseScopeExecutionPropertyFactory();
                context.Properties.Add(ExecutionPropertyName, executionProperty);
                context.ScheduleActivity(tryCatch);
            }
        }
    }
}
