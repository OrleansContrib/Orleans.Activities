﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Runtime.DurableInstancing;
using System.Xml.Linq;
using Orleans.Activities.Configuration;
using Orleans.Activities.Persistence;

namespace Orleans.Activities.Hosting
{
    /// <summary>
    /// Stores the completion information of the workflow instance.
    /// When the workflow instance completes, the workflow host immediately replaces the instance with this WorkflowCompletionState.
    /// When the workflow instance is reloaded from a completed state, only this WorkflowCompletionState is loaded with the minimal set of the extensions.
    /// </summary>
    public class WorkflowCompletionState : IWorkflowCompletionState
    {
        private ActivityInstanceState completionState;
        private IDictionary<string, object> outputArguments;
        private Exception terminationException;

        public WorkflowCompletionState()
        { }

        public WorkflowCompletionState(ActivityInstanceState completionState, IDictionary<string, object> outputArguments, Exception terminationException)
        {
            this.completionState = completionState;
            this.outputArguments = outputArguments;
            this.terminationException = terminationException;
        }

        public async Task LoadAsync(IDictionary<XName, InstanceValue> instanceValues, IEnumerable<object> extensions, IParameters parameters)
        {
            switch (instanceValues[WorkflowNamespace.Status].Value as string)
            {
                case WorkflowStatus.Closed:
                    var outputArgumentsRead = new Dictionary<string, object>();
                    foreach (var kvp in instanceValues.Where((iv) => iv.Key.Namespace == WorkflowNamespace.OutputPath))
                        outputArgumentsRead.Add(kvp.Key.LocalName, kvp.Value.Value);
                    if (outputArgumentsRead.Count > 0)
                        this.outputArguments = outputArgumentsRead;
                    this.completionState = ActivityInstanceState.Closed;
                    break;
                case WorkflowStatus.Canceled:
                    this.completionState = ActivityInstanceState.Canceled;
                    break;
                case WorkflowStatus.Faulted:
                    this.terminationException = instanceValues[WorkflowNamespace.Exception].Value as Exception;
                    this.completionState = ActivityInstanceState.Faulted;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(WorkflowNamespace.Status.ToString());
            }

            // Yes, IEnumerable<object> is ugly, but there is nothing common in IPersistenceParticipant and PersistenceParticipant.
            var persistenceParticipants =
                ((IEnumerable<object>)extensions.OfType<System.Activities.Persistence.PersistenceParticipant>())
                .Concat(
                ((IEnumerable<object>)extensions.OfType<IPersistenceParticipant>()));
            // If the persistenceParticipants throw during OnLoadAsync(), the pipeline will rethrow
            if (persistenceParticipants.Any())
            {
                var persistencePipeline = new PersistencePipeline(persistenceParticipants, instanceValues);
                await persistencePipeline.OnLoadAsync(parameters.ExtensionsPersistenceTimeout);
                persistencePipeline.Publish();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations")]
        public IDictionary<string, object> Result
        {
            get
            {
                if (this.completionState == ActivityInstanceState.Faulted)
                    throw this.terminationException;
                if (this.completionState == ActivityInstanceState.Canceled)
                    throw new OperationCanceledException("Workflow is canceled.");
                return this.outputArguments;
            }
        }
    }
}
