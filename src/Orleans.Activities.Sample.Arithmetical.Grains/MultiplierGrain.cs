using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;

using System.Activities;
using Orleans.Activities;
using Orleans.Activities.Configuration;
using Orleans.Activities.Sample.Arithmetical.GrainInterfaces;

namespace Orleans.Activities.Sample.Arithmetical.Grains
{
    public sealed class MultiplierGrain : WorkflowGrain<MultiplierGrain, WorkflowState>, IMultiplier
    {
        public MultiplierGrain()
            : base((grainState, workflowIdentity) => new MultiplierActivity(), null)
        {
            Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Always);

            WorkflowControl.ExtensionsFactory = () => new GrainTrackingParticipant(GetLogger()).Yield();

            WorkflowControl.OnCompletedAsync = (ActivityInstanceState activityInstanceState, IDictionary<string, object> outputArguments, Exception terminationException) =>
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

        public async Task MultiplyAsync(int arg1, int arg2)
        {
            WorkflowControl.OnStartAsync = () => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>()
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
