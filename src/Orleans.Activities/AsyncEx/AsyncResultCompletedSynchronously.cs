using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities.AsyncEx
{
    // Because Task's IAsyncResult.CompletedSynchronously returns constant false...
    // Inside a grain, a Task can't run on another thread, so Task.IsCompleted is logically equal to Task.IsCompletedSynchronously at the moment we convert it to IAsyncResult.
    // IAsyncResult.CompletedSynchronously determines when the endMethod is called: this is important if we do TAP -> APM -> TAP conversions.

    public class AsyncResultCompletedSynchronously : IAsyncResult
    {
        private Task task;
        private object state;

        public AsyncResultCompletedSynchronously(Task task)
            : this(task, (task as IAsyncResult).AsyncState)
        { }

        public AsyncResultCompletedSynchronously(Task task, object state)
        {
            this.task = task;
            this.state = state;
        }

        public static explicit operator Task(AsyncResultCompletedSynchronously @this) => @this.task;

        public object AsyncState => this.state;

        public System.Threading.WaitHandle AsyncWaitHandle => (this.task as IAsyncResult).AsyncWaitHandle;

        public bool CompletedSynchronously => true;

        public bool IsCompleted => true;
    }

    public class AsyncResultCompletedSynchronously<TResult> : IAsyncResult
    {
        private Task<TResult> task;
        private object state;

        public AsyncResultCompletedSynchronously(Task<TResult> task)
            : this(task, (task as IAsyncResult).AsyncState)
        { }

        public AsyncResultCompletedSynchronously(Task<TResult> task, object state)
        {
            this.task = task;
            this.state = state;
        }

        public static explicit operator Task<TResult>(AsyncResultCompletedSynchronously<TResult> @this) => @this.task;

        public object AsyncState => this.state;

        public System.Threading.WaitHandle AsyncWaitHandle => (this.task as IAsyncResult).AsyncWaitHandle;

        public bool CompletedSynchronously => true;

        public bool IsCompleted => true;
    }
}
