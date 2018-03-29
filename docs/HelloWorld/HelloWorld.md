# Hello World

Based on Orleans [Hello World](https://dotnet.github.io/orleans/Documentation/Samples-Overview/Hello-World) sample.

Shows how to communicate with the workflow through custom interfaces.

Yes it's overcomplicated to write "Hello World" to the screen, but this shows the basic steps to implement a workflow that communicates with the external world meanwhile reliably executes the process described by the workflow activities.

## Overview

![HelloWorld-Overview](https://github.com/OrleansContrib/Orleans.Activities/raw/docs-master/docs/HelloWorld/HelloWorld-Overview.png)

## Interface

IHelloGrain is nearly the same, an optional `SayBye()` method is added.

```c#
public interface IHelloGrain : IGrainWithGuidKey
{
  Task<string> SayHelloAsync(string greeting);
  Task<string> SayByeAsync();
}
```

## Grain prerequisites

Before the grain, you have to define 3 things: Grain State, Workflow Interface and Workflow Callback Interface

### Grain State

Workflows always have a state. Even if they never persist it. You can use the `WorkflowState` base class or implement the `IWorkflowState` interface.

```c#
public class HelloGrainState : WorkflowState
{ }
```

### Workflow Interface

These are the operations that the grain calls on the workflow, these operations should __NOT__ be the same as the public grain interface methods (see `IHelloGrain`)!

There are 2 restrictions on the methods:

* must have 1 parameter, with type `Func<Task<anything>>` or `Func<Task>` (executed when the workflow accepts the request)
* the return type must be `Task` or `Task<anything>`

```c#
public interface IHelloWorkflow
{
  Task<string> GreetClientAsync(Func<Task<string>> clientSaid);
  Task<string> FarewellClientAsync(Func<Task> request);
}
```

### Workflow Callback Interface

These are the operations that the workflow calls back on the grain.

There are 2 restrictions on the methods:

* can have max. 1 parameter with any type
* the return type must be `Task<Func<Task<anything>>>` or `Task<Func<Task>>` (executed when the workflow accepts the response)

```c#
public interface IHelloWorkflowCallback
{
  Task<Func<Task<string>>> WhatShouldISayAsync(string clientSaid);
}
```

## Grain

The class definition, where we define the `TGrain`, `TGrainState`, `TWorkflowInterface` and `TWorkflowCallbackInterface` type parameters.

__NOTE:__ The grain must implement (if possible explicitly) the `TWorkflowCallbackInterface` interface (see `IHelloWorkflowCallback`) and `TGrain` should be the grain itself.

```c#
public sealed class HelloGrain : WorkflowGrain<HelloGrain, HelloGrainState, IHelloWorkflow, IHelloWorkflowCallback>,
  IHelloGrain, IHelloWorkflowCallback { ... }
```

### Constructor

In this example without Dependency Injection, just define the singleton workflow definition (ie. activity) factory and leave the workflow definition identity factory null.

Optionally, to see what happens during the workflow execution with tracking, we add a `TrackingParticipant` extension. The `ExtensionsFactory` property can also be null.

```c#
private static Activity workflowDefinition = new HelloActivity();

public HelloGrain()
  : base((grainState, workflowIdentity) => workflowDefinition, null)			
{
  WorkflowControl.ExtensionsFactory = () => new GrainTrackingParticipant(GetLogger()).Yield();
}
```

### Unhandled exception handler

A mandatory (boilerplate) implementation of the unhandled exception handler. Because workflows can run in the backround after an incoming call returns the result, we can't propagate back exceptions after this point. Workflow will by default abort in case of unhandled exception, depending on the `Parameters` property.

```c#
protected override Task OnUnhandledExceptionAsync(Exception exception, Activity source)
{
  GetLogger().TrackTrace($"OnUnhandledExceptionAsync: the workflow is going to {Parameters.UnhandledExceptionAction}\n\n{exception}", Runtime.Severity.Error);
  return Task.CompletedTask;
}
```

### Incoming operations

The `SayHelloAsync()` grain interface method, that does nothing just calls the workflow's `GreetClientAsync()` `WorkflowInterface` operation. A normal grain can store data from the incoming message in the `State`, call other grains, closure the necessary data into the parameter delegate. After the `await`, it can build a complex response message based on the value the workflow returned and the grain's `State`, or any other information.

The parameter delegate is executed when the workflow accepts the incoming call.

It also shows how to implement idempotent responses for the incoming calls. In the repeated case, the parameter delegate won't be executed!

```c#
async Task<string> IHelloGrain.SayHelloAsync(string greeting)
{
  Task<string> ProcessRequestAsync(string _request) => Task.FromResult(_request);
  Task<string> CreateResponseAsync(string _responseParameter) => Task.FromResult(_responseParameter);

  try
  {
    return await CreateResponseAsync(
      await WorkflowInterface.GreetClientAsync(
        async () => await ProcessRequestAsync(greeting)));
  }
  catch (OperationRepeatedException<string> e)
  {
    return await CreateResponseAsync(e.PreviousResponseParameter);
  }
}
```

The `SayByeAsync()` grain interface method, that also does nothing just calls the workflow's `FarewellClientAsync()` optional `WorkflowInterface` operation.
The parameter delegate executed when the workflow accepts the incoming call.

It also shows how to handle out-of-order request, when the `SayByeAsync()` method is called before the `SayHelloAsync()` method and the workflow is not ready to process the request (`InvalidOperationException`). In the out-of-order case, the parameter delegate won't be executed!

It also shows how to implement optional operation's idempotent canceled responses for the incoming calls (`OperationCanceledException`). Optional in this case means, that after a timeout the workflow cancels the waiting for the operation. In the canceled case, after the timeout, the parameter delegate won't be executed!

```c#
async Task<string> IHelloGrain.SayByeAsync()
{
  Task ProcessRequestAsync() => Task.CompletedTask;
  Task<string> CreateResponseAsync(string _responseParameter) => Task.FromResult(_responseParameter);

  try
  {
    return await CreateResponseAsync(
      await WorkflowInterface.FarewellClientAsync(
        async () => await ProcessRequestAsync()));
  }
  catch (OperationRepeatedException<string> e)
  {
    return await CreateResponseAsync(e.PreviousResponseParameter);
  }
  catch (InvalidOperationException)
  {
      return "Sorry, you must say hello first, before farewell!";
  }  
  catch (OperationCanceledException)
  {
    return "Sorry, we have waited for your farewell, but gave up!";
  }
}
```

### Outgoing operations

This is the explicit implementation of the workflow's `WhatShouldISay()` `IWorkflowCallback` interface operation, that does nearly nothing. A normal grain can modify the grain's `State`, call other grain's operations or do nearly anything a normal grain method can.  

The return value delegate is executed when the workflow accepts the outgoing call's response.

```c#
async Task<Func<Task<string>>> IHelloWorkflowCallback.WhatShouldISayAsync(string clientSaid)
{
  Task<string> CreateRequestAsync(string _requestParameter) => Task.FromResult(_requestParameter);
  Task<string> SomeExternalStuffAsync(string _request) => Task.FromResult(string.IsNullOrEmpty(_request) ? "Who are you?" : "Hello!");
  Task<string> ProcessResponseAsync(string _response) => Task.FromResult(_response);

  string request = await CreateRequestAsync(clientSaid);
  string response = await SomeExternalStuffAsync(request);
  return async () => await ProcessResponseAsync(response);
}
```

## Workflow / Activity

And see the Workflow:

* First it accepts the incoming `GreetClientAsync()` operation, calls back the grain with `WhatShouldISayAsync()` operation, and returns the response to the grain.
* Then it waits 5 seconds for the `FarewellClientAsync()` operation, if it times out, it cancels the operation and completes.
* Both `GreetClientAsync()` and `FarewellClientAsync()` operations are idempotent, so the responses are persisted (in our concrete example, `FarewellClientAsync()` operation times out, so the fact that it was canceled is persisted).

![HelloActivity.xaml](https://github.com/OrleansContrib/Orleans.Activities/raw/docs-master/docs/HelloWorld/HelloActivity.png)

That's all. Ctrl+F5, and it works.

## Details

If you want to dig deep into the source and understand the detailed events in the background, this sequence diagram can help (this is not a completely valid diagram, but displaying every asnyc details, even the AsyncAutoResetEvent idle-queue, this would be 2 times bigger).

![HelloWorld-Details](https://github.com/OrleansContrib/Orleans.Activities/raw/docs-master/docs/HelloWorld/HelloWorld-Details.png)
