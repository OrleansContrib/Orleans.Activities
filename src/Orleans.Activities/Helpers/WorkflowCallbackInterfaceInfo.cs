using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;

namespace Orleans.Activities.Helpers
{
    /// <summary>
    /// Validates the TWorkflowCallbackInterface interface's methods.
    /// </summary>
    /// <typeparam name="TWorkflowCallbackInterface"></typeparam>
    public static class WorkflowCallbackInterfaceInfo<TWorkflowCallbackInterface>
    {
        public static bool IsValidWorkflowCallbackInterface { get; }
        public static string ValidationMessage { get; }

        private static VirtualArray<Type, VirtualArray<Type, List<string>>> operationNames;

        static WorkflowCallbackInterfaceInfo()
        {
            operationNames = new VirtualArray<Type, VirtualArray<Type, List<string>>>();

            var validationMessages = new List<string>();

            if (!typeof(TWorkflowCallbackInterface).IsInterface)
                validationMessages.Add($"TWorkflowCallbackInterface type '{typeof(TWorkflowCallbackInterface).GetFriendlyName()}' must be an interface!");

            // Valid method signatures:
            // Task<Func<Task<...>>> ...(... requestParameter)
            // Task<Func<Task>> ...(... requestParameter)
            // Task<Func<Task<...>>> ...()
            // Task<Func<Task>> ...()
            foreach (var method in OperationInfo<TWorkflowCallbackInterface>.OperationMethods)
            {
                var requestParameterType = typeof(void);
                var parameters = method.GetParameters();
                if (parameters.Length > 1)
                    validationMessages.Add($"TWorkflowCallbackInterface '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' can have max. 1 parameter (of any type)!");
                else if (parameters.Length == 1)
                    requestParameterType = parameters[0].ParameterType;

                var responseResultType = typeof(void);
                var returnType = method.ReturnType;
                if (returnType != typeof(Task<Func<Task>>)
                    && (!returnType.IsGenericTypeOf(typeof(Task<>))
                        || !returnType.GetGenericArguments()[0].IsGenericTypeOf(typeof(Func<>))
                        || !returnType.GetGenericArguments()[0].GetGenericArguments()[0].IsGenericTypeOf(typeof(Task<>))))
                    validationMessages.Add($"TWorkflowCallbackInterface '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' return type must be Task<Func<Task>> or Task<Func<Task<...>>>!");
                else if (returnType.GetGenericArguments()[0].GetGenericArguments()[0].IsGenericType)
                    responseResultType = returnType.GetGenericArguments()[0].GetGenericArguments()[0].GetGenericArguments()[0];

                operationNames[requestParameterType][responseResultType].Add(OperationInfo<TWorkflowCallbackInterface>.GetOperationName(method));
            }

            IsValidWorkflowCallbackInterface = validationMessages.Count() == 0;
            if (!IsValidWorkflowCallbackInterface)
            {
                var sb = new StringBuilder();
                foreach (var validationMessage in validationMessages)
                    sb.AppendLine(validationMessage);
                ValidationMessage = sb.ToString();

                operationNames.Clear();
            }
        }

        public static IEnumerable<string> GetOperationNames(Type requestParameterType, Type responseResultType)
        {
            if (!operationNames.TryGetValue(requestParameterType, out var  responseResultTypeArray)
                || !responseResultTypeArray.TryGetValue(responseResultType, out var operationNameList))
                return Enumerable.Empty<string>();
            else
                return operationNameList;
        }
    }
}
