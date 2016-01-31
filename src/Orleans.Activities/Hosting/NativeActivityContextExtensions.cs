using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Orleans.Activities.Hosting
{
    /// <summary>
    /// Extension methods compiled at static run time to access the internal members.
    /// </summary>
    public static class NativeActivityContextExtensions
    {
        private static Action<NativeActivityContext> cancelWorkflow;

        static NativeActivityContextExtensions()
        {
            ParameterExpression instance = Expression.Parameter(typeof(NativeActivityContext), "this");
            FieldInfo executor = typeof(NativeActivityContext).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo cancelRootActivity = typeof(Activity).Assembly.GetType("System.Activities.Runtime.ActivityExecutor").GetMethod("CancelRootActivity", BindingFlags.Instance | BindingFlags.Public);

            cancelWorkflow = Expression.Lambda<Action<NativeActivityContext>>(
                Expression.Call(Expression.Field(instance, executor), cancelRootActivity), instance).Compile();
        }

        // TODO It is a brutal hack, but works. With one serious flaw: the completion state is Closed and not Canceled.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CancelWorkflow(this NativeActivityContext context)
        {
            cancelWorkflow(context);
        }
    }
}
