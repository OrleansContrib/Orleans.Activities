using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;

namespace Orleans.Activities.Helpers
{
    /// <summary>
    /// Validates the TEffector interface's methods.
    /// </summary>
    /// <typeparam name="TEffector"></typeparam>
    public static class EffectorInfo<TEffector>
    {
        public static bool IsValidEffectorInterface { get; }
        public static string ValidationMessage { get; }

        static EffectorInfo()
        {
            List<string> validationMessages = new List<string>();

            if (!typeof(TEffector).IsInterface)
                validationMessages.Add($"TEffector type '{typeof(TEffector).GetFriendlyName()}' must be an interface!");

            // Valid method signatures:
            // Task<Func<Task<...>>> ...(... requestParameter)
            // Task<Func<Task>> ...(... requestParameter)
            // Task<Func<Task<...>>> ...()
            // Task<Func<Task>> ...()
            foreach (MethodInfo method in OperationInfo<TEffector>.OperationMethods)
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length > 1)
                    validationMessages.Add($"TEffector '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' can have max. 1 parameter (of any type)!");
                Type returnType = method.ReturnType;
                if (returnType != typeof(Task<Func<Task>>)
                    && (!returnType.IsGenericTypeOf(typeof(Task<>))
                        || !returnType.GetGenericArguments()[0].IsGenericTypeOf(typeof(Func<>))
                        || !returnType.GetGenericArguments()[0].GetGenericArguments()[0].IsGenericTypeOf(typeof(Task<>))))
                    validationMessages.Add($"TEffector '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' return type must be Task<Func<Task>> or Task<Func<Task<...>>>!");
            }

            IsValidEffectorInterface = validationMessages.Count() == 0;
            if (!IsValidEffectorInterface)
            {
                StringBuilder sb = new StringBuilder();
                foreach (string validationMessage in validationMessages)
                    sb.AppendLine(validationMessage);
                ValidationMessage = sb.ToString();
            }
        }
    }
}
