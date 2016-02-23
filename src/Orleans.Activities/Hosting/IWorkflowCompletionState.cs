using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.DurableInstancing;
using System.Xml.Linq;
using Orleans.Activities.Configuration;

namespace Orleans.Activities.Hosting
{
    public interface IWorkflowCompletionState
    {
        Task LoadAsync(IDictionary<XName, InstanceValue> instanceValues, IEnumerable<object> extensions, IParameters parameters);

        /// <summary>
        /// In case of Canceled or Faulted completionState it will throw the terminationException or the OperationCanceledException.
        /// </summary>
        IDictionary<string, object> Result { get; }
    }
}
