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
    // In this sample we don't use custom TGrainState, TWorkflowInterface and TWorkflowCallbackInterface type parameters, we use the less generic
    // WorkflowGrain<TGrain, TGrainState> base type with the default WorkflowState as TGrainState. But this is optional, the full blown
    // WorkflowGrain<TGrain, TGrainState, TWorkflowInterface, TWorkflowCallbackInterface> base type can be used also if there are outgoing calls or incoming callbacks.
    public sealed class AdderGrain : WorkflowGrain<AdderGrain, WorkflowState>, IAdder
    {
        public AdderGrain()
            : base((grainState, workflowIdentity) => new AdderActivity(), null)
        {
            // Set the persistence mode to Always, because the default setting is to not save the workflow on the first idle, to immediately accept the incoming operation.
            Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Always);

            WorkflowControl.ExtensionsFactory = () => new GrainTrackingParticipant(GetLogger()).Yield();
        }

        protected override Task OnUnhandledExceptionAsync(Exception exception, Activity source)
        {
            GetLogger().Error(0, $"OnUnhandledExceptionAsync: the workflow is going to {Parameters.UnhandledExceptionAction}", exception);
            return Task.CompletedTask;
        }

        // AdderGrain executes the workflow during the incoming request like a method. If there is a failure before the workflow completes (yes completes, not before goes idle),
        // it is propagated back to the caller. If the workflow persist itself but due to a failure it aborts later and propagates the exception back to the caller,
        // it will be reloaded when the caller repeats the request or by a reactivation reminder. If the caller repeats the call only after the workflow is reloaded and completed,
        // this not a problem, it will get the same output arguments or OperationCanceledException or the exception that caused the workflow to terminate.
        public async Task<int> AddAsync(int arg1, int arg2)
        {
            // IMPORTANT: Do not copy values from the grain's state into the input arguments, because input arguments will be persisted by the workflow also.
            // Closure directly the necessary values from the incoming public grain method call's parameters into the delegate.
            WorkflowControl.StartingAsync = () => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>()
            {
                { nameof(arg1), arg1 },
                { nameof(arg2), arg2 },
            });

            IDictionary<string, object> outputArguments = await WorkflowControl.RunToCompletionAsync();

            return (int)outputArguments["result"];
        }
    }
}
