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
    /// Base class for WorkflowGrain states.
    /// <para>Usage is not mandatory, <see cref="WorkflowGrain{TGrain, TGrainState, TWorkflowInterface, TWorkflowCallbackInterface}"/> requires only to implement <see cref="IWorkflowState"/> interface.</para>
    /// </summary>
    public class WorkflowState : GrainState, IWorkflowState
    {
        public IDictionary<XName, InstanceValue> InstanceValues { get; set; }
        public WorkflowIdentity WorkflowDefinitionIdentity { get; set; }
    }
}
