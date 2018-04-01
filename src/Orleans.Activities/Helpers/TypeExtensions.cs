using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities.Helpers
{
    public static class TypeExtensions
    {
        public static string GetNongenericName(this Type type)
            => type.IsGenericType
                ? type.Name.Remove(type.Name.IndexOf('`'))
                : type.Name;

        public static string GetFriendlyName(this Type type)
            => type.IsGenericType
                ? type.Name.Remove(type.Name.IndexOf('`')) + "<" + string.Join(", ", type.GetGenericArguments().Select(x => x.GetFriendlyName())) + ">"
                : type.Name;

        public static bool IsGenericTypeOf(this Type type, Type genericTypeDefinition)
            => type.IsGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition;
    }
}
