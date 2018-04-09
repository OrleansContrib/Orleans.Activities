using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Hosting;

using Orleans.Activities.Samples.HelloWorld.GrainInterfaces;

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
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(IHelloGrain).Assembly).WithReferences())
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
            lock(Console.Out)
            {
                Console.BackgroundColor = ConsoleColor.White;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(message);
                Console.ResetColor();
            }
        }

        #endregion

        private static async Task DoClientWorkAsync(IClusterClient client)
        {
            var helloGrain = client.GetGrain<IHelloGrain>(Guid.NewGuid());
            WriteLine("\n\nCalling SayHello...\n\n");
            WriteLine($"\n\n{await helloGrain.SayHelloAsync("Good morning, my friend!")}\n\n");

            WriteLine("Let see idempotent forward recovery, calling SayHello again...\n\n");
            WriteLine($"{await helloGrain.SayHelloAsync("Ooops")}\n\n");

            WriteLine("Wait to timeout the waiting for our farewell...\n\n");
            for (var i = 10; i > 0; --i)
            {
                if (i % 5 == 0)
                    Write(i.ToString());
                else
                    Write(".");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            WriteLine("\n\n\nCalling SayBye...\n\n");
            WriteLine($"{await helloGrain.SayByeAsync()}\n\n");
        }
    }
}
