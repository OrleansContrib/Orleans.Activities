using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

namespace Orleans.Activities.Hosting
{
    /// <summary>
    /// SynchronizationContext used by the workflow instance's ActivityExecutor to pump delegates to the scheduler.
    /// </summary>
    public class SynchronizationContext : System.Threading.SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object state) => Task.Factory.StartNew((_state) => d(_state), state);

        public override void Send(SendOrPostCallback d, object state) => throw new NotSupportedException("Send is not supported.");
    }
}
