# Arithmetical

Shows how to execute the complete workflow like a method.

These are workflows that don't send and receive requests. They are executed like a method from start to completion. Though, they can initiate outgoing requests and can accept incoming callback requests. The main difference compared to the HelloWorld like workflows, is that these workflows don't start with accepting a request, they start by a direct `RunToCompletionAsync()` or `RunAsync()` call.

Typically all the necessary persistable state is in the workflows variables, based on input arguments. You can add several custom activities to manipulate the workflows variables.

## Overview

![Arithmetical-Adder-Overview](https://github.com/OrleansContrib/Orleans.Activities/raw/docs-master/docs/Arithmetical/Arithmetical-Adder-Overview.png)
![Arithmetical-Multiplier-Overview](https://github.com/OrleansContrib/Orleans.Activities/raw/docs-master/docs/Arithmetical/Arithmetical-Multiplier-Overview.png)

## Interfaces

The `IMultiplier` will use a callback call to deliver the result. This is a demonstration for a long running workflow. A real grain can use streams or callback other grains to deliver the result.

```c#
public interface IAdder : IGrainWithGuidKey
{
  Task<int> AddAsync(int arg1, int arg2);
}

public interface IMultiplier : IGrainWithGuidKey
{
  Task MultiplyAsync(int arg1, int arg2);

  Task SubscribeAsync(IMultiplierResultReceiver observer);
  Task UnsubscribeAsync(IMultiplierResultReceiver observer);
}

public interface IMultiplierResultReceiver : IGrainObserver
{
  void ReceiveResult(int result);
}
```

## Grains

In this sample we don't use custom `TGrainState`, `TWorkflowInterface` and `TWorkflowCallbackInterface` type parameters, we use the less generic `WorkflowGrain<TGrain, TGrainState>` base type with the default `WorkflowState` as `TGrainState`. But this is optional, the full blown `WorkflowGrain<TGrain, TGrainState, TWorkflowInterface, TWorkflowCallbackInterface>` base type can be used also if there are outgoing calls or incoming callbacks.

```c#
public sealed class AdderGrain : WorkflowGrain<AdderGrain, WorkflowState>, IAdder { ... }

public sealed class MultiplierGrain : WorkflowGrain<MultiplierGrain, WorkflowState>, IMultiplier { ... }
```

Typically the parameters of the grain methods become the input arguments of the workflow and the output arguments of the Completed workflow event get back to the caller. There are 2 versions:

* Method like execution: the output arguments become the return value of the method (__AdderGrain__).
* Execution with a callback to the client/caller grain: the output arguments become the callback method's argument (__MultiplierGrain__).

### Constructors

In the constructor we:

* Set the persistence mode to `Always`, because the default setting is to not save the workflow on the first idle, to immediately accept the incoming operation.
* Optionally add a `TrackingParticipant` extension to see what happens during the workflow execution with tracking.

```c#
private static Activity workflowDefinition = new AdderActivity();

public AdderGrain()
  : base((grainState, workflowIdentity) => workflowDefinition, null)
{
  Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Always);
  WorkflowControl.ExtensionsFactory = () => new GrainTrackingParticipant(GetLogger()).Yield();
}
```

The `MultiplierGrain` additionally sets the `CompletedAsync` event to send back the result to the client.

__NOTE:__ This sample can't demonstrate a failure during the callback (the observation is a one-way call), but the workflow persistence happens after the Completed workflow event: if the callback fails, the workflow will abort and continue from the last persisted state by a reactivation reminder. __Don't use callbacks on Completed event when there is no implicit or explicit persistence before__, because the incoming request that started the workflow and called `RunAsync()` will run the workflow to the first idle moment, if the first idle is the completion, the callback will happen during the incoming request (usually also a problem), and the exception during the callback will be propagated back to the caller and the caller has to repeat the incoming request to restart the workflow.

```c#
private static Activity workflowDefinition = new MultiplierActivity();

public MultiplierGrain()
  : base((grainState, workflowIdentity) => workflowDefinition, null)
{
  Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Always);
  WorkflowControl.ExtensionsFactory = () => new GrainTrackingParticipant(GetLogger()).Yield();

  WorkflowControl.CompletedAsync = (ActivityInstanceState activityInstanceState, IDictionary<string, object> outputArguments, Exception terminationException) =>
  {
    _subsManager.Notify(s => s.ReceiveResult((int)outputArguments["result"]));
    return Task.CompletedTask;
  };
}
```

### Unhandled exception handler

The mandatory (boilerplate) implementation of the unhandled exception handler (this is the same for both grain).

```c#
protected override Task OnUnhandledExceptionAsync(Exception exception, Activity source)
{
  GetLogger().TrackTrace($"OnUnhandledExceptionAsync: the workflow is going to {Parameters.UnhandledExceptionAction}\n\n{exception}", Runtime.Severity.Error);
  return Task.CompletedTask;
}
```

### Execution

`AdderGrain` executes the workflow during the incoming request like a method. If there is a failure before the workflow completes (yes completes, not before goes idle, due to `RunToCompletionAsync()` is called), it is propagated back to the caller. If the workflow persist itself but due to a failure it aborts later and propagates the exception back to the caller, it will be reloaded when the caller repeats the request or by a reactivation reminder. If the caller repeats the call only after the workflow is reloaded and completed, this not a problem, it will get the same output arguments or get `OperationCanceledException` or get the exception that caused the workflow to terminate.

__IMPORTANT:__ Do not copy values from the grain's `State` into the input arguments, because input arguments will be persisted by the workflow also. Closure directly the necessary values from the incoming public grain method call's parameters into the delegate.

```c#
public async Task<int> AddAsync(int arg1, int arg2)
{
  WorkflowControl.StartingAsync = () => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>()
  {
    { nameof(arg1), arg1 },
    { nameof(arg2), arg2 },
  });

  IDictionary<string, object> outputArguments = await WorkflowControl.RunToCompletionAsync();

  return (int)outputArguments["result"];
}
```

`MultiplierGrain` only executes the workflow until it gets idle, from that moment the workflow executes in the "background" and calls the `CompletedAsync` event when it completes.

```c#
public async Task MultiplyAsync(int arg1, int arg2)
{
  WorkflowControl.StartingAsync = () => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>()
  {
    { nameof(arg1), arg1 },
    { nameof(arg2), arg2 },
  });

  await WorkflowControl.RunAsync();
}
```

The subscription methods for the `MultiplierGrain` are the same as in the [Observers](http://dotnet.github.io/orleans/Documentation/Getting-Started-With-Orleans/Observers) Orleans sample.

## Workflows / Activities

### Adder

![AdderActivity.xaml](https://github.com/OrleansContrib/Orleans.Activities/raw/docs-master/docs/Arithmetical/AdderActivity.png)

### Multiplier

![AdderActivity.xaml](https://github.com/OrleansContrib/Orleans.Activities/raw/docs-master/docs/Arithmetical/MultiplierActivity.png)
