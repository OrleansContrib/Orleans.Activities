using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities.Helpers
{
    public class VirtualArray<TKey, TValue>
        where TValue : new()
    {
        private Dictionary<TKey, TValue> values;

        public VirtualArray()
        {
            values = new Dictionary<TKey, TValue>();
        }

        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (!values.TryGetValue(key, out value))
                {
                    value = new TValue();
                    values.Add(key, value);
                }
                return value;
            }
        }

        public bool TryGetValue(TKey key, out TValue value) =>
            values.TryGetValue(key, out value);

        public void Clear()
        {
            values.Clear();
        }
    }
}
