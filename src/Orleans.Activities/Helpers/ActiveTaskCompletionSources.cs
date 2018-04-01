using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities.Helpers
{
    /// <summary>
    /// Helps WorkflowHost to always complete all the active/pending taskCompletionSource-s of the incoming operations.
    /// <para>During preparation, the Preparation protection is used, task is completed or exception is set only when the workflow goes idle.</para>
    /// <para>During operations/reminders, the Normal protection is used, task's exception is set immediatelly when the OnUnhandledException is called.</para>
    /// </summary>
    public class ActiveTaskCompletionSources
    {
        private abstract class ActiveTaskCompletionSource
        {
            public abstract bool TrySetException(Exception exception);
            public abstract bool TrySetDefaultResult();
            public abstract bool IsCompleted { get; }
        }

        private class ActiveTaskCompletionSource<TResult> : ActiveTaskCompletionSource
        {
            private TaskCompletionSource<TResult> taskCompletionSource;

            public ActiveTaskCompletionSource(TaskCompletionSource<TResult> taskCompletionSource)
                => this.taskCompletionSource = taskCompletionSource;

            public override bool TrySetException(Exception exception) => this.taskCompletionSource.TrySetException(exception);
            public override bool TrySetDefaultResult() => this.taskCompletionSource.TrySetResult(default);
            public override bool IsCompleted => this.taskCompletionSource.Task.IsCompleted;
        }

        public enum TaskCompletionSourceProtectionLevel
        {
            /// <summary>
            /// Task's exception is set immediatelly when the OnUnhandledException is called.
            /// </summary>
            Normal,
            /// <summary>
            /// Task is completed or exception is set only when the workflow goes idle.
            /// </summary>
            Preparation,
        }

        public TaskCompletionSourceProtectionLevel ProtectionLevel { get; set; }

        private Dictionary<object, ActiveTaskCompletionSource> activeTaskCompletionSources = new Dictionary<object, ActiveTaskCompletionSource>();
        private Exception storedException;

        public void Add<TResult>(TaskCompletionSource<TResult> taskCompletionSource)
            => this.activeTaskCompletionSources.Add(taskCompletionSource, new ActiveTaskCompletionSource<TResult>(taskCompletionSource));

        public void Remove<TResult>(TaskCompletionSource<TResult> taskCompletionSource)
            => this.activeTaskCompletionSources.Remove(taskCompletionSource);

        private bool TryStoreException(Exception exception)
        {
            if (this.storedException != null
                || !this.activeTaskCompletionSources.Any((kvp) => !kvp.Value.IsCompleted))
                return false;
            this.storedException = exception;
            return true;
        }

        public bool TrySetException(Exception exception)
        {
            if (this.ProtectionLevel == TaskCompletionSourceProtectionLevel.Preparation)
                return TryStoreException(exception);
            else // TaskCompletionSourceProtectionLevel.Normal
            {
                var result = false;
                // If any TCS accepts the exception, we are successful, ie. we don't need to propagate the exception with OnUnhandledException later.
                foreach (var activeTaskCompletionSource in this.activeTaskCompletionSources.Values.ToList())
                    if (activeTaskCompletionSource.TrySetException(exception))
                        result = true;
                return result;
            }
        }

        public bool TrySetCompleted()
        {
            if (this.storedException != null) // TaskCompletionSourceProtectionLevel.Preparation
            {
                var storedException = this.storedException;
                this.storedException = null;
                // We assume, that this will be always successful, because nothing else will set this TCS during Preparation.
                foreach (var activeTaskCompletionSource in this.activeTaskCompletionSources.Values.ToList())
                    activeTaskCompletionSource.TrySetException(storedException);
                return true;
            }
            else if (this.ProtectionLevel == TaskCompletionSourceProtectionLevel.Preparation)
            {
                foreach (var activeTaskCompletionSource in this.activeTaskCompletionSources.Values.ToList())
                    activeTaskCompletionSource.TrySetDefaultResult();
                return true;
            }
            return false;
        }
    }
}
