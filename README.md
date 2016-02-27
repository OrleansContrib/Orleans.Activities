# Orleans.Activities

![Orleans logo](https://raw.githubusercontent.com/OrleansContrib/Orleans.Activities/master/src/Orleans.Activities.png)

Workflow Foundation (.Net 4.x System.Activities workflows) over Microsoft.Orleans framework to provide stable, long-running, extremely scalable processes with XAML designer support.

__Stable:__
[![GitHub version](https://badge.fury.io/gh/OrleansContrib%2FOrleans.Activities.svg)](https://badge.fury.io/gh/OrleansContrib%2FOrleans.Activities)
[![NuGet version](https://badge.fury.io/nu/Orleans.Activities.svg)](https://badge.fury.io/nu/Orleans.Activities)

__Master:__
[![Build status](https://ci.appveyor.com/api/projects/status/dy600wk9qn1fppqw/branch/master?svg=true)](https://ci.appveyor.com/project/OrleansContrib/orleans-activities)
[AppVeyor project feed (NuGet source)](https://ci.appveyor.com/nuget/orleans-activities-xqh82aku7sb3)

__Stats:__
[![Issue Stats PR](http://www.issuestats.com/github/OrleansContrib/Orleans.Activities/badge/pr)](http://www.issuestats.com/github/OrleansContrib/Orleans.Activities)
[![Issue Stats Issues](http://www.issuestats.com/github/OrleansContrib/Orleans.Activities/badge/issue)](http://www.issuestats.com/github/OrleansContrib/Orleans.Activities)

__Issues:__
[![Help Wanted (filtered view)](https://badge.waffle.io/OrleansContrib/Orleans.Activities.svg?label=Status-Help%20Wanted&title=Help%20Wanted%20%28filtered%20view%29)](http://waffle.io/OrleansContrib/Orleans.Activities?label=Status-Help%20Wanted)
[![Up for Grabs (filtered view)](https://badge.waffle.io/OrleansContrib/Orleans.Activities.svg?label=Status-Up%20for%20Grabs&title=Up%20for%20Grabs%20%28filtered%20view%29)](http://waffle.io/OrleansContrib/Orleans.Activities?label=Status-Up%20for%20Grabs)
[![Ready](https://badge.waffle.io/OrleansContrib/Orleans.Activities.svg?label=Phase-Ready&title=Ready)](http://waffle.io/OrleansContrib/Orleans.Activities)
[![In Progress](https://badge.waffle.io/OrleansContrib/Orleans.Activities.svg?label=Phase-In%20Progress&title=In%20Progress)](http://waffle.io/OrleansContrib/Orleans.Activities)

~~Documentation~~ (see [Samples](https://github.com/OrleansContrib/Orleans.Activities#samples) below)

[Branching Guidelines](https://github.com/OrleansContrib/Orleans.Activities/blob/docs-master/docs/Branching-Guidelines.md)

[Coding Guidelines](https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md)

[Design Guidelines](https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/framework-design-guidelines-digest.md)

This project is licensed under the [Apache License](https://github.com/OrleansContrib/Orleans.Activities/blob/master/LICENSE).

## Concept

![Overview](https://raw.githubusercontent.com/OrleansContrib/Orleans.Activities/docs-master/docs/Orleans.Activities-Overview.png)

This is a very high level view:

* Each WorkflowGrain is indistinguishable from a normal grain and backed by a WorkflowHost.
* The WorkflowHost is responsible to handle the lifecycle of the WorkflowInstance, mainly recreate it from a previous persisted state when it aborts.
* The communication between the WorkflowGrain and the WorkflowHost is based on 2 developer defined interfaces for the incoming and outgoing requests (TWorkflowInterface and TWorkflowCallbackInterface). These interfaces' methods can be referenced from the workflow activities to accept incoming or to initiate outgoing requests.
* The methods of the TWorkflowInterface and TWorkflowCallbackInterface are independent from the grain's external public interface, you can merge different public requests into one method or vice versa. Or a reentrant grain even can execute (read-only) public interface methods independently from the current running workflow operations.
* The method's signatures are restricted, their parameters and return values are lazy, async delegates with 1 optional parameter/return value. The delegates executed by the workflow activities if/when they accept them (command pattern).
* There are design-, build- and static-run-time checks to keep the interfaces and the workflows in sync.
* Though you can execute complete workflows as methods also.

A typical workflow grain manages operations in other grain(s) and handles only the process specific data in it's own state.

The goal, is to keep the C# code in the grain, and use the workflow only to decide what to do next. This way we can avoid a steep learning curve to use workflows: the developer doesn't need to write or to understand anything about activities, he/she can build workflows with the provided activities in a designer.

If it's needed, a mainly computational workflow can be executed also, even without any incoming or outgoing request.

## Functionality

Implemented:

* Persistence (compatible with legacy workflow extensions)
* Reminders (compatible with legacy Delay activities, though 1 min. is the minimum)
* Tracking
* Designer support
* Nearly all legacy activities are supported (except TransactionScope and messaging activities)

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
* TransactionScope activity support (see https://github.com/dotnet/orleans/issues/1090)
* See all [Help Wanted issues](http://waffle.io/OrleansContrib/Orleans.Activities?label=Status-Help%20Wanted) (filtered view)

And there are nearly unlimited [Open issues](http://waffle.io/OrleansContrib/Orleans.Activities)...

## Samples

[HelloWorld](https://github.com/OrleansContrib/Orleans.Activities/blob/docs-master/docs/HelloWorld/HelloWorld.md) - How to communicate with the workflow through custom interfaces.

[Arithmetical](https://github.com/OrleansContrib/Orleans.Activities/blob/docs-master/docs/Arithmetical/Arithmetical.md) - How to execute the complete workflow like a method.

## Details

This is still an overview, all the details of the classes are hidden. The goal is to give a map to understand the relations between the classes. See the comments in the source!

![Overview](https://raw.githubusercontent.com/OrleansContrib/Orleans.Activities/docs-master/docs/Orleans.Activities-Details.png)
