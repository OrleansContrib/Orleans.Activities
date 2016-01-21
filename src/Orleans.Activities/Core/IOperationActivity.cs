using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using Orleans.Activities.Designers.Binding;
using Orleans.Activities.Helpers;

namespace Orleans.Activities
{
    /// <summary>
    /// Marker for the designers: marks the operation receive/send activities to store the possible operation names during design time.
    /// </summary>
    public interface IOperationActivity
    {
        /// <summary>
        /// Used by <see cref="OperationActivityHelper.VerifyIsOperationNameSet"/> validation constraint. Implementation practically have a setter.
        /// </summary>
        string OperationName { get; }

        /// <summary>
        /// Used by <see cref="OperationActivityHelper.SetOperationNames"/> validation constraint. The ObservableCollection have a Set() method, the collection can't be replaced.
        /// </summary>
        ObservableCollection<string> OperationNames { get; }
    }

    public static class IOperationActivityExtensions
    {
        public static bool IsOperationNameSet(this Activity activity) =>
            !string.IsNullOrEmpty((activity as IOperationActivity)?.OperationName);

        public static void SetOperationNames(this Activity activity, IEnumerable<string> operationNames)
        {
            (activity as IOperationActivity)?.OperationNames.Set(operationNames);
        }
    }
}
