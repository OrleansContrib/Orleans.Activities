using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities.Hosting;

namespace Orleans.Activities.Configuration
{
    /// <summary>
    /// Affects when the workflow state is saved.
    /// </summary>
    [Flags]
    public enum IdlePersistenceMode
    {
        /// <summary>
        /// Workflow state is never persisted automatically.
        /// </summary>
        Never = 0,
        /// <summary>
        /// Saves workflow state when it is completed. Combine it with other flags.
        /// </summary>
        OnCompleted = 1,
        /// <summary>
        /// Saves workflow state when it is idle during execution, except the first idle state after start (see <see cref="IdlePersistenceMode.OnStart"/>). Combine it with other flags.
        /// </summary>
        OnPersistableIdle = 2,
        /// <summary>
        /// Saves workflow state when it is idle during execution on the first idle state after start. Combine it with other flags.
        /// <para>Usually workflow stops immediately after start to accept the first incoming request that started the workflow, we don't need to persist the workflow in this state.
        /// It doesn't matter, whether the incoming request matches the request the workflow is waiting for,
        /// the persistence will skip only the first persistable idle state and after processing the first incoming request (whether successful or not)
        /// this flag has no effect on persistence (see <see cref="IdlePersistenceMode.OnPersistableIdle"/>).</para>
        /// </summary>
        OnStart = 4,
        Allways = OnStart | OnPersistableIdle | OnCompleted,
    }

    public static class IdlePersistenceModeExtensions
    {
        // don't use "this" to make it a real extension method, don't box enums
        public static bool ShouldSave(IdlePersistenceMode idlePersistenceMode, WorkflowInstanceState workflowInstanceState, bool isStarting)
        {
            if (workflowInstanceState == WorkflowInstanceState.Idle)
                if (isStarting)
                    return idlePersistenceMode.HasFlag(IdlePersistenceMode.OnStart);
                else
                    return idlePersistenceMode.HasFlag(IdlePersistenceMode.OnPersistableIdle);
            if (workflowInstanceState == WorkflowInstanceState.Complete)
                return idlePersistenceMode.HasFlag(IdlePersistenceMode.OnCompleted);
            return false;
        }
    }
}
