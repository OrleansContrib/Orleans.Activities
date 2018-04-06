using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;

namespace Orleans.Activities.Helpers
{
    /// <summary>
    /// Validates the TWorkflowInterface interface's methods.
    /// </summary>
    /// <typeparam name="TWorkflowInterface"></typeparam>
    public static class WorkflowInterfaceInfo<TWorkflowInterface>
    {
        public static bool IsValidWorkflowInterface { get; private set; }
        public static string ValidationMessage { get; private set; }

        private static VirtualArray<Type, VirtualArray<Type, List<string>>> operationNames;

        static WorkflowInterfaceInfo()
        {
            operationNames = new VirtualArray<Type, VirtualArray<Type, List<string>>>();

            var validationMessages = new List<string>();

            if (!typeof(TWorkflowInterface).IsInterface)
                validationMessages.Add($"TWorkflowInterface type '{typeof(TWorkflowInterface).GetFriendlyName()}' must be an interface!");

            // Valid method signatures:
            // Task <...> ...(Func<Task<...>> requestResult)
            // Task ...(Func<Task<...>> requestResult)
            // Task <...> ...(Func<Task> requestResult)
            // Task ...(Func<Task> requestResult)
            foreach (var method in OperationInfo<TWorkflowInterface>.OperationMethods)
            {
                var responseParameterType = typeof(void);
                var returnType = method.ReturnType;
                if (returnType != typeof(Task)
                    && !returnType.IsGenericTypeOf(typeof(Task<>)))
                    validationMessages.Add($"TWorkflowInterface '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' return type must be Task or Task<...>!");
                else if (returnType.IsGenericType)
                    responseParameterType = returnType.GetGenericArguments()[0];

                var requestResultType = typeof(void);
                var parameters = method.GetParameters();
                if (parameters.Length != 1)
                    validationMessages.Add($"TWorkflowInterface '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' must have 1 parameter!");
                if (parameters.Length >= 1)
                {
                    var parameterType = parameters[0].ParameterType;
                    if (parameterType != typeof(Func<Task>)
                        && (!parameterType.IsGenericTypeOf(typeof(Func<>))
                            || !parameterType.GetGenericArguments()[0].IsGenericTypeOf(typeof(Task<>))))
                        validationMessages.Add($"TWorkflowInterface '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' parameter type must be Func<Task> or Func<Task<...>>!");
                    else if (parameterType.GetGenericArguments()[0].IsGenericType)
                        requestResultType = parameterType.GetGenericArguments()[0].GetGenericArguments()[0];
                }

                operationNames[requestResultType][responseParameterType].Add(OperationInfo<TWorkflowInterface>.GetOperationName(method));
            }

            IsValidWorkflowInterface = validationMessages.Count() == 0;
            if (!IsValidWorkflowInterface)
            {
                var sb = new StringBuilder();
                foreach (var validationMessage in validationMessages)
                    sb.AppendLine(validationMessage);
                ValidationMessage = sb.ToString();

                operationNames.Clear();
            }
        }

        public static IEnumerable<string> GetOperationNames(Type requestResultType, Type responseParameterType)
        {
            if (!operationNames.TryGetValue(requestResultType, out var responseParameterTypeArray)
                || !responseParameterTypeArray.TryGetValue(responseParameterType, out var operationNameList))
                return Enumerable.Empty<string>();
            else
                return operationNameList;
        }
    }
}
