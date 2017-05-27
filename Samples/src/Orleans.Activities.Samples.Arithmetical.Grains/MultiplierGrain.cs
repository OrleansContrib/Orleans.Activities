using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;
using Orleans.Providers;

using System.Activities;
using Orleans.Activities;
using Orleans.Activities.Configuration;
using Orleans.Activities.Samples.Arithmetical.GrainInterfaces;

namespace Orleans.Activities.Samples.Arithmetical.Grains
{
    // In this sample we don't use custom TGrainState, TWorkflowInterface and TWorkflowCallbackInterface type parameters, we use the less generic
    // WorkflowGrain<TGrain, TGrainState> base type with the default WorkflowState as TGrainState. But this is optional, the full blown
    // WorkflowGrain<TGrain, TGrainState, TWorkflowInterface, TWorkflowCallbackInterface> base type can be used also if there are outgoing calls or incoming callbacks.
    [StorageProvider(ProviderName = "MemoryStore")]
    public sealed class MultiplierGrain : WorkflowGrain<MultiplierGrain, WorkflowState>, IMultiplier
    {
        private static Activity workflowDefinition = new MultiplierActivity();

        public MultiplierGrain()
            : base((grainState, workflowIdentity) => workflowDefinition, null)
        {
            // Set the persistence mode to Always, because the default setting is to not save the workflow on the first idle, to immediately accept the incoming operation.
            Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Always);

            WorkflowControl.ExtensionsFactory = () => new GrainTrackingParticipant(GetLogger()).Yield();

            // NOTE: This sample can't demonstrate a failure during the callback (the observation is a one-way call), but the workflow persistence happens after the Completed
            // event: if the callback fails, the workflow will abort and continue from the last persisted state by a reactivation reminder.
            // Don't use callbacks on Completed event when there is no implicit or explicit persistence before, because the incoming request that started the workflow will run
            // the workflow to the first idle moment, if the first idle is the completion, the callback will happen during the incoming request (usually also a problem),
            // and the exception during the callback will be propagated back to the caller and the caller has to repeat the incoming request to restart the workflow.
            WorkflowControl.CompletedAsync = (ActivityInstanceState _activityInstanceState, IDictionary<string, object> _outputArguments, Exception _terminationException) =>
            {
                subsManager.Notify(_subscriber => _subscriber.ReceiveResult((int)_outputArguments["result"]));
                return Task.CompletedTask;
            };
        }

        protected override Task OnUnhandledExceptionAsync(Exception exception, Activity source)
        {
            GetLogger().TrackTrace($"OnUnhandledExceptionAsync: the workflow is going to {Parameters.UnhandledExceptionAction}\n\n{exception}", Runtime.Severity.Error);
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

        private ObserverSubscriptionManager<IMultiplierResultReceiver> subsManager;

        public override async Task OnActivateAsync()
        {
            await base.OnActivateAsync();
            subsManager = new ObserverSubscriptionManager<IMultiplierResultReceiver>();
        }

        public Task SubscribeAsync(IMultiplierResultReceiver observer)
        {
            subsManager.Subscribe(observer);
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(IMultiplierResultReceiver observer)
        {
            subsManager.Unsubscribe(observer);
            return Task.CompletedTask;
        }

        #endregion
    }
}
