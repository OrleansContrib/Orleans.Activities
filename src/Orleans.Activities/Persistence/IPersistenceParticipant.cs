using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml.Linq;

namespace Orleans.Activities.Persistence
{
    /// <summary>
    /// Public, interface equivalent of the <see cref="System.Activities.Persistence.PersistenceParticipant"/> abstract class.
    /// <para>The reimplemented <see cref="PersistencePipeline"/> supports both way (System.Activities and Orleans.Activities).</para>
    /// </summary>
    public interface IPersistenceParticipant
    {
        void CollectValues(out IDictionary<XName, object> readWriteValues, out IDictionary<XName, object> writeOnlyValues);

        IDictionary<XName, object> MapValues(IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues);

        void PublishValues(IDictionary<XName, object> readWriteValues);
    }

    /// <summary>
    /// Equivalent of the <see cref="System.Activities.Persistence.PersistenceParticipant"/> abstract class.
    /// <para>You don't need to use the abstract class, you can inherit from the interface also.</para>
    /// <para>The reimplemented <see cref="PersistencePipeline"/> supports both way (System.Activities and Orleans.Activities).</para>
    /// </summary>
    public abstract class PersistenceParticipant : IPersistenceParticipant
    {
        public virtual void CollectValues(out IDictionary<XName, object> readWriteValues, out IDictionary<XName, object> writeOnlyValues)
        {
            readWriteValues = null;
            writeOnlyValues = null;
        }

        public virtual IDictionary<XName, object> MapValues(IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues) => null;

        public virtual void PublishValues(IDictionary<XName, object> readWriteValues) { }
    }
}
