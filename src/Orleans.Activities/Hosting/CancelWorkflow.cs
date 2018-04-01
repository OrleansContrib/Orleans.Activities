using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;

namespace Orleans.Activities.Hosting
{
    // The Cancel equivalent of TerminateWorkflow,
    // see: http://referencesource.microsoft.com/#System.Activities/System/Activities/Statements/TerminateWorkflow.cs
    /// <summary>
    /// Cancels the running workflow instance, raises the Completed event in the host. Once the workflow is canceled, it cannot be resumed.
    /// </summary>
    public sealed class CancelWorkflow : NativeActivity
    {
        protected override void Execute(NativeActivityContext context) => context.CancelWorkflow();
    }
}
