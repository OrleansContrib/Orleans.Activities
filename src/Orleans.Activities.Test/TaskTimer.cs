﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

namespace Orleans.Activities.Test
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly")]
    public class TaskTimer : IDisposable
    {
        private CancellationTokenSource cts;

        public TaskTimer(Func<Task> callback, TimeSpan dueTime, TimeSpan period)
        {
            this.cts = new CancellationTokenSource();
            Task.Factory.StartNew(async () =>
            {
                await Task.Yield();
                try
                {
                    if (dueTime > TimeSpan.Zero)
                        await Task.Delay(dueTime, this.cts.Token);
                    while (!this.cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await callback();
                        }
                        catch
                        { }
                        if (period > TimeSpan.Zero)
                            await Task.Delay(period, this.cts.Token);
                    }
                }
                catch
                { }
            });
        }

        public void Cancel() => this.cts.Cancel();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly")]
        public void Dispose() => this.cts.Dispose();
    }
}
