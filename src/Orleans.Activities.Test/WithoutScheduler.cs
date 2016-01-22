using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Activities.Hosting;
using System.Threading.Tasks;
using Orleans.Activities.Configuration;
using Orleans.Activities.Helpers;
using Orleans.Activities.Hosting;
using Orleans.Activities.AsyncEx;

namespace Orleans.Activities.Test
{
    public interface ITestWorkflowInterface : TestWorkflowInterface1A.ITestWorkflowInterface, TestWorkflowInterface1B.ITestWorkflowInterface { }
    namespace TestWorkflowInterface1A
    {
        public interface ITestWorkflowInterface
        {
            Task TestWorkflowInterfaceOperationAsync();
        }
    }
    namespace TestWorkflowInterface1B
    {
        public interface ITestWorkflowInterface
        {
            Task TestWorkflowInterfaceOperationAsync();
        }
    }
    public interface ITestWorkflowCallbackInterface : ITestWorkflowCallbackInterface1 { }
    public interface ITestWorkflowCallbackInterface1
    {
        Task TestWorkflowCallbackInterfaceOperationAsync();
    }

    public interface ITestWorkflowInterfaceBase
    {
        Task<string> SayHello1(Func<Task<string>> requestResult);
        Task SayHello2(Func<Task<string>> requestResult);
        Task<string> SayHello3(Func<Task> requestResult);
        Task SayHello4(Func<Task> requestResult);
    }
    public interface ITestWorkflowInterface2 : ITestWorkflowInterfaceBase
    {
        Task SayHello44(Func<Task> requestResult);
    }

    public class WorkflowInterface : IWorkflowHostOperations
    {
        public Task<TResponseParameter> OperationAsync<TRequestResult, TResponseParameter>(string operationName, Func<Task<TRequestResult>> requestResult)
            where TRequestResult : class
            where TResponseParameter : class
        {
            Console.WriteLine("1: " + operationName);
            return TaskConstants<TResponseParameter>.Default;
        }

        public Task OperationAsync<TRequestResult>(string operationName, Func<Task<TRequestResult>> requestResult)
            where TRequestResult : class
        {
            Console.WriteLine("2: " + operationName);
            return TaskConstants.Completed;
        }

        public Task<TResponseParameter> OperationAsync<TResponseParameter>(string operationName, Func<Task> requestResult)
            where TResponseParameter : class
        {
            Console.WriteLine("3: " + operationName);
            return TaskConstants<TResponseParameter>.Default;
        }

        public Task OperationAsync(string operationName, Func<Task> requestResult)
        {
            Console.WriteLine("4: " + operationName);
            return TaskConstants.Completed;
        }
    }

    public interface ITestWorkflowCallbackInterfaceBase
    {
        Task<Func<Task<string>>> SayHello1(string requestParameter);
        Task<Func<Task>> SayHello2(string requestParameter);
        Task<Func<Task<string>>> SayHello3();
        Task<Func<Task>> SayHello4();
    }
    public interface ITestWorkflowCallbackInterface2 : ITestWorkflowCallbackInterfaceBase
    {
        Task<Func<Task>> SayHello44();
    }

    public class WorkflowCallbackInterface : ITestWorkflowCallbackInterface2
    {
        Task<Func<Task<string>>> ITestWorkflowCallbackInterfaceBase.SayHello1(string requestParameter)
        {
            Console.WriteLine($"ITestWorkflowCallbackInterfaceBase.SayHello1({requestParameter})");
            return Task.FromResult<Func<Task<string>>>(() => TaskConstants.StringEmpty);
        }

        Task<Func<Task>> ITestWorkflowCallbackInterfaceBase.SayHello2(string requestParameter)
        {
            Console.WriteLine($"ITestWorkflowCallbackInterfaceBase.SayHello2({requestParameter})");
            return Task.FromResult<Func<Task>>(() => TaskConstants.BooleanFalse);
        }

        Task<Func<Task<string>>> ITestWorkflowCallbackInterfaceBase.SayHello3()
        {
            Console.WriteLine("ITestWorkflowCallbackInterfaceBase.SayHello3");
            return Task.FromResult<Func<Task<string>>>(() => TaskConstants.StringEmpty);
        }

        Task<Func<Task>> ITestWorkflowCallbackInterfaceBase.SayHello4()
        {
            Console.WriteLine("ITestWorkflowCallbackInterfaceBase.SayHello4");
            return Task.FromResult<Func<Task>>(() => Task.FromResult(false));
        }

        Task<Func<Task>> ITestWorkflowCallbackInterface2.SayHello44()
        {
            Console.WriteLine("ITestWorkflowCallbackInterface2.SayHello44");
            return Task.FromResult<Func<Task>>(() => Task.FromResult(false));
        }
    }

    [TestClass]
    public class WithoutScheduler
    {
        [TestMethod]
        public void IdlePersistenceModeShouldSave()
        {
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.Never, WorkflowInstanceState.Aborted, isStarting: false));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.Never, WorkflowInstanceState.Runnable, isStarting: false));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.Never, WorkflowInstanceState.Idle, isStarting: false));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.Never, WorkflowInstanceState.Idle, isStarting: true));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.Never, WorkflowInstanceState.Complete, isStarting: false));

            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnCompleted, WorkflowInstanceState.Aborted, isStarting: false));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnCompleted, WorkflowInstanceState.Runnable, isStarting: false));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnCompleted, WorkflowInstanceState.Idle, isStarting: false));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnCompleted, WorkflowInstanceState.Idle, isStarting: true));
            Assert.IsTrue(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnCompleted, WorkflowInstanceState.Complete, isStarting: false));

            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnPersistableIdle, WorkflowInstanceState.Aborted, isStarting: false));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnPersistableIdle, WorkflowInstanceState.Runnable, isStarting: false));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnPersistableIdle, WorkflowInstanceState.Idle, isStarting: true));
            Assert.IsTrue(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnPersistableIdle, WorkflowInstanceState.Idle, isStarting: false));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnPersistableIdle, WorkflowInstanceState.Complete, isStarting: false));

            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnStart, WorkflowInstanceState.Aborted, isStarting: false));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnStart, WorkflowInstanceState.Runnable, isStarting: false));
            Assert.IsTrue(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnStart, WorkflowInstanceState.Idle, isStarting: true));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnStart, WorkflowInstanceState.Idle, isStarting: false));
            Assert.IsFalse(IdlePersistenceModeExtensions.ShouldSave(IdlePersistenceMode.OnStart, WorkflowInstanceState.Complete, isStarting: false));
        }

        [TestMethod]
        public void TypeExtensions()
        {
            foreach (var name in OperationInfo.GetOperationNames(typeof(IWorkflowActivity<ITestWorkflowInterface, ITestWorkflowCallbackInterface>).GetGenericArguments()[0]))
                Console.WriteLine(name);
            foreach (var name in OperationInfo.GetOperationNames(typeof(IWorkflowActivity<ITestWorkflowInterface, ITestWorkflowCallbackInterface>).GetGenericArguments()[1]))
                Console.WriteLine(name);
        }

        [TestMethod]
        public void WorkflowInterfaceProxy()
        {
            ITestWorkflowInterface2 proxy = WorkflowInterfaceProxy<ITestWorkflowInterface2>.CreateProxy(new WorkflowInterface());
            proxy.SayHello1(null);
            proxy.SayHello2(null);
            proxy.SayHello3(null);
            proxy.SayHello4(null);
            proxy.SayHello44(null);
            proxy = WorkflowInterfaceProxy<ITestWorkflowInterface2>.CreateProxy(new WorkflowInterface());
            proxy.SayHello1(null);
            proxy.SayHello2(null);
            proxy.SayHello3(null);
            proxy.SayHello4(null);
            proxy.SayHello44(null);
            ITestWorkflowInterfaceBase proxyBase = WorkflowInterfaceProxy<ITestWorkflowInterfaceBase>.CreateProxy(new WorkflowInterface());
            proxyBase.SayHello1(null);
            proxyBase.SayHello2(null);
            proxyBase.SayHello3(null);
            proxyBase.SayHello4(null);
        }

        [TestMethod]
        public void WorkflowCallbackInterfaceProxy()
        {
            IWorkflowHostCallbackOperations proxy = WorkflowCallbackInterfaceProxy<ITestWorkflowCallbackInterface2>.CreateProxy(new WorkflowCallbackInterface());

            proxy.OnOperationAsync<string, string>("ITestWorkflowCallbackInterfaceBase.SayHello1", "foo").Result();
            proxy.OnOperationAsync<string>("ITestWorkflowCallbackInterfaceBase.SayHello2", "foo").Result();
            proxy.OnOperationAsync<string>("ITestWorkflowCallbackInterfaceBase.SayHello3").Result();
            proxy.OnOperationAsync("ITestWorkflowCallbackInterfaceBase.SayHello4").Result();
            proxy.OnOperationAsync("ITestWorkflowCallbackInterface2.SayHello44").Result();
        }
    }
}