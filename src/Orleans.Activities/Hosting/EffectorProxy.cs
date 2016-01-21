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
    /// Generates a proxy implementation for the IEffectorOperations interface (called by the workflow host) that forwards the calls to the TEffector interface's appropriate method.
    /// </summary>
    /// <typeparam name="TEffector"></typeparam>
    public static class EffectorProxy<TEffector> where TEffector : class
    {
        // This is a de facto switch statement where the TEffector's method name (operationName) is the selection value.
        private static Dictionary<string, Delegate> onOperationAsyncDelegates;

        // Type safe wrapper around the "switch" dictionary.
        private class EffectorProxyImplementation : IEffectorOperations
        {
            private TEffector effector;

            public EffectorProxyImplementation(TEffector effector)
            {
                this.effector = effector;
            }

            public Task<Func<Task<TResponseResult>>> OnOperationAsync<TRequestParameter, TResponseResult>(string operationName, TRequestParameter requestParameter)
                where TRequestParameter : class
                where TResponseResult : class
            {
                Delegate operationDelegate;
                Func<TEffector, TRequestParameter, Task<Func<Task<TResponseResult>>>> operation = null;
                if (onOperationAsyncDelegates.TryGetValue(operationName, out operationDelegate))
                    operation = operationDelegate as Func<TEffector, TRequestParameter, Task<Func<Task<TResponseResult>>>>;
                if (operation == null)
                    throw new InvalidOperationException(operationName);
                return operation(effector, requestParameter);
            }

            public Task<Func<Task>> OnOperationAsync<TRequestParameter>(string operationName, TRequestParameter requestParameter)
                where TRequestParameter : class
            {
                Delegate operationDelegate;
                Func<TEffector, TRequestParameter, Task<Func<Task>>> operation = null;
                if (onOperationAsyncDelegates.TryGetValue(operationName, out operationDelegate))
                    operation = operationDelegate as Func<TEffector, TRequestParameter, Task<Func<Task>>>;
                if (operation == null)
                    throw new InvalidOperationException(operationName);
                return operation(effector, requestParameter);
            }

            public Task<Func<Task<TResponseResult>>> OnOperationAsync<TResponseResult>(string operationName)
                where TResponseResult : class
            {
                Delegate operationDelegate;
                Func<TEffector, Task<Func<Task<TResponseResult>>>> operation = null;
                if (onOperationAsyncDelegates.TryGetValue(operationName, out operationDelegate))
                    operation = operationDelegate as Func<TEffector, Task<Func<Task<TResponseResult>>>>;
                if (operation == null)
                    throw new InvalidOperationException(operationName);
                return operation(effector);
            }

            public Task<Func<Task>> OnOperationAsync(string operationName)
            {
                Delegate operationDelegate;
                Func<TEffector, Task<Func<Task>>> operation = null;
                if (onOperationAsyncDelegates.TryGetValue(operationName, out operationDelegate))
                    operation = operationDelegate as Func<TEffector, Task<Func<Task>>>;
                if (operation == null)
                    throw new InvalidOperationException(operationName);
                return operation(effector);
            }
        }

        // Generates the delegates in to the "switch" dictionary.
        // The delegates in the dictionary are compiled at static run time.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations")]
        static EffectorProxy()
        {
            if (!EffectorInfo<TEffector>.IsValidEffectorInterface)
                throw new InvalidProgramException(EffectorInfo<TEffector>.ValidationMessage);

            onOperationAsyncDelegates = new Dictionary<string, Delegate>();
            foreach (MethodInfo method in OperationInfo<TEffector>.OperationMethods)
                onOperationAsyncDelegates.Add(OperationInfo<TEffector>.GetOperationName(method), GetEffectorDelegate(method));
        }

        /// <summary>
        /// Creates a new instance of the proxy implementing the IEffectorOperations interface (called by the workflow host) that forwards the calls to the TEffector interface's appropriate method.
        /// </summary>
        /// <param name="effector"></param>
        /// <returns></returns>
        public static IEffectorOperations CreateProxy(TEffector effector) =>
            new EffectorProxyImplementation(effector);

        // Compiles a delegate with the proper signature that forwards the calls to the TEffector interface's appropriate method.
        // The interfaceMethod parameter has to be a valid TEffector interface method! Test TEffector with EffectorInfo<TEffector>.IsValidEffectorInterface!
        private static Delegate GetEffectorDelegate(MethodInfo interfaceMethod)
        {
            bool hasNoReturnType = interfaceMethod.ReturnType == typeof(Task<Func<Task>>);
            bool hasNoParameterType = interfaceMethod.GetParameters().Length == 0;
            if (hasNoReturnType)
                if (hasNoParameterType)
                    return CompileEffectorDelegate(interfaceMethod);
                else
                    return CompileEffectorDelegateTRequestParameter(interfaceMethod,
                        interfaceMethod.GetParameters()[0].ParameterType);
            else
                if (hasNoParameterType)
                    return CompileEffectorDelegateTResponseResult(interfaceMethod,
                        interfaceMethod.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0].GetGenericArguments()[0]);
                else
                    return CompileEffectorDelegateTRequestParameterTResponseResult(interfaceMethod,
                        interfaceMethod.GetParameters()[0].ParameterType,
                        interfaceMethod.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0].GetGenericArguments()[0]);
        }

        private static Delegate CompileEffectorDelegateTRequestParameterTResponseResult(MethodInfo method, Type requestParameterType, Type responseResultType) =>
            CompileEffectorDelegate(typeof(Func<,,>).MakeGenericType(typeof(TEffector), requestParameterType, GetTaskFuncTaskResponseResultType(responseResultType)), method, requestParameterType);

        private static Delegate CompileEffectorDelegateTRequestParameter(MethodInfo method, Type requestParameterType) =>
            CompileEffectorDelegate(typeof(Func<,,>).MakeGenericType(typeof(TEffector), requestParameterType, typeof(Task<Func<Task>>)), method, requestParameterType);

        private static Delegate CompileEffectorDelegate(Type delegateType, MethodInfo method, Type requestParameterType)
        {
            ParameterExpression instance = Expression.Parameter(method.DeclaringType, "this");
            ParameterExpression requestParameter = Expression.Parameter(requestParameterType, nameof(requestParameter));
            return Expression.Lambda(delegateType, Expression.Call(instance, method, requestParameter), true, instance, requestParameter).Compile();
        }

        private static Delegate CompileEffectorDelegateTResponseResult(MethodInfo method, Type responseResultType) =>
            CompileEffectorDelegate(typeof(Func<,>).MakeGenericType(typeof(TEffector), GetTaskFuncTaskResponseResultType(responseResultType)), method);

        private static Delegate CompileEffectorDelegate(MethodInfo method) =>
            CompileEffectorDelegate(typeof(Func<TEffector, Task<Func<Task>>>), method);

        private static Delegate CompileEffectorDelegate(Type delegateType, MethodInfo method)
        {
            ParameterExpression instance = Expression.Parameter(method.DeclaringType, "this");
            return Expression.Lambda(delegateType, Expression.Call(instance, method), true, instance).Compile();
        }

        private static Type GetTaskFuncTaskResponseResultType(Type responseResultType) =>
            typeof(Task<>).MakeGenericType(typeof(Func<>).MakeGenericType(typeof(Task<>).MakeGenericType(responseResultType)));
    }
}
