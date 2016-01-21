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
        public string OperationName { get; protected set; }

        //[DataMember] If the workflow is reloaded during execution, taskCompletionSource is lost intentionally.
        protected object taskCompletionSource;

        // Called by ReceiveRequest.
        public abstract void Initialize(string operationName, object taskCompletionSource);

        // Called by ReceiveRequestSendResponseScope.
        public abstract void TrySetTaskCompletionSourceCanceled();

        // Called by ReceiveRequestSendResponseScope.
        public abstract bool TrySetTaskCompletionSourceException(Exception exception);
    }

    /// <summary>
    /// Generic ExecutionProperty managed by ReceiveRequestSendResponseScope.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    [DataContract]
    public sealed class ReceiveRequestSendResponseScopeExecutionProperty<TResult> : ReceiveRequestSendResponseScopeExecutionProperty
    {
        private void AssertIsNotInitialized()
        {
            if (!string.IsNullOrEmpty(OperationName))
                throw new InvalidOperationException(typeof(ReceiveRequestSendResponseScopeExecutionProperty<TResult>).GetFriendlyName() + " is already initialized.");
        }
            
        // Called by ReceiveRequest.
        public override void Initialize(string operationName, object taskCompletionSource)
        {
            AssertIsNotInitialized();
            if (!(taskCompletionSource is TaskCompletionSource<TResult>))
                throw new ArgumentException($"Operation's taskCompletionSource is '{taskCompletionSource.GetType().GetFriendlyName()}' and not '{typeof(TaskCompletionSource<TResult>).GetFriendlyName()}', use the proper SendResponse or SendResponse<> activity.");

            this.OperationName = operationName;
            this.taskCompletionSource = taskCompletionSource;
        }

        // Called by SendResponse.
        public void AssertIsInitialized()
        {
            if (string.IsNullOrEmpty(OperationName))
                throw new InvalidOperationException(typeof(ReceiveRequestSendResponseScopeExecutionProperty<TResult>).GetFriendlyName() + " is not initialized, Initialize() must be called by ReceiveRequest before any SendResponse activity.");
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
                (taskCompletionSource as TaskCompletionSource<TResult>).SetResult(responseParameter);
        }

        // Called by ReceiveRequestSendResponseScope.
        public override void TrySetTaskCompletionSourceCanceled()
        {
            if (taskCompletionSource != null)
                (taskCompletionSource as TaskCompletionSource<TResult>).TrySetCanceled();
        }

        // Called by ReceiveRequestSendResponseScope.
        public override bool TrySetTaskCompletionSourceException(Exception exception)
        {
            if (taskCompletionSource != null)
                return (taskCompletionSource as TaskCompletionSource<TResult>).TrySetException(exception);
            return false;
        }
    }
}
