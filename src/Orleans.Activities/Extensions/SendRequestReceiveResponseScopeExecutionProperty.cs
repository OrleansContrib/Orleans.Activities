using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using Orleans.Activities.Helpers;
using Orleans.Activities.Hosting;

namespace Orleans.Activities.Extensions
{
    public static class SendRequestReceiveResponseScopeExecutionPropertyExtensions
    {
        public static SendRequestReceiveResponseScopeExecutionProperty GetSendRequestReceiveResponseScopeExecutionProperty(this NativeActivityContext context)
        {
            var executionProperty =
                context.Properties.Find(SendRequestReceiveResponseScope.ExecutionPropertyName) as SendRequestReceiveResponseScopeExecutionProperty;
            if (executionProperty == null)
                throw new ValidationException(nameof(SendRequestReceiveResponseScopeExecutionProperty) + " is not found.");
            return executionProperty;
        }

        public static SendRequestReceiveResponseScopeExecutionPropertyWithoutResult GetSendRequestReceiveResponseScopeExecutionPropertyWithoutResult(
            this NativeActivityContext context)
        {
            var executionProperty =
                context.Properties.Find(SendRequestReceiveResponseScope.ExecutionPropertyName) as SendRequestReceiveResponseScopeExecutionPropertyWithoutResult;
            if (executionProperty == null)
                throw new ValidationException(nameof(SendRequestReceiveResponseScopeExecutionPropertyWithoutResult) + " is not found.");
            return executionProperty;
        }

        public static SendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult> GetSendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult>(
            this NativeActivityContext context)
        {
            var executionProperty =
                context.Properties.Find(SendRequestReceiveResponseScope.ExecutionPropertyName) as SendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult>;
            if (executionProperty == null)
                throw new ValidationException(typeof(SendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult>).GetFriendlyName() + " is not found.");
            return executionProperty;
        }
    }

    /// <summary>
    /// Abstract ExecutionProperty managed by ReceiveRequestSendResponseScope.
    /// </summary>
    public abstract class SendRequestReceiveResponseScopeExecutionProperty
    {
        // Called by SendRequest.
        public abstract void StartOnOperationAsync(IActivityContext activityContext, string operationName);

        // Called by SendRequest.
        public abstract void StartOnOperationAsync<TRequestParameter>(IActivityContext activityContext, string operationName, TRequestParameter parameter);

        // Called by SendRequestReceiveResponseScope.
        public abstract Task UntypedOnOperationTask { get; }

        // Called by ReceiveResponse and SendRequestReceiveResponseScope.
        public abstract void OnOperationTaskWaiterIsScheduled();
    }

    /// <summary>
    /// ExecutionProperty managed by ReceiveRequestSendResponseScope.
    /// </summary>
    public sealed class SendRequestReceiveResponseScopeExecutionPropertyWithoutResult : SendRequestReceiveResponseScopeExecutionProperty
    {
        // SendRequestReceiveResponseScope has a NoPersistHandle, so we can hold references to responseResultTaskFuncTask
        private Task<Func<Task>> responseResultTaskFuncTask;

        private void AssertIsNotStarted()
        {
            if (this.responseResultTaskFuncTask != null)
                throw new InvalidOperationException(nameof(SendRequestReceiveResponseScopeExecutionPropertyWithoutResult) + " is already initialized.");
        }

        // Called by SendRequest.
        public override void StartOnOperationAsync(IActivityContext activityContext, string operationName)
        {
            AssertIsNotStarted();
            this.responseResultTaskFuncTask = activityContext.OnOperationAsync(operationName);
        }

        // Called by SendRequest.
        public override void StartOnOperationAsync<TRequestParameter>(IActivityContext activityContext, string operationName, TRequestParameter parameter)
        {
            AssertIsNotStarted();
            this.responseResultTaskFuncTask = activityContext.OnOperationAsync<TRequestParameter>(operationName, parameter);
        }

        // Called by ReceiveResponse.
        public void AssertIsStarted()
        {
            if (this.responseResultTaskFuncTask == null)
                throw new InvalidOperationException(nameof(SendRequestReceiveResponseScopeExecutionPropertyWithoutResult) +
                    " is not initialized, StartOnOperationAsync() must be called by SendRequest before any ReceiveResponse activity.");
        }

        // Called by ReceiveResponse.
        public Task<Func<Task>> OnOperationTask => this.responseResultTaskFuncTask;

        // Called by SendRequestReceiveResponseScope.
        public override Task UntypedOnOperationTask => this.responseResultTaskFuncTask;

        // Called by ReceiveResponse and SendRequestReceiveResponseScope.
        public override void OnOperationTaskWaiterIsScheduled()
            => this.responseResultTaskFuncTask = null;
    }

    /// <summary>
    /// Generic ExecutionProperty managed by ReceiveRequestSendResponseScope.
    /// </summary>
    /// <typeparam name="TResponseResult"></typeparam>
    public sealed class SendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult> : SendRequestReceiveResponseScopeExecutionProperty
    {
        // SendRequestReceiveResponseScope has a NoPersistHandle, so we can hold references to responseResultTaskFuncTask
        private Task<Func<Task<TResponseResult>>> responseResultTaskFuncTask;

        private void AssertIsNotStarted()
        {
            if (this.responseResultTaskFuncTask != null)
                throw new InvalidOperationException(typeof(SendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult>).GetFriendlyName() + " is already initialized.");
        }

        // Called by SendRequest.
        public override void StartOnOperationAsync(IActivityContext activityContext, string operationName)
        {
            AssertIsNotStarted();
            this.responseResultTaskFuncTask = activityContext.OnOperationAsync<TResponseResult>(operationName);
        }

        // Called by SendRequest.
        public override void StartOnOperationAsync<TRequestParameter>(IActivityContext activityContext, string operationName, TRequestParameter parameter)
        {
            AssertIsNotStarted();
            this.responseResultTaskFuncTask = activityContext.OnOperationAsync<TRequestParameter, TResponseResult>(operationName, parameter);
        }

        // Called by ReceiveResponse.
        public void AssertIsStarted()
        {
            if (this.responseResultTaskFuncTask == null)
                throw new InvalidOperationException(typeof(SendRequestReceiveResponseScopeExecutionPropertyWithResult<TResponseResult>).GetFriendlyName() +
                    " is not initialized, StartOnOperationAsync() must be called by SendRequest before any ReceiveResponse activity.");
        }

        // Called by ReceiveResponse.
        public Task<Func<Task<TResponseResult>>> OnOperationTask => this.responseResultTaskFuncTask;

        // Called by SendRequestReceiveResponseScope.
        public override Task UntypedOnOperationTask => this.responseResultTaskFuncTask;

        // Called by ReceiveResponse and SendRequestReceiveResponseScope.
        public override void OnOperationTaskWaiterIsScheduled() => this.responseResultTaskFuncTask = null;
    }
}
