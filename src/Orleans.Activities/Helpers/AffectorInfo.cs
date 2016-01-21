using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;

namespace Orleans.Activities.Helpers
{
    /// <summary>
    /// Validates the TAffector interface's methods.
    /// </summary>
    /// <typeparam name="TAffector"></typeparam>
    public static class AffectorInfo<TAffector>
    {
        public static bool IsValidAffectorInterface { get; }
        public static string ValidationMessage { get; }

        static AffectorInfo()
        {
            List<string> validationMessages = new List<string>();

            if (!typeof(TAffector).IsInterface)
                validationMessages.Add($"TAffector type '{typeof(TAffector).GetFriendlyName()}' must be an interface!");

            // Valid method signatures:
            // Task <...> ...(Func<Task<...>> requestResult)
            // Task ...(Func<Task<...>> requestResult)
            // Task <...> ...(Func<Task> requestResult)
            // Task ...(Func<Task> requestResult)
            foreach (MethodInfo method in OperationInfo<TAffector>.OperationMethods)
            {
                Type returnType = method.ReturnType;
                if (returnType != typeof(Task)
                    && !returnType.IsGenericTypeOf(typeof(Task<>)))
                    validationMessages.Add($"TAffector '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' return type must be Task or Task<...>!");
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1)
                    validationMessages.Add($"TAffector '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' must have 1 parameter!");
                if (parameters.Length >= 1)
                {
                    Type parameterType = parameters[0].ParameterType;
                    if (parameterType != typeof(Func<Task>)
                        && (!parameterType.IsGenericTypeOf(typeof(Func<>))
                            || !parameterType.GetGenericArguments()[0].IsGenericTypeOf(typeof(Task<>))))
                        validationMessages.Add($"TAffector '{method.DeclaringType.GetFriendlyName()}' method '{method.Name}' parameter type must be Func<Task> or Func<Task<...>>!");
                }
            }

            IsValidAffectorInterface = validationMessages.Count() == 0;
            if (!IsValidAffectorInterface)
            {
                StringBuilder sb = new StringBuilder();
                foreach (string validationMessage in validationMessages)
                    sb.AppendLine(validationMessage);
                ValidationMessage = sb.ToString();
            }
        }
    }
}
