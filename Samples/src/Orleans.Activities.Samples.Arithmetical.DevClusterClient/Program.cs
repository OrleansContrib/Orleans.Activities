using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Hosting;

using System.Threading;
using Orleans.Activities.Samples.Arithmetical.GrainInterfaces;

namespace DevClusterClient
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            IClusterClient client = null;
            try
            {
                client = await StartClusterClientAsync();
                await DoClientWorkAsync(client);
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 1;
            }
            finally
            {
                Console.WriteLine("Press Enter to terminate...");
                Console.ReadKey();
                if (client != null)
                    await client.Close();
            }
        }

        private static async Task<IClusterClient> StartClusterClientAsync(int initializeAttemptsBeforeFailing = 5)
        {
            var client = new ClientBuilder()
                .UseLocalhostClustering()
                .ConfigureLogging(logging => logging.AddConsole())
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(IAdderGrain).Assembly).WithReferences())
                .Build();
            var attempt = 1;
            await client.Connect(async exception =>
                {
                    if (exception is SiloUnavailableException)
                    {
                        Console.WriteLine($"\nAttempt {attempt} of {initializeAttemptsBeforeFailing} failed to initialize the Orleans client.\n");
                        if (attempt++ < initializeAttemptsBeforeFailing)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(4));
                            return true;
                        }
                    }
                    return false;
                });
            Console.WriteLine("Client successfully connected to silo host.");
            return client;
        }

        #region Colored, timestamped console

        private static int IndexOf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            var i = 0;
            foreach (var element in source)
            {
                if (predicate(element))
                    return i;
                i++;
            }
            return -1;
        }

        private static void WriteLine(string message)
        {
            var pos = message.IndexOf<char>(c => !char.IsWhiteSpace(c));
            if (pos < 0)
                pos = 0;
            message = message.Insert(pos, $"[{DateTime.Now.ToString("s")}] ");
            Write($"{message}\n");
        }

        private static void Write(string message)
        {
            lock (Console.Out)
            {
                Console.BackgroundColor = ConsoleColor.White;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(message);
                Console.ResetColor();
            }
        }

        #endregion

        public class MultiplierResultReceiver : IMultiplierResultReceiver, IDisposable
        {
            private AutoResetEvent completed = new AutoResetEvent(false);

            public void WaitForCompletion() => this.completed.WaitOne(TimeSpan.FromSeconds(5));

            public void ReceiveResult(int result)
            {
                WriteLine($"Multiplier result received: {result}\n\n");
                this.completed.Set();
            }

            public void Dispose() => this.completed.Dispose();
        }

        private static async Task DoClientWorkAsync(IClusterClient client)
        {
            var adderGrain = client.GetGrain<IAdderGrain>(Guid.NewGuid());
            var multiplierGrain = client.GetGrain<IMultiplierGrain>(Guid.NewGuid());

            WriteLine("\n\nCalling AddAsync(2, 3)...\n\n");
            WriteLine($"\n\nResult: {await adderGrain.AddAsync(2, 3)}\n\n");

            WriteLine("Let see idempotent forward recovery, calling AddAsync(4, 5) again...\n\n");
            WriteLine($"Result: {await adderGrain.AddAsync(4, 5)}\n\n");

            WriteLine("Subscribing to Multiplier...\n\n");
            var multiplierResultReceiver = new MultiplierResultReceiver();
            var obj = await client.CreateObjectReference<IMultiplierResultReceiver>(multiplierResultReceiver);
            await multiplierGrain.SubscribeAsync(obj);

            WriteLine("Calling MultiplyAsync(2, 3)...\n\n");
            await multiplierGrain.MultiplyAsync(2, 3);

            WriteLine("Waiting for result... (there is a 5s delay in the Activity, you know, multiplication is slow...)\n\n");
            multiplierResultReceiver.WaitForCompletion();
            await Task.Delay(100); // wait for console to flush...

            WriteLine("Wait the workflow to complete...\n\n");
            for (var i = 5; i > 0; --i)
            {
                if (i % 5 == 0)
                    Write(i.ToString());
                else
                    Write(".");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            WriteLine("\n\n\nCalling MultiplyAsync(4, 5) again...\n");
            await multiplierGrain.MultiplyAsync(4, 5);

            WriteLine("\nWaiting for result... (the timeout is 5s, be patient...)\n\n");
            WriteLine("--- Idempotent forward recovery is not applicable for this situation, grain would repeat the callback in case of failure after reactivation from it's previously persisted state. ---");
            WriteLine("--- The Multiply operation will simply do nothing when repeated. ---\n\n");
            multiplierResultReceiver.WaitForCompletion();

            WriteLine("Unsubscribing from Multiplier...\n\n");
            await multiplierGrain.UnsubscribeAsync(obj);
        }
    }
}
