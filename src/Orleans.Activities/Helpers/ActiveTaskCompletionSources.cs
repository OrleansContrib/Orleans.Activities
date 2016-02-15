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
            {
                this.taskCompletionSource = taskCompletionSource;
            }

            public override bool TrySetException(Exception exception) => taskCompletionSource.TrySetException(exception);
            public override bool TrySetDefaultResult() => taskCompletionSource.TrySetResult(default(TResult));
            public override bool IsCompleted => taskCompletionSource.Task.IsCompleted;
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

        private Dictionary<object, ActiveTaskCompletionSource> activeTaskCompletionSources;
        private Exception storedException;

        public ActiveTaskCompletionSources()
        {
            activeTaskCompletionSources = new Dictionary<object, ActiveTaskCompletionSource>();
        }

        public void Add<TResult>(TaskCompletionSource<TResult> taskCompletionSource)
        {
            activeTaskCompletionSources.Add(taskCompletionSource, new ActiveTaskCompletionSource<TResult>(taskCompletionSource));
        }

        public void Remove<TResult>(TaskCompletionSource<TResult> taskCompletionSource)
        {
            activeTaskCompletionSources.Remove(taskCompletionSource);
        }

        private bool TryStoreException(Exception exception)
        {
            if (storedException != null
                || !activeTaskCompletionSources.Any((kvp) => !kvp.Value.IsCompleted))
                return false;
            storedException = exception;
            return true;
        }

        public bool TrySetException(Exception exception)
        {
            if (ProtectionLevel == TaskCompletionSourceProtectionLevel.Preparation)
                return TryStoreException(exception);
            else // TaskCompletionSourceProtectionLevel.Normal
            {
                bool result = false;
                // If any TCS accepts the exception, we are successful, ie. we don't need to propagate the exception with OnUnhandledException later.
                foreach (ActiveTaskCompletionSource activeTaskCompletionSource in activeTaskCompletionSources.Values.ToList())
                    if (activeTaskCompletionSource.TrySetException(exception))
                        result = true;
                return result;
            }
        }

        public bool TrySetCompleted()
        {
            if (storedException != null) // TaskCompletionSourceProtectionLevel.Preparation
            {
                Exception storedException = this.storedException;
                this.storedException = null;
                // We assume, that this will be always successful, because nothing else will set this TCS during Preparation.
                foreach (ActiveTaskCompletionSource activeTaskCompletionSource in activeTaskCompletionSources.Values.ToList())
                    activeTaskCompletionSource.TrySetException(storedException);
                return true;
            }
            else if (ProtectionLevel == TaskCompletionSourceProtectionLevel.Preparation)
            {
                foreach (ActiveTaskCompletionSource activeTaskCompletionSource in activeTaskCompletionSources.Values.ToList())
                    activeTaskCompletionSource.TrySetDefaultResult();
                return true;
            }
            return false;
        }
    }
}
