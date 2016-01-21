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
    public interface ITestGrainAffector : Affector1A.ITestAffector, Affector1B.ITestAffector { }
    namespace Affector1A
    {
        public interface ITestAffector
        {
            Task TestAffectorOperationAsync();
        }
    }
    namespace Affector1B
    {
        public interface ITestAffector
        {
            Task TestAffectorOperationAsync();
        }
    }
    public interface ITestGrainEffector : ITestEffector1 { }
    public interface ITestEffector1
    {
        Task TestEffectorOperationAsync();
    }

    public interface ITestAffectorBase
    {
        Task<string> SayHello1(Func<Task<string>> requestResult);
        Task SayHello2(Func<Task<string>> requestResult);
        Task<string> SayHello3(Func<Task> requestResult);
        Task SayHello4(Func<Task> requestResult);
    }
    public interface ITestAffector2 : ITestAffectorBase
    {
        Task SayHello44(Func<Task> requestResult);
    }

    public class Affector : IAffectorOperations
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

    public interface ITestEffectorBase
    {
        Task<Func<Task<string>>> SayHello1(string requestParameter);
        Task<Func<Task>> SayHello2(string requestParameter);
        Task<Func<Task<string>>> SayHello3();
        Task<Func<Task>> SayHello4();
    }
    public interface ITestEffector2 : ITestEffectorBase
    {
        Task<Func<Task>> SayHello44();
    }

    public class Effector : ITestEffector2
    {
        Task<Func<Task<string>>> ITestEffectorBase.SayHello1(string requestParameter)
        {
            Console.WriteLine($"ITestEffectorBase.SayHello1í({requestParameter})");
            return Task.FromResult<Func<Task<string>>>(() => TaskConstants.StringEmpty);
        }

        Task<Func<Task>> ITestEffectorBase.SayHello2(string requestParameter)
        {
            Console.WriteLine($"ITestEffectorBase.SayHello2({requestParameter})");
            return Task.FromResult<Func<Task>>(() => TaskConstants.BooleanFalse);
        }

        Task<Func<Task<string>>> ITestEffectorBase.SayHello3()
        {
            Console.WriteLine("ITestEffectorBase.SayHello3");
            return Task.FromResult<Func<Task<string>>>(() => TaskConstants.StringEmpty);
        }

        Task<Func<Task>> ITestEffectorBase.SayHello4()
        {
            Console.WriteLine("ITestEffectorBase.SayHello4");
            return Task.FromResult<Func<Task>>(() => Task.FromResult(false));
        }

        Task<Func<Task>> ITestEffector2.SayHello44()
        {
            Console.WriteLine("ITestEffector2.SayHello44");
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
            foreach (var name in OperationInfo.GetOperationNames(typeof(IWorkflowActivity<ITestGrainAffector, ITestGrainEffector>).GetGenericArguments()[0]))
                Console.WriteLine(name);
            foreach (var name in OperationInfo.GetOperationNames(typeof(IWorkflowActivity<ITestGrainAffector, ITestGrainEffector>).GetGenericArguments()[1]))
                Console.WriteLine(name);
        }

        [TestMethod]
        public void AffectorProxy()
        {
            ITestAffector2 proxy = AffectorProxy<ITestAffector2>.CreateProxy(new Affector());
            proxy.SayHello1(null);
            proxy.SayHello2(null);
            proxy.SayHello3(null);
            proxy.SayHello4(null);
            proxy.SayHello44(null);
            proxy = AffectorProxy<ITestAffector2>.CreateProxy(new Affector());
            proxy.SayHello1(null);
            proxy.SayHello2(null);
            proxy.SayHello3(null);
            proxy.SayHello4(null);
            proxy.SayHello44(null);
            ITestAffectorBase proxyBase = AffectorProxy<ITestAffectorBase>.CreateProxy(new Affector());
            proxyBase.SayHello1(null);
            proxyBase.SayHello2(null);
            proxyBase.SayHello3(null);
            proxyBase.SayHello4(null);
        }

        [TestMethod]
        public void EffectorProxy()
        {
            IEffectorOperations proxy = EffectorProxy<ITestEffector2>.CreateProxy(new Effector());

            proxy.OnOperationAsync<string, string>("ITestEffectorBase.SayHello1", "foo").Result();
            proxy.OnOperationAsync<string>("ITestEffectorBase.SayHello2", "foo").Result();
            proxy.OnOperationAsync<string>("ITestEffectorBase.SayHello3").Result();
            proxy.OnOperationAsync("ITestEffectorBase.SayHello4").Result();
            proxy.OnOperationAsync("ITestEffector2.SayHello44").Result();
        }
    }
}