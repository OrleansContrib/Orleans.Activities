using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

namespace Orleans.Activities.Test
{
    public class TaskTimer : IDisposable
    {
        private CancellationTokenSource cts;

        public TaskTimer(Func<Task> callback, TimeSpan dueTime, TimeSpan period)
        {
            cts = new CancellationTokenSource();
            Task.Factory.StartNew(async () =>
            {
                await Task.Yield();
                try
                {
                    if (dueTime > TimeSpan.Zero)
                        await Task.Delay(dueTime, cts.Token);
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await callback();
                        }
                        catch
                        { }
                        if (period > TimeSpan.Zero)
                            await Task.Delay(period, cts.Token);
                    }
                }
                catch
                { }
            });
        }

        public void Cancel()
        {
            cts.Cancel();
        }

        public void Dispose()
        {
            cts.Cancel();
        }
    }
}
