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
    public sealed class AdderGrain : WorkflowGrain<AdderGrain, WorkflowState>, IAdder
    {
        public AdderGrain()
            : base((grainState, workflowIdentity) => new AdderActivity(), null)
        {
            Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Always);

            WorkflowControl.ExtensionsFactory = () => new GrainTrackingParticipant(GetLogger()).Yield();
        }

        protected override Task OnUnhandledExceptionAsync(Exception exception, Activity source)
        {
            GetLogger().Error(0, $"OnUnhandledExceptionAsync: the workflow is going to {Parameters.UnhandledExceptionAction}", exception);
            return Task.CompletedTask;
        }

        public async Task<int> AddAsync(int arg1, int arg2)
        {
            WorkflowControl.OnStartAsync = () => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>()
            {
                { nameof(arg1), arg1 },
                { nameof(arg2), arg2 },
            });

            IDictionary<string, object> outputArguments = await WorkflowControl.RunToCompletionAsync();

            return (int)outputArguments["result"];
        }
    }
}
