using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Linq.Expressions;
using System.Reflection;
using Orleans.Activities.Helpers;

namespace Orleans.Activities.Hosting
{
    /// <summary>
    /// Generates a proxy implementation for the IWorkflowHostCallbackOperations interface (called by the workflow host) that forwards the calls to the TWorkflowCallbackInterface interface's appropriate method.
    /// </summary>
    /// <typeparam name="TWorkflowCallbackInterface"></typeparam>
    public static class WorkflowCallbackInterfaceProxy<TWorkflowCallbackInterface> where TWorkflowCallbackInterface : class
    {
        // This is a de facto switch statement where the TWorkflowCallbackInterface's method name (operationName) is the selection value.
        private static Dictionary<string, Delegate> onOperationAsyncDelegates;

        // Type safe wrapper around the "switch" dictionary.
        private class WorkflowCallbackInterfaceImplementation : IWorkflowHostCallbackOperations
        {
            private TWorkflowCallbackInterface grain;

            public WorkflowCallbackInterfaceImplementation(TWorkflowCallbackInterface grain)
            {
                this.grain = grain;
            }

            public Task<Func<Task<TResponseResult>>> OnOperationAsync<TRequestParameter, TResponseResult>(string operationName, TRequestParameter requestParameter)
            {
                Delegate operationDelegate;
                Func<TWorkflowCallbackInterface, TRequestParameter, Task<Func<Task<TResponseResult>>>> operation = null;
                if (onOperationAsyncDelegates.TryGetValue(operationName, out operationDelegate))
                    operation = operationDelegate as Func<TWorkflowCallbackInterface, TRequestParameter, Task<Func<Task<TResponseResult>>>>;
                if (operation == null)
                    throw new InvalidOperationException(operationName);
                return operation(grain, requestParameter);
            }

            public Task<Func<Task>> OnOperationAsync<TRequestParameter>(string operationName, TRequestParameter requestParameter)
            {
                Delegate operationDelegate;
                Func<TWorkflowCallbackInterface, TRequestParameter, Task<Func<Task>>> operation = null;
                if (onOperationAsyncDelegates.TryGetValue(operationName, out operationDelegate))
                    operation = operationDelegate as Func<TWorkflowCallbackInterface, TRequestParameter, Task<Func<Task>>>;
                if (operation == null)
                    throw new InvalidOperationException(operationName);
                return operation(grain, requestParameter);
            }

            public Task<Func<Task<TResponseResult>>> OnOperationAsync<TResponseResult>(string operationName)
            {
                Delegate operationDelegate;
                Func<TWorkflowCallbackInterface, Task<Func<Task<TResponseResult>>>> operation = null;
                if (onOperationAsyncDelegates.TryGetValue(operationName, out operationDelegate))
                    operation = operationDelegate as Func<TWorkflowCallbackInterface, Task<Func<Task<TResponseResult>>>>;
                if (operation == null)
                    throw new InvalidOperationException(operationName);
                return operation(grain);
            }

            public Task<Func<Task>> OnOperationAsync(string operationName)
            {
                Delegate operationDelegate;
                Func<TWorkflowCallbackInterface, Task<Func<Task>>> operation = null;
                if (onOperationAsyncDelegates.TryGetValue(operationName, out operationDelegate))
                    operation = operationDelegate as Func<TWorkflowCallbackInterface, Task<Func<Task>>>;
                if (operation == null)
                    throw new InvalidOperationException(operationName);
                return operation(grain);
            }
        }

        // Generates the delegates in to the "switch" dictionary.
        // The delegates in the dictionary are compiled at static run time.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations")]
        static WorkflowCallbackInterfaceProxy()
        {
            if (!WorkflowCallbackInterfaceInfo<TWorkflowCallbackInterface>.IsValidWorkflowCallbackInterface)
                throw new InvalidProgramException(WorkflowCallbackInterfaceInfo<TWorkflowCallbackInterface>.ValidationMessage);

            onOperationAsyncDelegates = new Dictionary<string, Delegate>();
            foreach (MethodInfo method in OperationInfo<TWorkflowCallbackInterface>.OperationMethods)
                onOperationAsyncDelegates.Add(OperationInfo<TWorkflowCallbackInterface>.GetOperationName(method), GetWorkflowCallbackInterfaceDelegate(method));
        }

        /// <summary>
        /// Creates a new instance of the proxy implementing the IWorkflowHostCallbackOperations interface (called by the workflow host) that forwards the calls to the TWorkflowCallbackInterface interface's appropriate method.
        /// </summary>
        /// <param name="workflowCallbackInterface"></param>
        /// <returns></returns>
        public static IWorkflowHostCallbackOperations CreateProxy(TWorkflowCallbackInterface workflowCallbackInterface) =>
            new WorkflowCallbackInterfaceImplementation(workflowCallbackInterface);

        // Compiles a delegate with the proper signature that forwards the calls to the TWorkflowCallbackInterface interface's appropriate method.
        // The interfaceMethod parameter has to be a valid TWorkflowCallbackInterface interface method! Test TWorkflowCallbackInterface with WorkflowCallbackInterfaceInfo<TWorkflowCallbackInterface>.IsValidWorkflowCallbackInterface!
        private static Delegate GetWorkflowCallbackInterfaceDelegate(MethodInfo interfaceMethod)
        {
            bool hasNoReturnType = interfaceMethod.ReturnType == typeof(Task<Func<Task>>);
            bool hasNoParameterType = interfaceMethod.GetParameters().Length == 0;
            if (hasNoReturnType)
                if (hasNoParameterType)
                    return CompileWorkflowCallbackInterfaceDelegate(interfaceMethod);
                else
                    return CompileWorkflowCallbackInterfaceDelegateTRequestParameter(interfaceMethod,
                        interfaceMethod.GetParameters()[0].ParameterType);
            else
                if (hasNoParameterType)
                    return CompileWorkflowCallbackInterfaceDelegateTResponseResult(interfaceMethod,
                        interfaceMethod.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0].GetGenericArguments()[0]);
                else
                    return CompileWorkflowCallbackInterfaceDelegateTRequestParameterTResponseResult(interfaceMethod,
                        interfaceMethod.GetParameters()[0].ParameterType,
                        interfaceMethod.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0].GetGenericArguments()[0]);
        }

        private static Delegate CompileWorkflowCallbackInterfaceDelegateTRequestParameterTResponseResult(MethodInfo method, Type requestParameterType, Type responseResultType) =>
            CompileWorkflowCallbackInterfaceDelegate(typeof(Func<,,>).MakeGenericType(typeof(TWorkflowCallbackInterface), requestParameterType, GetTaskFuncTaskResponseResultType(responseResultType)), method, requestParameterType);

        private static Delegate CompileWorkflowCallbackInterfaceDelegateTRequestParameter(MethodInfo method, Type requestParameterType) =>
            CompileWorkflowCallbackInterfaceDelegate(typeof(Func<,,>).MakeGenericType(typeof(TWorkflowCallbackInterface), requestParameterType, typeof(Task<Func<Task>>)), method, requestParameterType);

        private static Delegate CompileWorkflowCallbackInterfaceDelegate(Type delegateType, MethodInfo method, Type requestParameterType)
        {
            ParameterExpression instance = Expression.Parameter(method.DeclaringType, "this");
            ParameterExpression requestParameter = Expression.Parameter(requestParameterType, nameof(requestParameter));
            return Expression.Lambda(delegateType, Expression.Call(instance, method, requestParameter), true, instance, requestParameter).Compile();
        }

        private static Delegate CompileWorkflowCallbackInterfaceDelegateTResponseResult(MethodInfo method, Type responseResultType) =>
            CompileWorkflowCallbackInterfaceDelegate(typeof(Func<,>).MakeGenericType(typeof(TWorkflowCallbackInterface), GetTaskFuncTaskResponseResultType(responseResultType)), method);

        private static Delegate CompileWorkflowCallbackInterfaceDelegate(MethodInfo method) =>
            CompileWorkflowCallbackInterfaceDelegate(typeof(Func<TWorkflowCallbackInterface, Task<Func<Task>>>), method);

        private static Delegate CompileWorkflowCallbackInterfaceDelegate(Type delegateType, MethodInfo method)
        {
            ParameterExpression instance = Expression.Parameter(method.DeclaringType, "this");
            return Expression.Lambda(delegateType, Expression.Call(instance, method), true, instance).Compile();
        }

        private static Type GetTaskFuncTaskResponseResultType(Type responseResultType) =>
            typeof(Task<>).MakeGenericType(typeof(Func<>).MakeGenericType(typeof(Task<>).MakeGenericType(responseResultType)));
    }
}
