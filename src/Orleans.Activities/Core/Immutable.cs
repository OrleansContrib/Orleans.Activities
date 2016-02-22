using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities
{
    public static class Immutable
    {
        public static void Set<T>(ref T field, T value, string name)
            where T : class
        {
            if (value == null)
                throw new ArgumentNullException(name);
            if (field != null && value != field)
                throw new ArgumentException("Argument value differs from the already set value of the property.", name);
            if (field == null)
                field = value;
        }

        public static T Get<T>(T field, string name)
            where T : class
        {
            if (field == null)
                throw new InvalidOperationException($"Property '{name}' is not initialized, it's value is null.");
            return field;
        }
    }
}
