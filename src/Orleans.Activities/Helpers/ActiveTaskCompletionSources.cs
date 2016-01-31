using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities.Helpers
{
    /// <summary>
    /// Helps WorkflowHost to always complete all the active/pending taskCompletionSource-s of the incoming operations.
    /// <para>During activation, the UnhandledExceptionAndNormalCompletion protection is used, task is completed or exception is set only when the workflow goes idle.</para>
    /// <para>During operations, the UnhandledExceptionOnly protection is used, task's exception is set immediatelly when the OnUnhandledException is called.</para>
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
            UnhandledExceptionOnly,
            UnhandledExceptionAndNormalCompletion,
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
            if (ProtectionLevel == TaskCompletionSourceProtectionLevel.UnhandledExceptionAndNormalCompletion)
                return TryStoreException(exception);
            else
            {
                bool result = false;
                foreach (ActiveTaskCompletionSource activeTaskCompletionSource in activeTaskCompletionSources.Values.ToList())
                    if (activeTaskCompletionSource.TrySetException(exception))
                        result = true;
                return result;
            }
        }

        public void TrySetCompleted()
        {
            if (storedException != null)
            {
                Exception storedException = this.storedException;
                this.storedException = null;
                foreach (ActiveTaskCompletionSource activeTaskCompletionSource in activeTaskCompletionSources.Values.ToList())
                    activeTaskCompletionSource.TrySetException(storedException);
            }
            else if (ProtectionLevel == TaskCompletionSourceProtectionLevel.UnhandledExceptionAndNormalCompletion)
                foreach (ActiveTaskCompletionSource activeTaskCompletionSource in activeTaskCompletionSources.Values.ToList())
                    activeTaskCompletionSource.TrySetDefaultResult();
        }
    }
}
