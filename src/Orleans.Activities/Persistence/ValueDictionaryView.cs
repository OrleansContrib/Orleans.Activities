using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections;
using System.Runtime.DurableInstancing;
using System.Xml.Linq;

namespace Orleans.Activities.Persistence
{
    /// <summary>
    /// Helper class for PersistencePipeline.
    /// </summary>
    public class ValueDictionaryView : IDictionary<XName, object>
    {
        private IDictionary<XName, InstanceValue> instanceValues;
        private bool writeOnly;

        private List<XName> keys;
        private List<object> values;

        public ValueDictionaryView(IDictionary<XName, InstanceValue> instanceValues, bool writeOnly)
        {
            this.instanceValues = instanceValues;
            this.writeOnly = writeOnly;
        }
 
        public ICollection<XName> Keys
        {
            get
            {
                if (this.keys == null)
                    this.keys = new List<XName>(this.instanceValues.Where(value => value.Value.IsWriteOnly() == this.writeOnly).Select(value => value.Key));
                return this.keys;
            }
        }
 
        public ICollection<object> Values
        {
            get
            {
                if (this.values == null)
                    this.values = new List<object>(this.instanceValues.Where(value => value.Value.IsWriteOnly() == this.writeOnly).Select(value => value.Value.Value));
                return this.values;
            }
        }
 
        public object this[XName key]
        {
            get => !TryGetValue(key, out var value) ? throw new KeyNotFoundException() : value;
            set => throw CreateReadOnlyException();
        }

        public int Count => this.Keys.Count;

        public bool IsReadOnly => true;

        public void Add(XName key, object value) => throw CreateReadOnlyException();
 
        public bool ContainsKey(XName key) => TryGetValue(key, out var _);
 
        public bool Remove(XName key) => throw CreateReadOnlyException();
 
        public bool TryGetValue(XName key, out object value)
        {
            if (!this.instanceValues.TryGetValue(key, out var realValue) || realValue.IsWriteOnly() != this.writeOnly)
            {
                value = null;
                return false;
            }
            value = realValue.Value;
            return true;
        }
 
        public void Add(KeyValuePair<XName, object> item) => throw CreateReadOnlyException();
 
        public void Clear() => throw CreateReadOnlyException();
 
        public bool Contains(KeyValuePair<XName, object> item)
            => !TryGetValue(item.Key, out var value) ? false : EqualityComparer<object>.Default.Equals(value, item.Value);
 
        public void CopyTo(KeyValuePair<XName, object>[] array, int arrayIndex)
        {
            foreach (var entry in this)
                array[arrayIndex++] = entry;
        }
 
        public bool Remove(KeyValuePair<XName, object> item) => throw CreateReadOnlyException();

        public IEnumerator<KeyValuePair<XName, object>> GetEnumerator()
            => this.instanceValues.Where(value => value.Value.IsWriteOnly() == this.writeOnly).Select(value => new KeyValuePair<XName, object>(value.Key, value.Value.Value)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void ResetCaches()
        {
            this.keys = null;
            this.values = null;
        }

        private static Exception CreateReadOnlyException() => new InvalidOperationException("Dictionary is read only.");
    }
}
