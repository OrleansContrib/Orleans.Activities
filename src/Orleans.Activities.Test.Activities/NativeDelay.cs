using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Threading;
using Orleans.Activities;
using Orleans.Activities.AsyncEx;

namespace Orleans.Activities.Test.Activities
{
    public class NativeDelay : CancellableTaskAsyncNativeActivityWithResult<bool>
    {
        protected override async Task<bool> ExecuteAsync(NativeActivityContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            return true;
        }
    }
}
