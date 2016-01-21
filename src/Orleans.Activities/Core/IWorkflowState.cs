using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Runtime.DurableInstancing;
using System.Xml.Linq;

namespace Orleans.Activities
{
    /// <summary>
    /// Workflow grain state must implement this interface to store workflow instance state.
    /// <para>IMPORTANT: The running workflow instance executor is part of the saved instance values, the values have to be serialized, even during testing!</para>
    /// <para>IMPORTANT: The running workflow instance executor is part of the saved instance values, but that class works only with NetDataContractSerializer.</para>
    /// </summary>
    public interface IWorkflowState
    {
        /// <summary>
        /// The identity and version of the workflow definition.
        /// </summary>
        WorkflowIdentity WorkflowDefinitionIdentity { get; set; }

        /// <summary>
        /// The state of the running workflow instance.
        /// </summary>
        IDictionary<XName, InstanceValue> InstanceValues { get; set; }
    }
}
