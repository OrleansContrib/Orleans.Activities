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
    /// Nongeneric helper class for <see cref="AffectorProxy{TAffector}"/>.
    /// </summary>
    public static class AffectorProxy
    {
        private static MethodInfo operationAsyncTRequestResultTResponseParameter;
        private static MethodInfo operationAsyncTRequestResult;
        private static MethodInfo operationAsyncTResponseParameter;
        private static MethodInfo operationAsync;

        static AffectorProxy()
        {
            foreach (MethodInfo methodInfo in typeof(IAffectorOperations).GetMethods())
                if (!methodInfo.ContainsGenericParameters)
                    operationAsync = methodInfo;
                else if (!methodInfo.ReturnType.ContainsGenericParameters)
                    operationAsyncTRequestResult = methodInfo;
                else if (methodInfo.GetGenericArguments().Length != 2)
                    operationAsyncTResponseParameter = methodInfo;
                else
                    operationAsyncTRequestResultTResponseParameter = methodInfo;
        }

        // Selects the appropriate operationAsync MethodInfo of workflow host (IAffectorOperations) based on the signature of the TAffector method.
        // The interfaceMethod parameter has to be a valid TAffector interface method! Test TAffector with AffectorInfo<TAffector>.IsValidAffectorInterface!
        public static MethodInfo GetAffectorMethodInfo(MethodInfo interfaceMethod)
        {
            bool hasNoReturnType = interfaceMethod.ReturnType == typeof(Task);
            bool hasNoParameterType = interfaceMethod.GetParameters()[0].ParameterType.GetGenericArguments()[0] == typeof(Task);
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
    /// Generates a proxy implementation for the TAffector interface, that forwards the calls to the workflow host's (IAffectorOperations) appropriate OperationAsync method.
    /// </summary>
    /// <typeparam name="TAffector"></typeparam>
    public static class AffectorProxy<TAffector> where TAffector : class
    {
        private static Func<IAffectorOperations, TAffector> createProxy;

        // Generates (IL emit) class definitions for TAffector interface like this.
        // The generated class is emitted at static run time.
        /*
        internal class AffectorProxyImplementationForTAffector : TAffector
        {
            private IAffectorOperations affectorOperations;

            public AffectorProxyForTAffector(IAffectorOperations affectorOperations)
            {
                this.affectorOperations = affectorOperations;
            }

            // explicit implementations of the TAffector members
            Task<...> ...(Func<Task<...>> requestResult) => affectorOperations.OperationAsync<..., ...>("...", requestResult);
            Task ...(Func<Task<...>> requestResult) => affectorOperations.OperationAsync<...>("...", requestResult);
            Task<...> ...(Func<Task> requestResult) => affectorOperations.OperationAsync<...>("...", requestResult);
            Task ...(Func<Task> requestResult) => affectorOperations.OperationAsync("...", requestResult);
            ...
        }
        */
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations")]
        static AffectorProxy()
        {
            if (!AffectorInfo<TAffector>.IsValidAffectorInterface)
                throw new InvalidProgramException(AffectorInfo<TAffector>.ValidationMessage);

            string proxyName = nameof(AffectorProxy) + "ImplementationFor" + typeof(TAffector).GetNongenericName();
            TypeBuilder proxyTypeBuilder = AppDomain
                .CurrentDomain
                .DefineDynamicAssembly(new AssemblyName(proxyName), AssemblyBuilderAccess.Run)
                .DefineDynamicModule(proxyName)
                .DefineType(proxyName, TypeAttributes.NotPublic);

            proxyTypeBuilder.AddInterfaceImplementation(typeof(TAffector));

            FieldBuilder affectorOperations = proxyTypeBuilder.DefineField(nameof(affectorOperations), typeof(IAffectorOperations), FieldAttributes.Private);

            EmitAffectorProxyConstructor(proxyTypeBuilder, affectorOperations);

            foreach (MethodInfo method in OperationInfo<TAffector>.OperationMethods)
                EmitAffectorProxyMethod(proxyTypeBuilder, affectorOperations, method);

            createProxy = CompileCreateProxyDelegate(proxyTypeBuilder.CreateType());
        }

        /// <summary>
        /// Creates a new instance of the proxy implementing TAffector interface, that forwards the calls to the workflow host's (IAffectorOperations) appropriate OperationAsync method.
        /// </summary>
        /// <param name="affectorOperations"></param>
        /// <returns></returns>
        public static TAffector CreateProxy(IAffectorOperations affectorOperations) =>
            createProxy(affectorOperations);

        private static void EmitAffectorProxyConstructor(TypeBuilder proxyTypeBuilder, FieldInfo affectorOperations)
        {
            ConstructorBuilder ctorBuilder = proxyTypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { typeof(IAffectorOperations) });
            ILGenerator ctorIL = ctorBuilder.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Ldarg_1);
            ctorIL.Emit(OpCodes.Stfld, affectorOperations);
            ctorIL.Emit(OpCodes.Ret);
        }

        private static void EmitAffectorProxyMethod(TypeBuilder proxyTypeBuilder, FieldInfo affectorOperations, MethodInfo method)
        {
            MethodBuilder methodBuilder = proxyTypeBuilder.DefineMethod(method.DeclaringType.GetNongenericName() + "." + method.Name,
                MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final,
                method.ReturnType, new Type[] { method.GetParameters()[0].ParameterType });
            proxyTypeBuilder.DefineMethodOverride(methodBuilder, method);
            ILGenerator methodIL = methodBuilder.GetILGenerator();
            methodIL.Emit(OpCodes.Ldarg_0);
            methodIL.Emit(OpCodes.Ldfld, affectorOperations);
            methodIL.Emit(OpCodes.Ldstr, OperationInfo<TAffector>.GetOperationName(method));
            methodIL.Emit(OpCodes.Ldarg_1);
            methodIL.Emit(OpCodes.Callvirt, AffectorProxy.GetAffectorMethodInfo(method));
            methodIL.Emit(OpCodes.Ret);
        }

        private static Func<IAffectorOperations, TAffector> CompileCreateProxyDelegate(Type proxyType)
        {
            ConstructorInfo ctor = proxyType.GetConstructor(new Type[] { typeof(IAffectorOperations) });
            ParameterExpression affectorOperations = Expression.Parameter(typeof(IAffectorOperations), nameof(affectorOperations));
            return Expression.Lambda<Func<IAffectorOperations, TAffector>>(Expression.New(ctor, affectorOperations), affectorOperations).Compile() as Func<IAffectorOperations, TAffector>;
        }
    }
}
