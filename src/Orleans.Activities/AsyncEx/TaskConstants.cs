// original source https://github.com/StephenCleary/AsyncEx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities.AsyncEx
{
    /// <summary>
    /// Provides completed task constants.
    /// </summary>
    public static class TaskConstants
    {
        // MODIFIED
        //private static readonly Task<bool> booleanTrue = TaskShim.FromResult(true);
        //private static readonly Task<bool> booleanTrue = Task.FromResult(true);
        // MODIFIED
        //private static readonly Task<int> intNegativeOne = TaskShim.FromResult(-1);
        //private static readonly Task<int> intNegativeOne = Task.FromResult(-1);

        // MODIFIED
        //private static readonly Task<string> stringEmpty = Task.FromResult(string.Empty);

        /// <summary>
        /// A task that has been completed with the value <c>true</c>.
        /// </summary>
        public static readonly Task<bool> BooleanTrue = Task.FromResult(true);

        /// <summary>
        /// A task that has been completed with the value <c>false</c>.
        /// </summary>
        public static Task<bool> BooleanFalse => TaskConstants<bool>.Default;

        /// <summary>
        /// A task that has been completed with the value <c>0</c>.
        /// </summary>
        public static Task<int> Int32Zero => TaskConstants<int>.Default;

        /// <summary>
        /// A task that has been completed with the value <c>-1</c>.
        /// </summary>
        public static readonly Task<int> Int32NegativeOne = Task.FromResult(-1);

        // MODIFIED
        /// <summary>
        /// A task that has been completed with the value <c>string.Empty</c>.
        /// </summary>
        public static readonly Task<string> StringEmpty = Task.FromResult(string.Empty);

        /// <summary>
        /// A <see cref="Task"/> that has been completed.
        /// </summary>
        public static Task Completed => BooleanTrue;

        // MODIFIED
        ///// <summary>
        ///// A <see cref="Task"/> that will never complete.
        ///// </summary>
        //public static Task Never => TaskConstants<bool>.Never;

        /// <summary>
        /// A task that has been canceled.
        /// </summary>
        public static Task Canceled => TaskConstants<bool>.Canceled;
    }

    /// <summary>
    /// Provides completed task constants.
    /// </summary>
    /// <typeparam name="T">The type of the task result.</typeparam>
    public static class TaskConstants<T>
    {
        // MODIFIED
        //private static readonly Task<T> defaultValue = TaskShim.FromResult(default(T));
        //private static readonly Task<T> defaultValue = Task.FromResult(default(T));

        // MODIFIED
        //private static readonly Task<T> never = new TaskCompletionSource<T>().Task;

        //private static readonly Task<T> canceled = CanceledTask();

        private static Task<T> CanceledTask()
        {
            var tcs = new TaskCompletionSource<T>();
            tcs.SetCanceled();
            return tcs.Task;
        }

        /// <summary>
        /// A task that has been completed with the default value of <typeparamref name="T"/>.
        /// </summary>
        public static readonly Task<T> Default = Task.FromResult(default(T));

        // MODIFIED
        ///// <summary>
        ///// A <see cref="Task"/> that will never complete.
        ///// </summary>
        //public static Task<T> Never => never;

        /// <summary>
        /// A task that has been canceled.
        /// </summary>
        public static readonly Task<T> Canceled = CanceledTask();
    }
}
