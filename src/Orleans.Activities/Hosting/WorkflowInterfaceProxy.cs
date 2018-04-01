using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Orleans.Activities.Helpers;

namespace Orleans.Activities.Hosting
{
    /// <summary>
    /// Nongeneric helper class for <see cref="WorkflowInterfaceProxy{TWorkflowInterface}"/>.
    /// </summary>
    public static class WorkflowInterfaceProxy
    {
        private static MethodInfo operationAsyncTRequestResultTResponseParameter;
        private static MethodInfo operationAsyncTRequestResult;
        private static MethodInfo operationAsyncTResponseParameter;
        private static MethodInfo operationAsync;

        static WorkflowInterfaceProxy()
        {
            foreach (var methodInfo in typeof(IWorkflowHostOperations).GetMethods())
                if (!methodInfo.ContainsGenericParameters)
                    operationAsync = methodInfo;
                else if (!methodInfo.ReturnType.ContainsGenericParameters)
                    operationAsyncTRequestResult = methodInfo;
                else if (methodInfo.GetGenericArguments().Length != 2)
                    operationAsyncTResponseParameter = methodInfo;
                else
                    operationAsyncTRequestResultTResponseParameter = methodInfo;
        }

        // Selects the appropriate operationAsync MethodInfo of workflow host (IWorkflowHostOperations) based on the signature of the TWorkflowInterface method.
        // The interfaceMethod parameter has to be a valid TWorkflowInterface interface method! Test TWorkflowInterface with WorkflowInterfaceInfo<TWorkflowInterface>.IsValidWorkflowInterface!
        public static MethodInfo GetWorkflowInterfaceMethodInfo(MethodInfo interfaceMethod)
        {
            var hasNoReturnType = interfaceMethod.ReturnType == typeof(Task);
            var hasNoParameterType = interfaceMethod.GetParameters()[0].ParameterType.GetGenericArguments()[0] == typeof(Task);
            if (hasNoReturnType)
                if (hasNoParameterType)
                    return operationAsync;
                else
                    return operationAsyncTRequestResult.MakeGenericMethod(new Type[] {
                        interfaceMethod.GetParameters()[0].ParameterType.GetGenericArguments()[0].GetGenericArguments()[0] });
            else
                if (hasNoParameterType)
                    return operationAsyncTResponseParameter.MakeGenericMethod(new Type[] {
                        interfaceMethod.ReturnType.GetGenericArguments()[0] });
                else
                    return operationAsyncTRequestResultTResponseParameter.MakeGenericMethod(new Type[] {
                        interfaceMethod.GetParameters()[0].ParameterType.GetGenericArguments()[0].GetGenericArguments()[0],
                        interfaceMethod.ReturnType.GetGenericArguments()[0] });
        }
    }

    /// <summary>
    /// Generates a proxy implementation for the TWorkflowInterface interface, that forwards the calls to the workflow host's (IWorkflowHostOperations) appropriate OperationAsync method.
    /// </summary>
    /// <typeparam name="TWorkflowInterface"></typeparam>
    public static class WorkflowInterfaceProxy<TWorkflowInterface> where TWorkflowInterface : class
    {
        private static Func<IWorkflowHostOperations, TWorkflowInterface> createProxy;

        // Generates (IL emit) class definitions for TWorkflowInterface interface like this.
        // The generated class is emitted at static run time.
        /*
        internal class WorkflowInterfaceImplementationForTWorkflowInterface : TWorkflowInterface
        {
            private IWorkflowHostOperations workflowHost;

            public WorkflowInterfaceImplementationForTWorkflowInterface(IWorkflowHostOperations workflowHost)
            {
                this.workflowHost = workflowHost;
            }

            // explicit implementations of the TWorkflowInterface members
            Task<...> ...(Func<Task<...>> requestResult) => workflowHost.OperationAsync<..., ...>("...", requestResult);
            Task ...(Func<Task<...>> requestResult) => workflowHost.OperationAsync<...>("...", requestResult);
            Task<...> ...(Func<Task> requestResult) => workflowHost.OperationAsync<...>("...", requestResult);
            Task ...(Func<Task> requestResult) => workflowHost.OperationAsync("...", requestResult);
            ...
        }
        */
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations")]
        static WorkflowInterfaceProxy()
        {
            if (!WorkflowInterfaceInfo<TWorkflowInterface>.IsValidWorkflowInterface)
                throw new InvalidProgramException(WorkflowInterfaceInfo<TWorkflowInterface>.ValidationMessage);

            var proxyName = "WorkflowInterfaceImplementationFor" + typeof(TWorkflowInterface).GetNongenericName();
            var proxyTypeBuilder = AppDomain
                .CurrentDomain
                .DefineDynamicAssembly(new AssemblyName(proxyName), AssemblyBuilderAccess.Run)
                .DefineDynamicModule(proxyName)
                .DefineType(proxyName, TypeAttributes.NotPublic);

            proxyTypeBuilder.AddInterfaceImplementation(typeof(TWorkflowInterface));

            FieldBuilder workflowHost = proxyTypeBuilder.DefineField(nameof(workflowHost), typeof(IWorkflowHostOperations), FieldAttributes.Private);

            EmitWorkflowInterfaceProxyConstructor(proxyTypeBuilder, workflowHost);

            foreach (var method in OperationInfo<TWorkflowInterface>.OperationMethods)
                EmitWorkflowInterfaceProxyMethod(proxyTypeBuilder, workflowHost, method);

            createProxy = CompileCreateProxyDelegate(proxyTypeBuilder.CreateType());
        }

        /// <summary>
        /// Creates a new instance of the proxy implementing TWorkflowInterface interface, that forwards the calls to the workflow host's (IWorkflowHostOperations) appropriate OperationAsync method.
        /// </summary>
        /// <param name="workflowHost"></param>
        /// <returns></returns>
        public static TWorkflowInterface CreateProxy(IWorkflowHostOperations workflowHost)
            => createProxy(workflowHost);

        private static void EmitWorkflowInterfaceProxyConstructor(TypeBuilder proxyTypeBuilder, FieldInfo workflowHost)
        {
            var ctorBuilder = proxyTypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { typeof(IWorkflowHostOperations) });
            var ctorIL = ctorBuilder.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Ldarg_1);
            ctorIL.Emit(OpCodes.Stfld, workflowHost);
            ctorIL.Emit(OpCodes.Ret);
        }

        private static void EmitWorkflowInterfaceProxyMethod(TypeBuilder proxyTypeBuilder, FieldInfo workflowHost, MethodInfo method)
        {
            var methodBuilder = proxyTypeBuilder.DefineMethod(method.DeclaringType.GetNongenericName() + "." + method.Name,
                MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final,
                method.ReturnType, new Type[] { method.GetParameters()[0].ParameterType });
            proxyTypeBuilder.DefineMethodOverride(methodBuilder, method);
            var methodIL = methodBuilder.GetILGenerator();
            methodIL.Emit(OpCodes.Ldarg_0);
            methodIL.Emit(OpCodes.Ldfld, workflowHost);
            methodIL.Emit(OpCodes.Ldstr, OperationInfo<TWorkflowInterface>.GetOperationName(method));
            methodIL.Emit(OpCodes.Ldarg_1);
            methodIL.Emit(OpCodes.Callvirt, WorkflowInterfaceProxy.GetWorkflowInterfaceMethodInfo(method));
            methodIL.Emit(OpCodes.Ret);
        }

        private static Func<IWorkflowHostOperations, TWorkflowInterface> CompileCreateProxyDelegate(Type proxyType)
        {
            var ctor = proxyType.GetConstructor(new Type[] { typeof(IWorkflowHostOperations) });
            ParameterExpression workflowHost = Expression.Parameter(typeof(IWorkflowHostOperations), nameof(workflowHost));
            return Expression.Lambda<Func<IWorkflowHostOperations, TWorkflowInterface>>(Expression.New(ctor, workflowHost), workflowHost).Compile() as Func<IWorkflowHostOperations, TWorkflowInterface>;
        }
    }
}
