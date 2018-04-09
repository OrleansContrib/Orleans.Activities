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
using Orleans.Activities.Samples.HelloWorld.GrainInterfaces;

namespace Orleans.Activities.Samples.HelloWorld.GrainImplementations
{
    public class HelloGrainState : WorkflowState
    { }

    public interface IHelloWorkflow
    {
        // These are the operations that the grain calls on the workflow, these shouldn't be the same as the IHelloGrain grain interface method!
        // There are 2 restrictions on the methods:
        // - it must have 1 parameter, with type Func<Task<anything>> or Func<Task> (executed when the workflow accepts the request)
        // - the return type can be Task or Task<anything>
        Task<string> GreetClientAsync(Func<Task<string>> clientSaid);
        Task<string> FarewellClientAsync(Func<Task> request);
    }

    public interface IHelloWorkflowCallback
    {
        // This is the operation that the workflow calls back on the grain.
        // There are 2 restrictions on the methods:
        // - it can have max. 1 parameter with any type
        // - the return type can be Task<Func<Task<anything>>> or Task<Func<Task>> (executed when the workflow accepts the response)
        Task<Func<Task<string>>> WhatShouldISayAsync(string clientSaid);
    }

    [StorageProvider(ProviderName = "MemoryStore")]
    public sealed class HelloGrain : WorkflowGrain<HelloGrain, HelloGrainState, IHelloWorkflow, IHelloWorkflowCallback>, IHelloGrain, IHelloWorkflowCallback
    {
        // Without DI and versioning, just directly create the singleton workflow definition.
        private static Activity workflowDefinition = new HelloActivity();

        private readonly ILogger logger;

        public HelloGrain(ILogger<HelloGrain> logger)
            : base((grainState, workflowIdentity) => workflowDefinition, null)
        {
            this.logger = logger;
            this.WorkflowControl.ExtensionsFactory = () => new GrainTrackingParticipant(logger).Yield();
        }

        // Mandatory: at least log the unhandled exceptions (workflow will abort by default, see Parameters property).
        protected override Task OnUnhandledExceptionAsync(Exception exception, Activity source)
        {
            this.logger.LogError($"OnUnhandledExceptionAsync: the workflow is going to {this.Parameters.UnhandledExceptionAction}\n\n{exception}");
            return Task.CompletedTask;
        }

        // The parameter delegate executed when the workflow accepts the incoming call,
        // it can modify the grain's State or do nearly anything a normal grain method can (command pattern).
        async Task<string> IHelloGrain.SayHelloAsync(string greeting)
        {
            Task<string> ProcessRequestAsync(string _request) => Task.FromResult(_request);
            Task<string> CreateResponseAsync(string _responseParameter) => Task.FromResult(_responseParameter);

            try
            {
                return await CreateResponseAsync(
                    await this.WorkflowInterface.GreetClientAsync(
                        async () => await ProcessRequestAsync(greeting)));
            }
            // Idempotent response
            catch (OperationRepeatedException<string> e)
            {
                return await CreateResponseAsync(e.PreviousResponseParameter);
            }
        }

        // The parameter delegate executed when the workflow accepts the incoming call,
        // it can modify the grain's State or do nearly anything a normal grain method can (command pattern).
        async Task<string> IHelloGrain.SayByeAsync()
        {
            Task ProcessRequestAsync() => Task.CompletedTask;
            Task<string> CreateResponseAsync(string _responseParameter) => Task.FromResult(_responseParameter);

            try
            {
                return await CreateResponseAsync(
                    await this.WorkflowInterface.FarewellClientAsync(
                        async () => await ProcessRequestAsync()));
            }
            // Idempotent response
            catch (OperationRepeatedException<string> e)
            {
                return await CreateResponseAsync(e.PreviousResponseParameter);
            }
            // Out-of-order response
            catch (InvalidOperationException)
            {
                return "Sorry, you must say hello first, before farewell!";
            }
            // Idempotent cancellation
            catch (OperationCanceledException)
            {
                return "Sorry, we have waited for your farewell, but gave up!";
            }
        }

        // The return value delegate executed when the workflow accepts the outgoing call's response,
        // it can modify the grain's State or do nearly anything a normal grain method can (command pattern).
        async Task<Func<Task<string>>> IHelloWorkflowCallback.WhatShouldISayAsync(string clientSaid)
        {
            Task<string> CreateRequestAsync(string _requestParameter) => Task.FromResult(_requestParameter);
            Task<string> SomeExternalStuffAsync(string _request) => Task.FromResult(string.IsNullOrEmpty(_request) ? "Who are you?" : "Hello!");
            Task<string> ProcessResponseAsync(string _response) => Task.FromResult(_response);

            var request = await CreateRequestAsync(clientSaid);
            var response = await SomeExternalStuffAsync(request);
            return async () => await ProcessResponseAsync(response);
        }
    }
}
