// original source https://github.com/StephenCleary/AsyncEx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

// MODIFIED
// several methods are removed, only FromApm(), ToBegin() and ToEnd() are remained

namespace Orleans.Activities.AsyncEx
{
    /// <summary>
    /// Provides asynchronous wrappers.
    /// </summary>
    public static partial class AsyncFactory
    {
        private static AsyncCallback Callback(Action<IAsyncResult> endMethod, TaskCompletionSource<object> tcs)
        {
            // MODIFIED
            TaskScheduler originalTaskScheduler = TaskScheduler.Current;
            return (asyncResult) =>
            {
                // MODIFIED
                //try
                //{
                //    endMethod(asyncResult);
                //    tcs.TrySetResult(null);
                //}
                //catch (OperationCanceledException)
                //{
                //    tcs.TrySetCanceled();
                //}
                //catch (Exception ex)
                //{
                //    tcs.TrySetException(ex);
                //} 
                if (!asyncResult.CompletedSynchronously)
                {
                    if (TaskScheduler.Current == originalTaskScheduler)
                        CallEndMethod(asyncResult, endMethod, tcs);
                    else
                        Task.Factory.StartNew(() => CallEndMethod(asyncResult, endMethod, tcs),
                            CancellationToken.None, TaskCreationOptions.None, originalTaskScheduler);
                }
            };
        }

        private static void CallEndMethod(IAsyncResult asyncResult, Action<IAsyncResult> endMethod, TaskCompletionSource<object> tcs)
        {
            try
            {
                endMethod(asyncResult);
                tcs.TrySetResult(null);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        /// <summary>
        /// Wraps a begin/end asynchronous method.
        /// </summary>
        /// <param name="beginMethod">The begin method.</param>
        /// <param name="endMethod">The end method.</param>
        /// <returns></returns>
        public static Task FromApm(Func<AsyncCallback, object, IAsyncResult> beginMethod, Action<IAsyncResult> endMethod)
        {
            var tcs = new TaskCompletionSource<object>();
            // MODIFIED
            //beginMethod(Callback(endMethod, tcs), null);
            IAsyncResult asyncResult = beginMethod(Callback(endMethod, tcs), null);
            if (asyncResult.CompletedSynchronously)
                CallEndMethod(asyncResult, endMethod, tcs);
            return tcs.Task;
        }

        #region FromApm arg0 .. arg2

        /// <summary>
        /// Wraps a begin/end asynchronous method.
        /// </summary>
        /// <typeparam name="TArg0">The type of argument 0.</typeparam>
        /// <param name="beginMethod">The begin method.</param>
        /// <param name="endMethod">The end method.</param>
        /// <param name="arg0">Argument 0.</param>
        /// <returns></returns>
        public static Task FromApm<TArg0>(Func<TArg0, AsyncCallback, object, IAsyncResult> beginMethod, Action<IAsyncResult> endMethod, TArg0 arg0)
        {
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            // MODIFIED
            //beginMethod(arg0, Callback(endMethod, tcs), null);
            IAsyncResult asyncResult = beginMethod(arg0, Callback(endMethod, tcs), null);
            if (asyncResult.CompletedSynchronously)
                CallEndMethod(asyncResult, endMethod, tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Wraps a begin/end asynchronous method.
        /// </summary>
        /// <typeparam name="TArg0">The type of argument 0.</typeparam>
        /// <typeparam name="TArg1">The type of argument 1.</typeparam>
        /// <param name="beginMethod">The begin method.</param>
        /// <param name="endMethod">The end method.</param>
        /// <param name="arg0">Argument 0.</param>
        /// <param name="arg1">Argument 1.</param>
        /// <returns></returns>
        public static Task FromApm<TArg0, TArg1>(Func<TArg0, TArg1, AsyncCallback, object, IAsyncResult> beginMethod, Action<IAsyncResult> endMethod, TArg0 arg0, TArg1 arg1)
        {
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            // MODIFIED
            //beginMethod(arg0, arg1, Callback(endMethod, tcs), null);
            IAsyncResult asyncResult = beginMethod(arg0, arg1, Callback(endMethod, tcs), null);
            if (asyncResult.CompletedSynchronously)
                CallEndMethod(asyncResult, endMethod, tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Wraps a begin/end asynchronous method.
        /// </summary>
        /// <typeparam name="TArg0">The type of argument 0.</typeparam>
        /// <typeparam name="TArg1">The type of argument 1.</typeparam>
        /// <typeparam name="TArg2">The type of argument 2.</typeparam>
        /// <param name="beginMethod">The begin method.</param>
        /// <param name="endMethod">The end method.</param>
        /// <param name="arg0">Argument 0.</param>
        /// <param name="arg1">Argument 1.</param>
        /// <param name="arg2">Argument 2.</param>
        /// <returns></returns>
        public static Task FromApm<TArg0, TArg1, TArg2>(Func<TArg0, TArg1, TArg2, AsyncCallback, object, IAsyncResult> beginMethod, Action<IAsyncResult> endMethod, TArg0 arg0, TArg1 arg1, TArg2 arg2)
        {
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            // MODIFIED
            //beginMethod(arg0, arg1, arg2, Callback(endMethod, tcs), null);
            IAsyncResult asyncResult = beginMethod(arg0, arg1, arg2, Callback(endMethod, tcs), null);
            if (asyncResult.CompletedSynchronously)
                CallEndMethod(asyncResult, endMethod, tcs);
            return tcs.Task;
        }

        #endregion

        /// <summary>
        /// Wraps a <see cref="Task"/> into the Begin method of an APM pattern.
        /// </summary>
        /// <param name="task">The task to wrap.</param>
        /// <param name="callback">The callback method passed into the Begin method of the APM pattern.</param>
        /// <param name="state">The state passed into the Begin method of the APM pattern.</param>
        /// <returns>The asynchronous operation, to be returned by the Begin method of the APM pattern.</returns>
        public static IAsyncResult ToBegin(Task task, AsyncCallback callback, object state)
        {
            // MODIFIED
            if (task.IsCompleted)
                return new AsyncResultCompletedSynchronously(task, state);
            else
            {
                // MODIFIED
                //var tcs = new TaskCompletionSource(state);
                var tcs = new TaskCompletionSource<object>(state);
                task.ContinueWith((t) =>
                {
                    // MODIFIED
                    //tcs.TryCompleteFromCompletedTask(t);
                    if (task.IsFaulted)
                        tcs.TrySetException(task.Exception.InnerExceptions);
                    else if (task.IsCanceled)
                        tcs.TrySetCanceled();
                    else
                        tcs.TrySetResult(null);

                    if (callback != null)
                        callback(tcs.Task);
                // MODIFIED
                //}, TaskScheduler.Default);
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
                return tcs.Task;
            }
        }

        /// <summary>
        /// Wraps a <see cref="Task"/> into the End method of an APM pattern.
        /// </summary>
        /// <param name="asyncResult">The asynchronous operation returned by the matching Begin method of this APM pattern.</param>
        /// <returns>The result of the asynchronous operation, to be returned by the End method of the APM pattern.</returns>
        public static void ToEnd(IAsyncResult asyncResult)
        {
            // MODIFIED
            AsyncResultCompletedSynchronously asyncResultCompletedSynchronously = asyncResult as AsyncResultCompletedSynchronously;
            if (asyncResultCompletedSynchronously != null)
                ((Task)asyncResultCompletedSynchronously).GetAwaiter().GetResult();
            else
            {
                // MODIFIED
                //((Task)asyncResult).WaitAndUnwrapException();
                ((Task)asyncResult).GetAwaiter().GetResult();
            }
        }
    }

    /// <summary>
    /// Provides asynchronous wrappers.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the asychronous operation.</typeparam>
    public static partial class AsyncFactory<TResult>
    {
        private static AsyncCallback Callback(Func<IAsyncResult, TResult> endMethod, TaskCompletionSource<TResult> tcs)
        {
            // MODIFIED
            TaskScheduler originalTaskScheduler = TaskScheduler.Current;
            return (asyncResult) =>
            {
                // MODIFIED
                //try
                //{
                //    tcs.TrySetResult(endMethod(asyncResult));
                //}
                //catch (OperationCanceledException)
                //{
                //    tcs.TrySetCanceled();
                //}
                //catch (Exception ex)
                //{
                //    tcs.TrySetException(ex);
                //}
                if (!asyncResult.CompletedSynchronously)
                {
                    if (TaskScheduler.Current == originalTaskScheduler)
                        CallEndMethod(asyncResult, endMethod, tcs);
                    else
                        Task.Factory.StartNew(() => CallEndMethod(asyncResult, endMethod, tcs),
                            CancellationToken.None, TaskCreationOptions.None, originalTaskScheduler);
                }
            };
        }

        private static void CallEndMethod(IAsyncResult asyncResult, Func<IAsyncResult, TResult> endMethod, TaskCompletionSource<TResult> tcs)
        {
            try
            {
                tcs.TrySetResult(endMethod(asyncResult));
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        /// <summary>
        /// Wraps a begin/end asynchronous method.
        /// </summary>
        /// <param name="beginMethod">The begin method. May not be <c>null</c>.</param>
        /// <param name="endMethod">The end method. May not be <c>null</c>.</param>
        /// <returns>The result of the asynchronous operation.</returns>
        public static Task<TResult> FromApm(Func<AsyncCallback, object, IAsyncResult> beginMethod, Func<IAsyncResult, TResult> endMethod)
        {
            var tcs = new TaskCompletionSource<TResult>();
            // MODIFIED
            //beginMethod(Callback(endMethod, tcs), null);
            IAsyncResult asyncResult = beginMethod(Callback(endMethod, tcs), null);
            if (asyncResult.CompletedSynchronously)
                CallEndMethod(asyncResult, endMethod, tcs);
            return tcs.Task;
        }

        #region FromApm arg0 .. arg2

        /// <summary>
        /// Wraps a begin/end asynchronous method.
        /// </summary>
        /// <typeparam name="TArg0">The type of argument 0.</typeparam>
        /// <param name="beginMethod">The begin method.</param>
        /// <param name="endMethod">The end method.</param>
        /// <param name="arg0">Argument 0.</param>
        /// <returns>The result of the asynchronous operation.</returns>
        public static Task<TResult> FromApm<TArg0>(Func<TArg0, AsyncCallback, object, IAsyncResult> beginMethod, Func<IAsyncResult, TResult> endMethod, TArg0 arg0)
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            // MODIFIED
            //beginMethod(arg0, Callback(endMethod, tcs), null);
            IAsyncResult asyncResult = beginMethod(arg0, Callback(endMethod, tcs), null);
            if (asyncResult.CompletedSynchronously)
                CallEndMethod(asyncResult, endMethod, tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Wraps a begin/end asynchronous method.
        /// </summary>
        /// <typeparam name="TArg0">The type of argument 0.</typeparam>
        /// <typeparam name="TArg1">The type of argument 1.</typeparam>
        /// <param name="beginMethod">The begin method.</param>
        /// <param name="endMethod">The end method.</param>
        /// <param name="arg0">Argument 0.</param>
        /// <param name="arg1">Argument 1.</param>
        /// <returns>The result of the asynchronous operation.</returns>
        public static Task<TResult> FromApm<TArg0, TArg1>(Func<TArg0, TArg1, AsyncCallback, object, IAsyncResult> beginMethod, Func<IAsyncResult, TResult> endMethod, TArg0 arg0, TArg1 arg1)
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            // MODIFIED
            //beginMethod(arg0, arg1, Callback(endMethod, tcs), null);
            IAsyncResult asyncResult = beginMethod(arg0, arg1, Callback(endMethod, tcs), null);
            if (asyncResult.CompletedSynchronously)
                CallEndMethod(asyncResult, endMethod, tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Wraps a begin/end asynchronous method.
        /// </summary>
        /// <typeparam name="TArg0">The type of argument 0.</typeparam>
        /// <typeparam name="TArg1">The type of argument 1.</typeparam>
        /// <typeparam name="TArg2">The type of argument 2.</typeparam>
        /// <param name="beginMethod">The begin method.</param>
        /// <param name="endMethod">The end method.</param>
        /// <param name="arg0">Argument 0.</param>
        /// <param name="arg1">Argument 1.</param>
        /// <param name="arg2">Argument 2.</param>
        /// <returns>The result of the asynchronous operation.</returns>
        public static Task<TResult> FromApm<TArg0, TArg1, TArg2>(Func<TArg0, TArg1, TArg2, AsyncCallback, object, IAsyncResult> beginMethod, Func<IAsyncResult, TResult> endMethod, TArg0 arg0, TArg1 arg1, TArg2 arg2)
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            // MODIFIED
            //beginMethod(arg0, arg1, arg2, Callback(endMethod, tcs), null);
            IAsyncResult asyncResult = beginMethod(arg0, arg1, arg2, Callback(endMethod, tcs), null);
            if (asyncResult.CompletedSynchronously)
                CallEndMethod(asyncResult, endMethod, tcs);
            return tcs.Task;
        }

        #endregion

        /// <summary>
        /// Wraps a <see cref="Task{TResult}"/> into the Begin method of an APM pattern.
        /// </summary>
        /// <param name="task">The task to wrap. May not be <c>null</c>.</param>
        /// <param name="callback">The callback method passed into the Begin method of the APM pattern.</param>
        /// <param name="state">The state passed into the Begin method of the APM pattern.</param>
        /// <returns>The asynchronous operation, to be returned by the Begin method of the APM pattern.</returns>
        public static IAsyncResult ToBegin(Task<TResult> task, AsyncCallback callback, object state)
        {
            // MODIFIED
            if (task.IsCompleted)
                return new AsyncResultCompletedSynchronously<TResult>(task, state);
            else
            {
                var tcs = new TaskCompletionSource<TResult>(state);
                task.ContinueWith((t) =>
                {
                    // MODIFIED
                    //tcs.TryCompleteFromCompletedTask(t);
                    if (task.IsFaulted)
                        tcs.TrySetException(task.Exception.InnerExceptions);
                    else if (task.IsCanceled)
                        tcs.TrySetCanceled();
                    else
                        tcs.TrySetResult(task.Result);

                    if (callback != null)
                        callback(tcs.Task);
                // MODIFIED
                //}, TaskScheduler.Default);
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
                return tcs.Task;
            }
        }

        /// <summary>
        /// Wraps a <see cref="Task{TResult}"/> into the End method of an APM pattern.
        /// </summary>
        /// <param name="asyncResult">The asynchronous operation returned by the matching Begin method of this APM pattern.</param>
        /// <returns>The result of the asynchronous operation, to be returned by the End method of the APM pattern.</returns>
        public static TResult ToEnd(IAsyncResult asyncResult)
        {
            // MODIFIED
            AsyncResultCompletedSynchronously<TResult> asyncResultCompletedSynchronously = asyncResult as AsyncResultCompletedSynchronously<TResult>;
            if (asyncResultCompletedSynchronously != null)
                return ((Task<TResult>)asyncResultCompletedSynchronously).GetAwaiter().GetResult();
            else
            {
                // MODIFIED
                //return ((Task<TResult>)asyncResult).WaitAndUnwrapException();
                return ((Task<TResult>)asyncResult).GetAwaiter().GetResult();
            }
        }
    }
}
