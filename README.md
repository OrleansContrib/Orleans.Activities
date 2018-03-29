# ![Orleans logo](https://github.com/OrleansContrib/Orleans.Activities/raw/master/src/Orleans.Activities_64.png) Orleans.Activities

Workflow Foundation (.Net 4.x System.Activities workflows) over [Microsoft Orleans](https://github.com/dotnet/orleans) framework to provide stable, long-running, extremely scalable processes with XAML designer support.

[![Gitter](https://badges.gitter.im/join%20chat.svg)](https://gitter.im/dotnet/orleans)
[![Waffle](https://badge.waffle.io/OrleansContrib/Orleans.Activities.svg?columns=Triage)](http://waffle.io/OrleansContrib/Orleans.Activities)

__Stable:__
[![GitHub version](https://img.shields.io/github/tag/OrleansContrib/Orleans.Activities.svg)](https://github.com/OrleansContrib/Orleans.Activities/releases)
[![NuGet version](https://img.shields.io/nuget/v/Orleans.Activities.svg)](https://www.nuget.org/packages/Orleans.Activities)

__Master:__
[![Build status](https://ci.appveyor.com/api/projects/status/dy600wk9qn1fppqw/branch/master?svg=true)](https://ci.appveyor.com/project/OrleansContrib/orleans-activities/history)
[AppVeyor project feed (NuGet source)](https://ci.appveyor.com/nuget/orleans-activities-xqh82aku7sb3)

Guidelines
* [Branching Guidelines](https://github.com/OrleansContrib/Orleans.Activities/blob/docs-master/docs/Branching-Guidelines.md)
* [CI Guidelines](https://github.com/OrleansContrib/Orleans.Activities/blob/docs-master/docs/CI-Guidelines.md)
* [Coding Guidelines](https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md)
* [Design Guidelines](https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/framework-design-guidelines-digest.md)

This project is licensed under the [Apache License](https://github.com/OrleansContrib/Orleans.Activities/blob/master/LICENSE).

## Important

**Only in case of projects with designed XAML workflows!!!**

* Don’t use Microsoft.NET.Sdk, use old project format (Sdk has no XamlAppDef BuildAction)

*  Use at least VS 15.6 (older VS 15.x Activity designer crashes when Microsoft.Orleans.Core.Abstractions is installed via NuGet 4.x PackageReference tag)


## ~~Documentation~~
* see the [Samples](https://github.com/OrleansContrib/Orleans.Activities#samples) below, they come with tutorial level, detailed descriptions
* or see my [Presentations](https://github.com/lmagyar/Presentations) repo

## Concept

### Why?

The key concepts behind Workflow Foundation and Microsoft Orleans are very similar, but Microsoft Orleans solves the pain points of Workflow Foundation (WCF and SQL oriented, non-scalable).

Workflow Foundation||Microsoft Orleans
:-:|:-:|:-:
✓ | Single threaded | ✓
✓ | Persistent reminders | ✓
✓ | Stateful | ✓
`😐` | Communication | ✓
`😐` | Storage | ✓
`✗` | Scalability | ✓

### How?

We kept only the the "good parts" of Workflow Foundation, the `WorkflowInstance` (a mini Virtual Machine executing the Activities) and replaced everything else with Microsoft Orleans, ie. we replaced `WorkflowServiceHost` with Microsoft Orleans `Grains` (stateful actors).

![Concept-How](https://github.com/OrleansContrib/Orleans.Activities/raw/docs-master/docs/Orleans.Activities-Concept.How.png)

Integrated:

* Persistence (compatible with legacy workflow extensions)
* Reminders (compatible with legacy Delay activities)
* Tracking
* Designer & Debugger support
* Nearly all legacy activities are supported (except TransactionScope and WCF messaging activities)

### Internals

![Overview](https://github.com/OrleansContrib/Orleans.Activities/raw/docs-master/docs/Orleans.Activities-Overview.png)

This is a very high level view:

* Each `WorkflowGrain` is indistinguishable from a normal grain and backed by a `WorkflowHost`.
* The `WorkflowHost` is responsible to handle the lifecycle of the `WorkflowInstance`, mainly recreate it from a previous persisted state when it aborts.
* The communication between the `WorkflowGrain` and the `WorkflowHost` is based on 2 developer defined interfaces for the incoming and outgoing requests (`TWorkflowInterface` and `TWorkflowCallbackInterface`).
  * These interfaces provide the type safe communication with the workflow.
  * Their methods can be referenced from the workflow activities to accept incoming or to initiate outgoing requests.
* The methods of the `TWorkflowInterface` and `TWorkflowCallbackInterface` are independent from the grain's external public interface, you can merge different public requests into one method or vice versa. Or a reentrant grain even can execute (read-only) public interface methods independently from the current running workflow operations.
* The method's signatures are restricted, their parameters and return values are lazy, async delegates with 1 optional parameter/return value. The delegates executed by the workflow activities if/when they accept them (command pattern).
* There are design-, build- and static-run-time checks to keep the interfaces and the workflows in sync.
* Though you can execute complete workflows as methods also.

### Result

A typical a workflow grain ("domain service") manages operations in other "normal" grain(s) ("aggregate root") and handles only the process specific data in it's own state.
* "Normal" grains have long life, and even can have transactioned API or event sourced state.
* Workflow grains have a one-shot lifetime, the long lived transaction they implement.

![Concept-Result](https://github.com/OrleansContrib/Orleans.Activities/raw/docs-master/docs/Orleans.Activities-Concept.Result.png)

The goal, is to keep the C# code in the grain, and use the workflow only to decide what to do next. This way we can avoid a steep learning curve to use workflows: the developer doesn't need to write or to understand anything about activities, he/she can build workflows with the provided activities in a designer.

## Functionality

Extra implemented features:

* TAP async API
* Optionally idempotent request processing for forward recovery
* Automatic reactivation after failure
* Workflow can be persisted during processing an incoming request (ReceiveRequestSendResponseScope is __not__ an implicit NoPersistScope)
* Executing code "in the background" on the tail of the request after the request returns it's response
* Workflow is informed whether it is running in a reloaded state after failure (to determine necessary recovery)
* Notification participant extensions (to get notified when the workflow is idle)

Under construction:

* Tests (currently semi manual, semi automatic MSTest, don't even look at them)
* More elaborate sample with
  * DI/Autofac
  * Strategy and Humble Object patterns, to show an architecture, where the application logic can be tested independently from Orleans and from Orleans.Activities workflows

Not implemented, help wanted (for design and for implementation):

* DynamicUpdateMap support (updating loaded workflows to a newer definition version), though the separation of the application logic (the plain C# delegates) and the process (the diagram) results in a very simple workflow diagram, that has a big chance you won't need to update when it runs
* See all [Help Wanted issues](https://github.com/OrleansContrib/Orleans.Activities/issues?q=is%3Aopen+is%3Aissue+label%3A%22Status-Help+Wanted%22)


## Samples

[HelloWorld](https://github.com/OrleansContrib/Orleans.Activities/blob/docs-master/docs/HelloWorld/HelloWorld.md) - How to communicate with the workflow through custom interfaces.

[Arithmetical](https://github.com/OrleansContrib/Orleans.Activities/blob/docs-master/docs/Arithmetical/Arithmetical.md) - How to execute the complete workflow like a method.

## Details

**You don't need to understand this to use the project! This is for those who want to dig in to the source!**

This is still an overview, all the details of the classes are hidden. The goal is to give a map to understand the relations between the classes.

The two most important classes on the below diagram are the 2 generated proxies, that translate the calls between the `TWorkflowInterface` and `TWorkflowCallbackInterface` interfaces and the workflow's API, where the methods are identified by their names.

For more details see the detailed comments in the source!

![Overview](https://github.com/OrleansContrib/Orleans.Activities/raw/docs-master/docs/Orleans.Activities-Details.png)
