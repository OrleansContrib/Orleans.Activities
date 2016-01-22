using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using Orleans.Activities.Helpers;

namespace Orleans.Activities
{
    /// <summary>
    /// Marker for the designers: marks the de facto "root" activity to get the TWorkflowInterface and TWorkflowCallbackInterface types.
    /// <para>Due to the limitations of the designer, the base class of the XAML designed activity, ie. the workflow must be Activity.
    /// If we use the general ReceiveRequest, SendResponse, SendRequest, ReceiveResponse activities, a WorkflowActivity must be the top activity under the workflow's "root" activity.
    /// See <see cref="WorkflowGrain{TWorkflowState, TWorkflowInterface, TWorkflowCallbackInterface}"/> for the description of the type parameters.</para>  
    /// </summary>
    /// <typeparam name="TWorkflowInterface"></typeparam>
    /// <typeparam name="TWorkflowCallbackInterface"></typeparam>
    public interface IWorkflowActivity<TWorkflowInterface, TWorkflowCallbackInterface>
        where TWorkflowInterface : class
        where TWorkflowCallbackInterface : class
    { }

    public static class IWorkflowActivityExtensions
    {
        public static bool IsWorkflowActivity(this Activity activity) =>
            activity
                .GetType()
                .GetInterfaces()
                .Any((i) => i.IsGenericTypeOf(typeof(IWorkflowActivity<,>)));

        public static Type GetWorkflowActivityType(this Activity activity)
        {
            Type iWorkflowActivityType = activity
                .GetType()
                .GetInterfaces()
                .Where((i) => i.IsGenericTypeOf(typeof(IWorkflowActivity<,>)))
                .FirstOrDefault();
            if (iWorkflowActivityType == null)
                throw new ArgumentException($"Activity '{activity.GetType().GetFriendlyName()}' is not an IWorkflowActivity<,> activity.");
            return iWorkflowActivityType;
        }
    }
}
