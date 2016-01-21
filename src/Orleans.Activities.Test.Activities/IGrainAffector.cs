using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Orleans.Activities.Helpers;

namespace Orleans.Activities.Test.Activities
{
    public interface IGrainAffector
    {
        Task<string> OperationWithParamsAsync(Func<Task<string>> requestResult);
        Task OperationWithoutParamsAsync(Func<Task> requestResult);
    }
}
