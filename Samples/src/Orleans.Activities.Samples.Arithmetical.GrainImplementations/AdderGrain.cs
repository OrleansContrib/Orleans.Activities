using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Providers;

using System.Activities;
using Orleans.Activities;
using Orleans.Activities.Configuration;
using Orleans.Activities.Samples.Arithmetical.GrainInterfaces;

namespace Orleans.Activities.Samples.Arithmetical.GrainImplementations
{
    // In this sample we don't use custom TGrainState, TWorkflowInterface and TWorkflowCallbackInterface type parameters, we use the less generic
    // WorkflowGrain<TGrain, TGrainState> base type with the default WorkflowState as TGrainState. But this is optional, the full blown
    // WorkflowGrain<TGrain, TGrainState, TWorkflowInterface, TWorkflowCallbackInterface> base type can be used also if there are outgoing calls or incoming callbacks.
    [StorageProvider(ProviderName = "MemoryStore")]
    public sealed class AdderGrain : WorkflowGrain<AdderGrain, WorkflowState>, IAdderGrain
    {
        private static Activity workflowDefinition = new AdderActivity();

        private readonly ILogger logger;

        public AdderGrain(ILogger<MultiplierGrain> logger)
            : base((grainState, workflowIdentity) => workflowDefinition, null)
        {
            this.logger = logger;

            // Set the persistence mode to Always, because the default setting is to not save the workflow on the first idle, to immediately accept the incoming operation.
            this.Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Always);

            this.WorkflowControl.ExtensionsFactory = () => new GrainTrackingParticipant(logger).Yield();
        }

        protected override Task OnUnhandledExceptionAsync(Exception exception, Activity source)
        {
            this.logger.LogError($"OnUnhandledExceptionAsync: the workflow is going to {this.Parameters.UnhandledExceptionAction}\n\n{exception}", Runtime.Severity.Error);
            return Task.CompletedTask;
        }

        // AdderGrain executes the workflow during the incoming request like a method. If there is a failure before the workflow completes (yes completes, not before goes idle,
        // due to RunToCompletionAsync() is called), it is propagated back to the caller. If the workflow persist itself but due to a failure it aborts later and propagates
        // the exception back to the caller, it will be reloaded when the caller repeats the request or by a reactivation reminder. If the caller repeats the call only after
        // the workflow is reloaded and completed, this not a problem, it will get the same output arguments or OperationCanceledException or the exception that caused
        // the workflow to terminate.
        async Task<int> IAdderGrain.AddAsync(int arg1, int arg2)
        {
            // IMPORTANT: Do not copy values from the grain's state into the input arguments, because input arguments will be persisted by the workflow also.
            // Closure directly the necessary values from the incoming public grain method call's parameters into the delegate.
            this.WorkflowControl.StartingAsync = () => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>()
            {
                { nameof(AdderActivity.Arg1), arg1 },
                { nameof(AdderActivity.Arg2), arg2 },
            });

            var outputArguments = await this.WorkflowControl.RunToCompletionAsync();

            return (int)outputArguments[nameof(AdderActivity.Result)];
        }
    }
}
