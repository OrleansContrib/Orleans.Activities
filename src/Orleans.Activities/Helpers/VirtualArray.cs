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
        private Dictionary<TKey, TValue> values = new Dictionary<TKey, TValue>();

        public TValue this[TKey key]
        {
            get
            {
                if (!this.values.TryGetValue(key, out var value))
                {
                    value = new TValue();
                    this.values.Add(key, value);
                }
                return value;
            }
        }

        public bool TryGetValue(TKey key, out TValue value) => this.values.TryGetValue(key, out value);

        public void Clear() => this.values.Clear();
    }
}
