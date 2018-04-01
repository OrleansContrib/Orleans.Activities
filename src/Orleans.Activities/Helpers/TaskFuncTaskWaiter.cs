using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;

namespace Orleans.Activities.Helpers
{
    /// <summary>
    /// A child activity to wait for the operation's responseResult Task (Task&lt;Func&lt;Task&gt;&gt; or Task&lt;Func&lt;Task&lt;TResponseResult&gt;&gt;&gt; in fact).
    /// </summary>
    public sealed class TaskWaiter : TaskAsyncCodeActivity
    {
        public static ActivityAction<Task> CreateActivityDelegate()
        {
            var resultTask = new DelegateInArgument<Task>();
            return new ActivityAction<Task>()
            {
                Argument = resultTask,
                Handler = new TaskWaiter()
                {
                    ResultTask = resultTask,
                },
            };
        }

        public InArgument<Task> ResultTask { get; set; }

        protected override Task ExecuteAsync(AsyncCodeActivityContext context) => this.ResultTask.Get(context);
    }

    /// <summary>
    /// A child activity to wait for the operation's responseResult Task&lt;Func&lt;Task&gt;&gt;.
    /// </summary>
    public sealed class TaskFuncTaskWaiter : TaskAsyncCodeActivityWithResult<Func<Task>>
    {
        public static ActivityFunc<Task<Func<Task>>, Func<Task>> CreateActivityDelegate()
        {
            var resultTaskFuncTask = new DelegateInArgument<Task<Func<Task>>>();
            var result = new DelegateOutArgument<Func<Task>>();
            return new ActivityFunc<Task<Func<Task>>, Func<Task>>()
            {
                Argument = resultTaskFuncTask,
                Result = result,
                Handler = new TaskFuncTaskWaiter()
                {
                    ResultTaskFuncTask = resultTaskFuncTask,
                    Result = result,
                },
            };
        }

        public InArgument<Task<Func<Task>>> ResultTaskFuncTask { get; set; }

        protected override Task<Func<Task>> ExecuteAsync(AsyncCodeActivityContext context) => this.ResultTaskFuncTask.Get(context);
    }

    /// <summary>
    /// A child activity to wait for the operation's responseResult Task&lt;Func&lt;Task&lt;TResponseResult&gt;&gt;&gt;.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    public sealed class TaskFuncTaskWaiter<TResult> : TaskAsyncCodeActivityWithResult<Func<Task<TResult>>>
    {
        public static ActivityFunc<Task<Func<Task<TResult>>>, Func<Task<TResult>>> CreateActivityDelegate()
        {
            var resultTaskFuncTask = new DelegateInArgument<Task<Func<Task<TResult>>>>();
            var result = new DelegateOutArgument<Func<Task<TResult>>>();
            return new ActivityFunc<Task<Func<Task<TResult>>>, Func<Task<TResult>>>()
            {
                Argument = resultTaskFuncTask,
                Result = result,
                Handler = new TaskFuncTaskWaiter<TResult>()
                {
                    ResultTaskFuncTask = resultTaskFuncTask,
                    Result = result,
                },
            };
        }

        public InArgument<Task<Func<Task<TResult>>>> ResultTaskFuncTask { get; set; }

        protected override Task<Func<Task<TResult>>> ExecuteAsync(AsyncCodeActivityContext context) => this.ResultTaskFuncTask.Get(context);
    }
}
