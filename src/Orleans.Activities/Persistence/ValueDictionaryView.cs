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
                if (keys == null)
                    keys = new List<XName>(instanceValues.Where(value => value.Value.IsWriteOnly() == writeOnly).Select(value => value.Key));
                return keys;
            }
        }
 
        public ICollection<object> Values
        {
            get
            {
                if (values == null)
                    values = new List<object>(instanceValues.Where(value => value.Value.IsWriteOnly() == writeOnly).Select(value => value.Value.Value));
                return values;
            }
        }
 
        public object this[XName key]
        {
            get
            {
                object value;
                if (!TryGetValue(key, out value))
                    throw new KeyNotFoundException();
                return value;
            }
            set
            {
                throw CreateReadOnlyException();
            }
        }

        public int Count => Keys.Count;

        public bool IsReadOnly => true;

        public void Add(XName key, object value)
        {
            throw CreateReadOnlyException();
        }
 
        public bool ContainsKey(XName key)
        {
            object dummy;
            return TryGetValue(key, out dummy);
        }
 
        public bool Remove(XName key)
        {
            throw CreateReadOnlyException();
        }
 
        public bool TryGetValue(XName key, out object value)
        {
            InstanceValue realValue;
            if (!instanceValues.TryGetValue(key, out realValue) || realValue.IsWriteOnly() != writeOnly)
            {
                value = null;
                return false;
            }
            value = realValue.Value;
            return true;
        }
 
        public void Add(KeyValuePair<XName, object> item)
        {
            throw CreateReadOnlyException();
        }
 
        public void Clear()
        {
            throw CreateReadOnlyException();
        }
 
        public bool Contains(KeyValuePair<XName, object> item)
        {
            object value;
            if (!TryGetValue(item.Key, out value))
                return false;
            return EqualityComparer<object>.Default.Equals(value, item.Value);
        }
 
        public void CopyTo(KeyValuePair<XName, object>[] array, int arrayIndex)
        {
            foreach (KeyValuePair<XName, object> entry in this)
                array[arrayIndex++] = entry;
        }
 
        public bool Remove(KeyValuePair<XName, object> item)
        {
            throw CreateReadOnlyException();
        }

        public IEnumerator<KeyValuePair<XName, object>> GetEnumerator() =>
            instanceValues.Where(value => value.Value.IsWriteOnly() == writeOnly).Select(value => new KeyValuePair<XName, object>(value.Key, value.Value.Value)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        public void ResetCaches()
        {
            keys = null;
            values = null;
        }

        private static Exception CreateReadOnlyException() => new InvalidOperationException("Dictionary is read only.");
    }
}
