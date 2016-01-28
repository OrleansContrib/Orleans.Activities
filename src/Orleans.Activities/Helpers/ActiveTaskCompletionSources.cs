using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities.Helpers
{
    public class ActiveTaskCompletionSources
    {
        private abstract class ActiveTaskCompletionSource
        {
            public abstract void TrySetException(Exception exception);
            public abstract void TrySetResult();
        }

        private class ActiveTaskCompletionSource<TResult> : ActiveTaskCompletionSource
        {
            private TaskCompletionSource<TResult> taskCompletionSource;

            public ActiveTaskCompletionSource(TaskCompletionSource<TResult> taskCompletionSource)
            {
                this.taskCompletionSource = taskCompletionSource;
            }

            public override void TrySetException(Exception exception)
            {
                taskCompletionSource.TrySetException(exception);
            }

            public override void TrySetResult()
            {
                taskCompletionSource.TrySetResult(default(TResult));
            }
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

        public bool TryStoreException(Exception exception)
        {
            if (storedException != null
                || activeTaskCompletionSources.Count() == 0)
                return false;
            storedException = exception;
            return true;
        }

        public void TrySetCompletedEach()
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
                    activeTaskCompletionSource.TrySetResult();
        }
    }
}
