using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;

using System.Activities;
using System.Activities.Tracking;
using Orleans.Activities;
using Orleans.Activities.Configuration;
using Orleans.Activities.Sample.HelloWorld.GrainInterfaces;
using Orleans.Runtime;

namespace Orleans.Activities.Sample.HelloWorld.Grains
{
    public class HelloGrainState : WorkflowState
    { }

    public interface IHelloWorkflowInterface
    {
        // This is the operation that the grain calls on the workflow, it shouldn't be the same as the IHello grain interface method!
        // There are 2 restrictions on the methods:
        // - it must have 1 parameter, with type Func<Task<anything>> or Func<Task> (executed when the workflow accepts the request)
        // - the return type can be Task or Task<anything>
        Task<string> GreetClient(Func<Task<string>> clientSaid);
    }

    public interface IHelloWorkflowCallbackInterface
    {
        // This is the operation that the workflow calls back on the grain.
        // There are 2 restrictions on the methods:
        // - it can have max. 1 parameter with any type
        // - the return type can be Task<Func<Task<anything>>> or Task<Func<Task>> (executed when the workflow accepts the response)
        Task<Func<Task<string>>> WhatShouldISay(string clientSaid);
    }

    public sealed class HelloGrain : WorkflowGrain<HelloGrainState, IHelloWorkflowInterface, IHelloWorkflowCallbackInterface>, IHello, IHelloWorkflowCallbackInterface
    {
        // Without DI and versioning, just direct create the workflow definition.
        public HelloGrain()
            : base((wi) => new HelloActivity(), null)
        { }

        // Optionally see what happens during the workflow execution with tracking.
        protected override IEnumerable<object> CreateExtensions()
        {
            yield return new GrainTrackingParticipant(GetLogger());
        }

        // Mandatory: at least log the unhandled exceptions (workflow will abort by default, see Parameters property).
        protected override Task OnUnhandledExceptionAsync(Exception exception, Activity source)
        {
            GetLogger().Error(0, $"OnUnhandledExceptionAsync: the workflow is going to {Parameters.UnhandledExceptionAction}", exception);
            return Task.CompletedTask;
        }

        // The parameter delegate executed when the workflow accepts the incoming call,
        // it can modify the grain's State or do nearly anything a normal grain method can (command pattern).
        public async Task<string> SayHello(string greeting)
        {
            try
            {
                return await WorkflowInterface.GreetClient(() =>
                    Task.FromResult(greeting));
            }
            catch (RepeatedOperationException<string> e)
            {
                return e.PreviousResponseParameter;
            }
        }

        // The return value delegate executed when the workflow accepts the outgoing call's response,
        // it can modify the grain's State or do nearly anything a normal grain method can (command pattern).
        Task<Func<Task<string>>> IHelloWorkflowCallbackInterface.WhatShouldISay(string clientSaid) =>
            Task.FromResult<Func<Task<string>>>(() =>
                Task.FromResult(string.IsNullOrEmpty(clientSaid) ? "Who are you?" : "Hello!"));
    }
}
