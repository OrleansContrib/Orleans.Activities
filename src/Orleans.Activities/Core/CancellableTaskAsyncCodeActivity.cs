using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Threading;
using Orleans.Activities.AsyncEx;

namespace Orleans.Activities
{
    // On the "access activity context after any await statement" problem, see http://stackoverflow.com/questions/26054585/asynctaskcodeactivity-and-lost-context-after-await/26061482#26061482

    /// <summary>
    /// Base class for cancellable Task based async activities without result.
    /// </summary>
    public abstract class CancellableTaskAsyncCodeActivity : AsyncCodeActivity
    {
        protected sealed override IAsyncResult BeginExecute(AsyncCodeActivityContext context, AsyncCallback callback, object state)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            context.UserState = cts;
            return AsyncFactory.ToBegin(ExecuteAsync(context, cts.Token), callback, state);
        }

        protected sealed override void EndExecute(AsyncCodeActivityContext context, IAsyncResult asyncResult)
        {
            try
            {
                AsyncFactory.ToEnd(asyncResult);
                PostExecute(context);
            }
            catch (OperationCanceledException) when (context.IsCancellationRequested)
            {
                context.MarkCanceled();
            }
        }

        protected sealed override void Cancel(AsyncCodeActivityContext context)
        {
            (context.UserState as CancellationTokenSource).Cancel();
        }

        /// <summary>
        /// Implement any Task based async operation.
        /// <para>DO NOT USE context after any await statement! See <see cref="PostExecute"/> to access context after Task completion!
        /// Or use one variant of the <see cref="CancellableContextSafeTaskAsyncCodeActivity{TState}"/> class.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract Task ExecuteAsync(AsyncCodeActivityContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Executed after the Task of <see cref="ExecuteAsync"/> is completed.
        /// <para>In case of any need to access context after Task completed, use this method.</para>
        /// </summary>
        /// <param name="context"></param>
        protected virtual void PostExecute(AsyncCodeActivityContext context)
        { }
    }

    /// <summary>
    /// Base class for cancellable Task based async activities without result, where the Task has result.
    /// </summary>
    /// <typeparam name="TTaskResult"></typeparam>
    public abstract class CancellableTaskAsyncCodeActivity<TTaskResult> : AsyncCodeActivity
    {
        protected sealed override IAsyncResult BeginExecute(AsyncCodeActivityContext context, AsyncCallback callback, object state)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            context.UserState = cts;
            return AsyncFactory<TTaskResult>.ToBegin(ExecuteAsync(context, cts.Token), callback, state);
        }

        protected sealed override void EndExecute(AsyncCodeActivityContext context, IAsyncResult asyncResult)
        {
            try
            {
                PostExecute(context, AsyncFactory<TTaskResult>.ToEnd(asyncResult));
            }
            catch (OperationCanceledException) when (context.IsCancellationRequested)
            {
                context.MarkCanceled();
            }
        }

        protected sealed override void Cancel(AsyncCodeActivityContext context)
        {
            (context.UserState as CancellationTokenSource).Cancel();
        }

        /// <summary>
        /// Implement any Task based async operation.
        /// <para>DO NOT USE context after any await statement! See <see cref="PostExecute"/> to access context after Task completion!
        /// Or use one variant of the <see cref="CancellableContextSafeTaskAsyncCodeActivity{TState}"/> class.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract Task<TTaskResult> ExecuteAsync(AsyncCodeActivityContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Executed after the Task of <see cref="ExecuteAsync"/> is completed.
        /// <para>To handle the result of the Task (or in case of any need to access context after Task completed), implement this method.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="taskResult"></param>
        /// <returns></returns>
        protected abstract void PostExecute(AsyncCodeActivityContext context, TTaskResult taskResult);
    }

    /// <summary>
    /// Base class for cancellable Task based async activities with result.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    public abstract class CancellableTaskAsyncCodeActivityWithResult<TResult> : CancellableTaskAsyncCodeActivityWithResult<TResult, TResult>
    {
        /// <summary>
        /// Executed after the Task of <see cref="CancellableTaskAsyncCodeActivityWithResult{TActivityResult, TTaskResult}.ExecuteAsync"/> is completed.
        /// <para>In case of any need to access context after Task completed, implement this method.
        /// The return value of the method is copied to the Result OutArgument of the Activity.
        /// By default the result of the Task is the return value of this method.
        /// </para>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="taskResult"></param>
        /// <returns></returns>
        protected override TResult PostExecute(AsyncCodeActivityContext context, TResult taskResult) => taskResult;
    }

    /// <summary>
    /// Base class for cancellable Task based async activities with result, where the Task has different result type than the Activity.
    /// </summary>
    /// <typeparam name="TActivityResult"></typeparam>
    /// <typeparam name="TTaskResult"></typeparam>
    public abstract class CancellableTaskAsyncCodeActivityWithResult<TActivityResult, TTaskResult> : AsyncCodeActivity<TActivityResult>
    {
        protected sealed override IAsyncResult BeginExecute(AsyncCodeActivityContext context, AsyncCallback callback, object state)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            context.UserState = cts;
            return AsyncFactory<TTaskResult>.ToBegin(ExecuteAsync(context, cts.Token), callback, state);
        }

        protected sealed override TActivityResult EndExecute(AsyncCodeActivityContext context, IAsyncResult asyncResult)
        {
            try
            {
                return PostExecute(context, AsyncFactory<TTaskResult>.ToEnd(asyncResult));
            }
            catch (OperationCanceledException) when (context.IsCancellationRequested)
            {
                context.MarkCanceled();
                return default(TActivityResult);
            }
        }

        protected sealed override void Cancel(AsyncCodeActivityContext context)
        {
            (context.UserState as CancellationTokenSource).Cancel();
        }

        /// <summary>
        /// Implement any Task based async operation.
        /// <para>DO NOT USE context after any await statement! See <see cref="PostExecute"/> to access context after Task completion!
        /// Or use one variant of the <see cref="CancellableContextSafeTaskAsyncCodeActivity{TState}"/> class.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract Task<TTaskResult> ExecuteAsync(AsyncCodeActivityContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Executed after the Task of <see cref="ExecuteAsync"/> is completed.
        /// <para>To handle the result of the Task (or in case of any need to access context after Task completed), implement this method.
        /// The return value of the method is copied to the Result OutArgument of the Activity.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="taskResult"></param>
        /// <returns></returns>
        protected abstract TActivityResult PostExecute(AsyncCodeActivityContext context, TTaskResult taskResult);
    }

    /// <summary>
    /// Base class for cancellable Task based async activities without result.
    /// The Task is safely separated from activity context, the Task can't access it even before any await statement.
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    public abstract class CancellableContextSafeTaskAsyncCodeActivity<TState> : AsyncCodeActivity
    {
        protected sealed override IAsyncResult BeginExecute(AsyncCodeActivityContext context, AsyncCallback callback, object state)
        {
            TState activityState = PreExecute(context);
            CancellationTokenSource cts = new CancellationTokenSource();
            context.UserState = new Tuple<TState, CancellationTokenSource>(activityState, cts);
            return AsyncFactory.ToBegin(ExecuteAsync(activityState, cts.Token), callback, state);
        }

        protected sealed override void EndExecute(AsyncCodeActivityContext context, IAsyncResult asyncResult)
        {
            try
            {
                AsyncFactory.ToEnd(asyncResult);
                PostExecute(context, (context.UserState as Tuple<TState, CancellationTokenSource>).Item1);
            }
            catch (OperationCanceledException) when (context.IsCancellationRequested)
            {
                context.MarkCanceled();
            }
        }

        protected sealed override void Cancel(AsyncCodeActivityContext context)
        {
            (context.UserState as Tuple<TState, CancellationTokenSource>).Item2.Cancel();
        }

        /// <summary>
        /// Executed before the Task of <see cref="ExecuteAsync"/> executed.
        /// <para>Create a TState, instead of the activity context, TState will be accessible during and after Task execution.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected abstract TState PreExecute(AsyncCodeActivityContext context);

        /// <summary>
        /// Implement any Task based async operation.
        /// <para>The activityState is created during <see cref="PreExecute"/> and accessible during and after Task execution.</para>
        /// </summary>
        /// <param name="activityState"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract Task ExecuteAsync(TState activityState, CancellationToken cancellationToken);

        /// <summary>
        /// Executed after the Task of <see cref="ExecuteAsync"/> is completed.
        /// <para>In case of any need to access context after Task completed, use this method.
        /// The activityState is created during <see cref="PreExecute"/> and accessible during and after Task execution.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="activityState"></param>
        protected virtual void PostExecute(AsyncCodeActivityContext context, TState activityState)
        { }
    }

    /// <summary>
    /// Base class for cancellable Task based async activities without result, where the Task has result.
    /// The Task is safely separated from activity context, the Task can't access it even before any await statement.
    /// </summary>
    /// <typeparam name="TTaskResult"></typeparam>
    /// <typeparam name="TState"></typeparam>
    public abstract class CancellableContextSafeTaskAsyncCodeActivity<TTaskResult, TState> : AsyncCodeActivity
    {
        protected sealed override IAsyncResult BeginExecute(AsyncCodeActivityContext context, AsyncCallback callback, object state)
        {
            TState activityState = PreExecute(context);
            CancellationTokenSource cts = new CancellationTokenSource();
            context.UserState = new Tuple<TState, CancellationTokenSource>(activityState, cts);
            return AsyncFactory<TTaskResult>.ToBegin(ExecuteAsync(activityState, cts.Token), callback, state);
        }

        protected sealed override void EndExecute(AsyncCodeActivityContext context, IAsyncResult asyncResult)
        {
            try
            {
                PostExecute(context, (context.UserState as Tuple<TState, CancellationTokenSource>).Item1, AsyncFactory<TTaskResult>.ToEnd(asyncResult));
            }
            catch (OperationCanceledException) when (context.IsCancellationRequested)
            {
                context.MarkCanceled();
            }
        }

        protected sealed override void Cancel(AsyncCodeActivityContext context)
        {
            (context.UserState as Tuple<TState, CancellationTokenSource>).Item2.Cancel();
        }

        /// <summary>
        /// Executed before the Task of <see cref="ExecuteAsync"/> executed.
        /// <para>Create a TState, instead of the activity context, TState will be accessible during and after Task execution.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected abstract TState PreExecute(AsyncCodeActivityContext context);

        /// <summary>
        /// Implement any Task based async operation.
        /// <para>The activityState is created during <see cref="PreExecute"/> and accessible during and after Task execution.</para>
        /// </summary>
        /// <param name="activityState"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract Task<TTaskResult> ExecuteAsync(TState activityState, CancellationToken cancellationToken);

        /// <summary>
        /// Executed after the Task of <see cref="ExecuteAsync"/> is completed.
        /// <para>To handle the result of the Task (or in case of any need to access context after Task completed), implement this method.
        /// The activityState is created during <see cref="PreExecute"/> and accessible during and after Task execution.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="activityState"></param>
        /// <param name="taskResult"></param>
        /// <returns></returns>
        protected abstract void PostExecute(AsyncCodeActivityContext context, TState activityState, TTaskResult taskResult);
    }

    /// <summary>
    /// Base class for cancellable Task based async activities with result.
    /// The Task is safely separated from activity context, the Task can't access it even before any await statement.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <typeparam name="TState"></typeparam>
    public abstract class CancellableContextSafeTaskAsyncCodeActivityWithResult<TResult, TState> :
        CancellableContextSafeTaskAsyncCodeActivityWithResult<TResult, TResult, TState>
    {
        /// <summary>
        /// Executed after the Task of <see cref="CancellableContextSafeTaskAsyncCodeActivityWithResult{TActivityResult, TTaskResult, TState}.ExecuteAsync"/> is completed.
        /// <para>In case of any need to access context after Task completed, implement this method.
        /// The activityState is created during <see cref="CancellableContextSafeTaskAsyncCodeActivityWithResult{TResult, TResult, TState}.PreExecute"/> and accessible during and after Task execution.
        /// The return value of the method is copied to the Result OutArgument of the Activity.
        /// By default the result of the Task is the return value of this method.
        /// </para>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="activityState"></param>
        /// <param name="taskResult"></param>
        /// <returns></returns>
        protected override TResult PostExecute(AsyncCodeActivityContext context, TState activityState, TResult taskResult) => taskResult;
    }

    /// <summary>
    /// Base class for cancellable Task based async activities with result, where the Task has different result type than the Activity.
    /// The Task is safely separated from activity context, the Task can't access it even before any await statement.
    /// </summary>
    /// <typeparam name="TActivityResult"></typeparam>
    /// <typeparam name="TTaskResult"></typeparam>
    /// <typeparam name="TState"></typeparam>
    public abstract class CancellableContextSafeTaskAsyncCodeActivityWithResult<TActivityResult, TTaskResult, TState> : AsyncCodeActivity<TActivityResult>
    {
        protected sealed override IAsyncResult BeginExecute(AsyncCodeActivityContext context, AsyncCallback callback, object state)
        {
            TState activityState = PreExecute(context);
            CancellationTokenSource cts = new CancellationTokenSource();
            context.UserState = new Tuple<TState, CancellationTokenSource>(activityState, cts);
            return AsyncFactory<TTaskResult>.ToBegin(ExecuteAsync(activityState, cts.Token), callback, state);
        }

        protected sealed override TActivityResult EndExecute(AsyncCodeActivityContext context, IAsyncResult asyncResult)
        {
            try
            {
                return PostExecute(context, (context.UserState as Tuple<TState, CancellationTokenSource>).Item1, AsyncFactory<TTaskResult>.ToEnd(asyncResult));
            }
            catch (OperationCanceledException) when (context.IsCancellationRequested)
            {
                context.MarkCanceled();
                return default(TActivityResult);
            }
        }

        protected sealed override void Cancel(AsyncCodeActivityContext context)
        {
            (context.UserState as Tuple<TState, CancellationTokenSource>).Item2.Cancel();
        }

        /// <summary>
        /// Executed before the Task of <see cref="ExecuteAsync"/> executed.
        /// <para>Create a TState, instead of the activity context, TState will be accessible during and after Task execution.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected abstract TState PreExecute(AsyncCodeActivityContext context);

        /// <summary>
        /// Implement any Task based async operation.
        /// <para>The activityState is created during <see cref="PreExecute"/> and accessible during and after Task execution.</para>
        /// </summary>
        /// <param name="activityState"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract Task<TTaskResult> ExecuteAsync(TState activityState, CancellationToken cancellationToken);

        /// <summary>
        /// Executed after the Task of <see cref="ExecuteAsync"/> is completed.
        /// <para>To handle the result of the Task (or in case of any need to access context after Task completed), implement this method.
        /// The activityState is created during <see cref="PreExecute"/> and accessible during and after Task execution.
        /// The return value of the method is copied to the Result OutArgument of the Activity.</para>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="activityState"></param>
        /// <param name="taskResult"></param>
        /// <returns></returns>
        protected abstract TActivityResult PostExecute(AsyncCodeActivityContext context, TState activityState, TTaskResult taskResult);
    }
}
