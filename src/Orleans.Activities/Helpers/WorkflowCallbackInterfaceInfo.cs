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

        static WorkflowCallbackInterfaceInfo()
        {
            List<string> validationMessages = new List<string>();

            if (!typeof(TWorkflowCallbackInterface).IsInterface)
                validationMessages.Add($"TWorkflowCallbackInterface type '{typeof(TWorkflowCallbackInterface).GetFriendlyName()}' must be an interface!");

            // Valid method signatures:
            // Task<Func<Task<...>>> ...(... requestParameter)
            // Task<Func<Task>> ...(... requestParameter)
            // Task<Func<Task<...>>> ...()
            // Task<Func<Task>> ...()
            foreach (MethodInfo method in OperationInfo<TWorkflowCallbackInterface>.OperationMethods)
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length > 1)
                    validationMessages.Add($"TWorkflowCallbackInterface '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' can have max. 1 parameter (of any type)!");
                Type returnType = method.ReturnType;
                if (returnType != typeof(Task<Func<Task>>)
                    && (!returnType.IsGenericTypeOf(typeof(Task<>))
                        || !returnType.GetGenericArguments()[0].IsGenericTypeOf(typeof(Func<>))
                        || !returnType.GetGenericArguments()[0].GetGenericArguments()[0].IsGenericTypeOf(typeof(Task<>))))
                    validationMessages.Add($"TWorkflowCallbackInterface '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' return type must be Task<Func<Task>> or Task<Func<Task<...>>>!");
            }

            IsValidWorkflowCallbackInterface = validationMessages.Count() == 0;
            if (!IsValidWorkflowCallbackInterface)
            {
                StringBuilder sb = new StringBuilder();
                foreach (string validationMessage in validationMessages)
                    sb.AppendLine(validationMessage);
                ValidationMessage = sb.ToString();
            }
        }
    }
}
