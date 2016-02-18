# Hello World

Based on Orleans [Hello World](https://dotnet.github.io/orleans/Samples-Overview/Hello-World) sample.

## Overview

![SequenceDiagram](https://raw.githubusercontent.com/OrleansContrib/Orleans.Activities/master/docs/HelloWorld/HelloWorld-Overview.png)

## Interface

IHello is nearly the same, an optional `SayBye()` method is added.

```c#
public interface IHello : IGrainWithGuidKey
{
  Task<string> SayHello(string greeting);
  Task<string> SayBye();
}
```

## Grain

Before the grain, you have to define 3 things: Grain State, Workflow Interface and Workflow Callback Interface

### Grain State

Workflows always have a state. Even if they never persist it. You can use the `WorkflowState` base class or implement the `IWorkflowState` interface.

```c#
public class HelloGrainState : WorkflowState
{ }
```

### Workflow Interface

These are the operations that the grain calls on the workflow, these operations should __NOT__ be the same as the public grain interface methods (see `IHello`)!

There are 2 restrictions on the methods:

* must have 1 parameter, with type `Func<Task<anything>>` or `Func<Task>` (executed when the workflow accepts the request)
* the return type must be `Task` or `Task<anything>`

```c#
public interface IHelloWorkflowInterface
{
  Task<string> GreetClient(Func<Task<string>> clientSaid);
  Task<string> FarewellClient(Func<Task> request);
}
```

### Workflow Callback Interface

These are the operations that the workflow calls back on the grain.

There are 2 restrictions on the methods:

* can have max. 1 parameter with any type
* the return type must be `Task<Func<Task<anything>>>` or `Task<Func<Task>>` (executed when the workflow accepts the response)

```c#
public interface IHelloWorkflowCallbackInterface
{
  Task<Func<Task<string>>> WhatShouldISay(string clientSaid);
}
```

### Grain

The class definition, where we define the TGrain, TGrainState, TWorkflowInterface and TWorkflowCallbackInterface type parameters.

__NOTE:__ The grain must implement (if possible explicitly) the TWorkflowCallbackInterface interface (see `IHelloWorkflowCallbackInterface`) and TGrain should be the grain itself.

```c#
public sealed class HelloGrain : WorkflowGrain<HelloGrain, HelloGrainState, IHelloWorkflowInterface, IHelloWorkflowCallbackInterface>,
  IHello, IHelloWorkflowCallbackInterface
```

Constructor, in this example without Dependency Injection, just define the workflow definition (ie. activity) factory and leave the workflow definition identity factory null.

Optionally, to see what happens during the workflow execution with tracking, we add a TrackingParticipant extension. The ExtensionsFactory property can also be null.

```c#
public HelloGrain()
  : base(((grainState, workflowIdentity)) => new HelloActivity(), null)
{
  WorkflowControl.ExtensionsFactory = () => new GrainTrackingParticipant(GetLogger()).Yield();
}
```

A mandatory (boilerplate) implementation of the unhandled exception handler. Because workflows can run in the backround after an incoming call returns the result, we can't propagate back exceptions after this point. Workflow will by default abort in case of unhandled exception, depending on the `Parameters` property.

```c#
protected override Task OnUnhandledExceptionAsync(Exception exception, Activity source)
{
  GetLogger().Error(0, $"OnUnhandledExceptionAsync: the workflow is going to {Parameters.UnhandledExceptionAction}", exception);
  return Task.CompletedTask;
}
```

The public `SayHello()` grain interface method, that does nothing just calls the workflow's `GreetClient()` WorkflowInterface operation. A normal grain can store data from the incoming message in the state, call other grains, closure the necessary data into the parameter delegate. After the await, it can build a complex response message based on the value the workflow returned and the grain state, or any other information.

The parameter delegate executed when the workflow accepts the incoming call.

It also shows how to implement idempotent responses for the incoming calls. In the repeated case, the parameter delegate won't be executed!

```c#
public async Task<string> SayHello(string greeting)
{
  try
  {
    return await WorkflowInterface.GreetClient(() =>
      Task.FromResult(greeting));
  }
  catch (OperationRepeatedException<string> e)
  {
    return e.PreviousResponseParameter;
  }
}
```

The public `SayBye()` grain interface method, that also does nothing just calls the workflow's `FarewellClient()` optional WorkflowInterface operation.
The parameter delegate executed when the workflow accepts the incoming call.

It also shows how to implement optional operation's idempotent canceled responses for the incoming calls. Optional in this case means, that after a timeout the workflow cancels the waiting for the operation. In the canceled case, after the timeout, the parameter delegate won't be executed!

```c#
public async Task<string> SayBye()
{
  try
  {
    return await WorkflowInterface.FarewellClient(() =>
      Task.CompletedTask);
  }
  catch (OperationRepeatedException<string> e)
  {
    return e.PreviousResponseParameter;
  }
  catch (OperationCanceledException)
  {
    return "Sorry, we have waited for your farewell, but gave up!";
  }
}
```

This is the explicit implementation of the workflow's `WhatShouldISay()` WorkflowCallbackInterface operation, that does nearly nothing. A normal grain can modify the grain's State, call other grain's operations or do nearly anything a normal grain method can.  

The return value delegate executed when the workflow accepts the outgoing call's response.

```c#
Task<Func<Task<string>>> IHelloWorkflowCallbackInterface.WhatShouldISay(string clientSaid) =>
  Task.FromResult<Func<Task<string>>>(() =>
    Task.FromResult(string.IsNullOrEmpty(clientSaid) ? "Who are you?" : "Hello!"));
```

## Workflow / Activity

And see the Workflow:

* First it accepts the incoming `GreetClient()` operation, calls back the grain with `WhatShouldISay()` operation, and returns the response to the grain.
* Then it waits 1 minute for the `FarewellClient()` operation, if it times out, it cancels the operation and completes.
* Both `GreetClient()` and `FarewellClient()` operations are idempotent, so the responses are persisted (in our concrete example, `FarewellClient()` operation times out, so the fact that it was canceled is persisted).

![HelloActivity.xaml](https://raw.githubusercontent.com/OrleansContrib/Orleans.Activities/master/docs/HelloWorld/HelloActivity.png)

That's all. Ctrl+F5, and it works.

## Details

If you want to dig deep into the source and understand the detailed events in the background, this sequence diagram can help (this is not a completely valid diagram, but displaying every asnyc details, even the AsyncAutoResetEvent idle-queue, this would be 2 times bigger).

![SequenceDiagram](https://raw.githubusercontent.com/OrleansContrib/Orleans.Activities/master/docs/HelloWorld/HelloWorld-Details.png)
