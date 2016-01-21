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
        private static readonly XNamespace variablesPath = XNamespace.Get(BaseNamespace + "/variables");
        public static XNamespace VariablesPath => variablesPath;

        // WriteOnly
        private static readonly XNamespace outputPath = XNamespace.Get(BaseNamespace + "/output");
        public static XNamespace OutputPath => outputPath;

        private static readonly XNamespace workflowPath = XNamespace.Get(BaseNamespace);

        // WriteOnly
        private static readonly XName bookmarks = workflowPath.GetName(nameof(Bookmarks));
        public static XName Bookmarks => bookmarks;

        // WriteOnly
        private static readonly XName lastUpdate = workflowPath.GetName(nameof(LastUpdate));
        public static XName LastUpdate => lastUpdate;

        private static readonly XName workflow = workflowPath.GetName(nameof(Workflow));
        public static XName Workflow => workflow;

        // this is not part of the original System.Activities namespace/schema
        private static readonly XName isStarting = workflowPath.GetName(nameof(IsStarting));
        public static XName IsStarting => isStarting;

        // WriteOnly
        private static readonly XName status = workflowPath.GetName(nameof(Status));
        public static XName Status => status;

        // WriteOnly
        private static readonly XName exception = workflowPath.GetName(nameof(Exception));
        public static XName Exception => exception;
    }
}
