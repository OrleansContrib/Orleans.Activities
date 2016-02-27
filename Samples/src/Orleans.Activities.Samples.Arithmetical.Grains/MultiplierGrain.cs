using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;

using System.Activities;
using Orleans.Activities;
using Orleans.Activities.Configuration;
using Orleans.Activities.Samples.Arithmetical.GrainInterfaces;

namespace Orleans.Activities.Samples.Arithmetical.Grains
{
    // In this sample we don't use custom TGrainState, TWorkflowInterface and TWorkflowCallbackInterface type parameters, we use the less generic
    // WorkflowGrain<TGrain, TGrainState> base type with the default WorkflowState as TGrainState. But this is optional, the full blown
    // WorkflowGrain<TGrain, TGrainState, TWorkflowInterface, TWorkflowCallbackInterface> base type can be used also if there are outgoing calls or incoming callbacks.
    public sealed class MultiplierGrain : WorkflowGrain<MultiplierGrain, WorkflowState>, IMultiplier
    {
        public MultiplierGrain()
            : base((grainState, workflowIdentity) => new MultiplierActivity(), null)
        {
            // Set the persistence mode to Always, because the default setting is to not save the workflow on the first idle, to immediately accept the incoming operation.
            Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Always);

            WorkflowControl.ExtensionsFactory = () => new GrainTrackingParticipant(GetLogger()).Yield();

            // NOTE: This sample can't demonstrate a failure during the callback (the observation is a one-way call), but the workflow persistence happens after the Completed
            // event: if the callback fails, the workflow will abort and continue from the last persisted state by a reactivation reminder.
            // Don't use callbacks when there is no implicit or explicit persistence before (like in the sample), because the incoming request that started the workflow will run
            // the workflow to the first idle moment, if the first idle is the completion, the callback will happen during the incoming request (usually also a problem),
            // and the exception during the callback will be propagated back to the caller and the caller has to repeat the incoming request to restart the workflow.
            WorkflowControl.CompletedAsync = (ActivityInstanceState activityInstanceState, IDictionary<string, object> outputArguments, Exception terminationException) =>
            {
                _subsManager.Notify(s => s.ReceiveResult((int)outputArguments["result"]));
                return Task.CompletedTask;
            };
        }

        protected override Task OnUnhandledExceptionAsync(Exception exception, Activity source)
        {
            GetLogger().Error(0, $"OnUnhandledExceptionAsync: the workflow is going to {Parameters.UnhandledExceptionAction}", exception);
            return Task.CompletedTask;
        }

        // MultiplierGrain only executes the workflow until it gets idle, from that moment the workflow executes in the "background" and calls the Completed event when it completes.
        public async Task MultiplyAsync(int arg1, int arg2)
        {
            // IMPORTANT: Do not copy values from the grain's state into the input arguments, because input arguments will be persisted by the workflow also.
            // Closure directly the necessary values from the incoming public grain method call's parameters into the delegate.
            WorkflowControl.StartingAsync = () => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>()
            {
                { nameof(arg1), arg1 },
                { nameof(arg2), arg2 },
            });

            await WorkflowControl.RunAsync();
        }

        #region Subscription

        private ObserverSubscriptionManager<IMultiplierResultReceiver> _subsManager;

        public override async Task OnActivateAsync()
        {
            _subsManager = new ObserverSubscriptionManager<IMultiplierResultReceiver>();
            await base.OnActivateAsync();
        }

        public Task Subscribe(IMultiplierResultReceiver observer)
        {
            _subsManager.Subscribe(observer);
            return Task.CompletedTask;
        }

        public Task Unsubscribe(IMultiplierResultReceiver observer)
        {
            _subsManager.Unsubscribe(observer);
            return Task.CompletedTask;
        }

        #endregion
    }
}
