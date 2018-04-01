using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml.Linq;

namespace Orleans.Activities.Persistence
{
    /// <summary>
    /// Constant XNamespace and XName values for persistence.
    /// </summary>
    public static class WorkflowNamespace
    {
        //private const string baseNamespace = "urn:schemas-microsoft-com:System.Activities/4.0/properties";
        public const string BaseNamespace = "urn:orleans.activities/1.0/properties";

        // WriteOnly
        public static readonly XNamespace VariablesPath = XNamespace.Get(BaseNamespace + "/variables");

        public static readonly XNamespace OutputPath = XNamespace.Get(BaseNamespace + "/output");

        private static readonly XNamespace WorkflowPath = XNamespace.Get(BaseNamespace);

        // WriteOnly
        public static readonly XName Bookmarks = WorkflowPath.GetName(nameof(Bookmarks));

        // WriteOnly
        public static readonly XName LastUpdate = WorkflowPath.GetName(nameof(LastUpdate));

        public static readonly XName Workflow = WorkflowPath.GetName(nameof(Workflow));

        // this is not part of the original System.Activities namespace/schema
        public static readonly XName IsStarting = WorkflowPath.GetName(nameof(IsStarting));

        public static readonly XName Status = WorkflowPath.GetName(nameof(Status));

        public static readonly XName Exception = WorkflowPath.GetName(nameof(Exception));
    }
}
