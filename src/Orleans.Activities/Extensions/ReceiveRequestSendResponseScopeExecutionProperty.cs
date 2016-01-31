using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Runtime.Serialization;
using Orleans.Activities.Helpers;

namespace Orleans.Activities.Extensions
{
    public static class ReceiveRequestSendResponseScopeExecutionPropertyExtensions
    {
        public static ReceiveRequestSendResponseScopeExecutionProperty GetReceiveRequestSendResponseScopeExecutionProperty(this NativeActivityContext context)
        {
            ReceiveRequestSendResponseScopeExecutionProperty executionProperty =
                context.Properties.Find(ReceiveRequestSendResponseScope.ExecutionPropertyName) as ReceiveRequestSendResponseScopeExecutionProperty;
            if (executionProperty == null)
                throw new ValidationException(nameof(ReceiveRequestSendResponseScopeExecutionProperty) + " is not found.");
            return executionProperty;
        }

        public static ReceiveRequestSendResponseScopeExecutionProperty<TResult> GetReceiveRequestSendResponseScopeExecutionProperty<TResult>(this NativeActivityContext context)
        {
            ReceiveRequestSendResponseScopeExecutionProperty<TResult> executionProperty =
                context.Properties.Find(ReceiveRequestSendResponseScope.ExecutionPropertyName) as ReceiveRequestSendResponseScopeExecutionProperty<TResult>;
            if (executionProperty == null)
                throw new ValidationException(typeof(ReceiveRequestSendResponseScopeExecutionProperty<TResult>).GetFriendlyName() + " is not found.");
            return executionProperty;
        }
    }

    /// <summary>
    /// Abstract ExecutionProperty managed by ReceiveRequestSendResponseScope.
    /// </summary>
    [DataContract]
    public abstract class ReceiveRequestSendResponseScopeExecutionProperty
    {
        [DataMember]
        #pragma warning disable CSE0002 // Use getter-only auto properties - setter method is required by persistence
        public bool Idempotent { get; private set; }
        #pragma warning restore CSE0002 // Use getter-only auto properties

        [DataMember]
        public string OperationName { get; private set; }

        [DataMember]
        public bool Faulted { get; set; }

        // Called implicitly by SendResponse.
        protected ReceiveRequestSendResponseScopeExecutionProperty(bool idempotent)
        {
            Idempotent = idempotent;
        }

        // Called by ReceiveRequest.
        public void Initialize(string operationName)
        {
            if (!string.IsNullOrEmpty(OperationName))
                throw new InvalidOperationException(nameof(OperationName) + " is already initialized.");
            OperationName = operationName;
        }

        // Called by ReceiveRequest.
        public abstract void Initialize(object taskCompletionSource);

        // Called by ReceiveRequestSendResponseScope.
        public abstract bool IsInitializedButNotCompleted { get; }

        // Called by ReceiveRequestSendResponseScope.
        public abstract bool IsInitializedAndCompleted { get; }

        // Called by ReceiveRequestSendResponseScope.
        public abstract void TrySetTaskCompletionSourceCanceled();
    }

    /// <summary>
    /// Generic ExecutionProperty managed by ReceiveRequestSendResponseScope.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    [DataContract]
    public sealed class ReceiveRequestSendResponseScopeExecutionProperty<TResult> : ReceiveRequestSendResponseScopeExecutionProperty
    {
        //[DataMember] If the workflow is reloaded during execution, taskCompletionSource is lost intentionally.
        private TaskCompletionSource<TResult> taskCompletionSource;

        [DataMember]
        private bool taskCompletionSourceIsInitialized;

        // Called by SendResponse.
        public ReceiveRequestSendResponseScopeExecutionProperty(bool idempotent)
            : base(idempotent)
        { }

        // Called by ReceiveRequest.
        public override void Initialize(object taskCompletionSource)
        {
            if (this.taskCompletionSource != null)
                throw new InvalidOperationException(nameof(taskCompletionSource) + " is already initialized.");
            if (!(taskCompletionSource is TaskCompletionSource<TResult>))
                throw new ArgumentException($"Operation's taskCompletionSource is '{taskCompletionSource.GetType().GetFriendlyName()}' and not '{typeof(TaskCompletionSource<TResult>).GetFriendlyName()}', use the proper SendResponse or SendResponse<> activity.");

            this.taskCompletionSource = taskCompletionSource as TaskCompletionSource<TResult>;
            taskCompletionSourceIsInitialized = true;
        }

        // Called by SendResponse.
        public void AssertIsInitialized()
        {
            // When the workflow is reloaded, the taskCompletionSource is lost (is null), but this is not a problem.
            if (string.IsNullOrEmpty(OperationName) || !taskCompletionSourceIsInitialized)
                throw new InvalidOperationException(typeof(ReceiveRequestSendResponseScopeExecutionProperty<TResult>).GetFriendlyName() + " is not initialized, both Initialize() must be called by ReceiveRequest before any SendResponse activity.");
        }

        // Called by SendResponse.
        public void SetTaskCompletionSourceResult(TResult responseParameter, bool throwIfAborted)
        {
            if (taskCompletionSource == null)
            {
                if (throwIfAborted)
                    throw new InvalidOperationException("Operation can't be completed, this is a reloaded workflow, there is no TaskCompletionSource to set the result on the operation.");
            }
            else
                taskCompletionSource.SetResult(responseParameter);
        }

        // Called by ReceiveRequestSendResponseScope.
        public override bool IsInitializedButNotCompleted =>
            !string.IsNullOrEmpty(OperationName) && taskCompletionSource != null && !taskCompletionSource.Task.IsCompleted;

        // Called by ReceiveRequestSendResponseScope.
        public override bool IsInitializedAndCompleted =>
            !string.IsNullOrEmpty(OperationName) && taskCompletionSource != null && taskCompletionSource.Task.IsCompleted;

        // Called by ReceiveRequestSendResponseScope.
        public override void TrySetTaskCompletionSourceCanceled()
        {
            if (taskCompletionSource != null)
                taskCompletionSource.TrySetCanceled();
        }
    }
}
