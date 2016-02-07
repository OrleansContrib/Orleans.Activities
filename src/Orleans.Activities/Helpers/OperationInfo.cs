using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;

namespace Orleans.Activities.Helpers
{
    /// <summary>
    /// Generates operation names for valid TWorkflowInterface or TWorkflowCallbackInterface interfaces.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class OperationInfo<T>
    {
        private static bool isNamespaceRequiredForOperationNames;

        public static IEnumerable<MethodInfo> OperationMethods { get; }

        static OperationInfo()
        {
            Type[] interfaces = typeof(T).GetInterfaces();
            ISet<string> interfaceNames = new HashSet<string>();
            interfaceNames.Add(nameof(T));
            isNamespaceRequiredForOperationNames = interfaces.Any((i) => !interfaceNames.Add(i.GetNongenericName()));

            MethodInfo[] operationMethods =
                typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Concat(
                typeof(T).GetInterfaces().SelectMany((i) => i.GetMethods(BindingFlags.Instance | BindingFlags.Public)))
                .ToArray();

            OperationMethods = operationMethods;
        }

        public static string GetOperationName(MethodInfo method)
        {
            string interfaceName = method.DeclaringType.GetNongenericName();
            return (isNamespaceRequiredForOperationNames ? method.DeclaringType.Namespace + "." + interfaceName : interfaceName) + "." + method.Name;
        }
    }
}
