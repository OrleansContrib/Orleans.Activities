using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml.Linq;
using Orleans.Activities.AsyncEx;

namespace Orleans.Activities.Persistence
{
    /// <summary>
    /// Public, interface equivalent of the <see cref="System.Activities.Persistence.PersistenceIOParticipant"/> abstract class.
    /// <para>The reimplemented <see cref="PersistencePipeline"/> supports both way (System.Activities and Orleans.Activities),
    /// but with IPersistenceIOParticipant you can implement TAP async methods, and the pipeline doesn't need to use APM Begin/End wrappers around them.</para>
    /// <para>It contains an extra method: OnSavedAsync(), called after the persistence was successful.</para>
    /// </summary>
    public interface IPersistenceIOParticipant : IPersistenceParticipant
    {
        Task OnSaveAsync(IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues, TimeSpan timeout);

        Task OnSavedAsync(TimeSpan timeout);

        Task OnLoadAsync(IDictionary<XName, object> readWriteValues, TimeSpan timeout);

        void Abort();
    }

    /// <summary>
    /// Equivalent of the <see cref="System.Activities.Persistence.PersistenceIOParticipant"/> abstract class.
    /// <para>You don't need to use the abstract class, you can inherit from the interface also.</para>
    /// <para>The reimplemented <see cref="PersistencePipeline"/> supports both way (System.Activities and Orleans.Activities),
    /// but with PersistenceIOParticipant you can implement TAP async methods, and the pipeline doesn't need to use APM Begin/End wrappers around them.</para>
    /// <para>It contains an extra method: OnSavedAsync(), called after the persistence was successful.</para>
    /// </summary>
    public abstract class PersistenceIOParticipant : PersistenceParticipant, IPersistenceIOParticipant
    {
        public virtual Task OnSaveAsync(IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues, TimeSpan timeout) => TaskConstants.Completed;

        public virtual Task OnSavedAsync(TimeSpan timeout) => TaskConstants.Completed;

        public virtual Task OnLoadAsync(IDictionary<XName, object> readWriteValues, TimeSpan timeout) => TaskConstants.Completed;

        public virtual void Abort() { }
    }
}
