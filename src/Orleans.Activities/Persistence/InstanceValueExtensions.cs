using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.DurableInstancing;

namespace Orleans.Activities.Persistence
{
    public static class InstanceValueExtensions
    {
        public static bool IsWriteOnly(this InstanceValue value) => (value.Options & InstanceValueOptions.WriteOnly) != 0;
    }
}
