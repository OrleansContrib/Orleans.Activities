using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;

namespace Orleans.Activities.Helpers
{
    /// <summary>
    /// A child activity to evaluate the operation's requestResult Func&lt;Task&gt; and responseResult's Func&lt;Task&gt;.
    /// </summary>
    public sealed class TaskFuncEvaluator : TaskAsyncCodeActivity
    {
        public static ActivityAction<Func<Task>> CreateActivityDelegate()
        {
            var resultTaskFunc = new DelegateInArgument<Func<Task>>();
            return new ActivityAction<Func<Task>>()
            {
                Argument = resultTaskFunc,
                Handler = new TaskFuncEvaluator()
                {
                    ResultTaskFunc = resultTaskFunc,
                },
            };
        }

        public InArgument<Func<Task>> ResultTaskFunc { get; set; }

        protected override Task ExecuteAsync(AsyncCodeActivityContext context) => this.ResultTaskFunc.Get(context)();
    }

    /// <summary>
    /// A child activity to evaluate the operation's requestResult Func&lt;Task&lt;TRequestResult&gt;&gt; and responseResult's Func&lt;Task&lt;TResponseResult&gt;&gt;.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    public sealed class TaskFuncEvaluator<TResult> : TaskAsyncCodeActivityWithResult<TResult>
    {
        public static ActivityFunc<Func<Task<TResult>>, TResult> CreateActivityDelegate()
        {
            var resultTaskFunc = new DelegateInArgument<Func<Task<TResult>>>();
            var result = new DelegateOutArgument<TResult>();
            return new ActivityFunc<Func<Task<TResult>>, TResult>()
            {
                Argument = resultTaskFunc,
                Result = result,
                Handler = new TaskFuncEvaluator<TResult>()
                {
                    ResultTaskFunc = resultTaskFunc,
                    Result = result,
                },
            };
        }

        public InArgument<Func<Task<TResult>>> ResultTaskFunc { get; set; }

        protected override Task<TResult> ExecuteAsync(AsyncCodeActivityContext context) => this.ResultTaskFunc.Get(context)();
    }
}
