/*
Project Orleans Cloud Service SDK ver. 1.0
 
Copyright (c) Microsoft Corporation
 
All rights reserved.
 
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
associated documentation files (the ""Software""), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO
THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Threading.Tasks;

using Orleans.Activities.Sample.Arithmetical.GrainInterfaces;
using System.Threading;

namespace Orleans.Activities.Sample.Arithmetical.SiloHost
{
    /// <summary>
    /// Orleans test silo host
    /// </summary>
    public class Program
    {
        private static void WriteLine(string message)
        {
            ConsoleColor backgroundColor = Console.BackgroundColor;
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message);
            Console.BackgroundColor = backgroundColor;
        }

        private static void Write(string message)
        {
            ConsoleColor backgroundColor = Console.BackgroundColor;
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(message);
            Console.BackgroundColor = backgroundColor;
        }

        public class MultiplierResultReceiver : IMultiplierResultReceiver
        {
            private AutoResetEvent completed;

            public MultiplierResultReceiver()
            {
                completed = new AutoResetEvent(false);
            }

            public void WaitForCompletion()
            {
                completed.WaitOne(TimeSpan.FromSeconds(5));
            }

            public void ReceiveResult(int result)
            {
                WriteLine("Multiplier result received\n\n");
                WriteLine($"{result}\n\n");
                completed.Set();
            }
        }

        static void Main(string[] args)
        {
            // The Orleans silo environment is initialized in its own app domain in order to more
            // closely emulate the distributed situation, when the client and the server cannot
            // pass data via shared memory.
            AppDomain hostDomain = AppDomain.CreateDomain("OrleansHost", null, new AppDomainSetup
            {
                AppDomainInitializer = InitSilo,
                AppDomainInitializerArguments = args,
            });

            Orleans.GrainClient.Initialize("DevTestClientConfiguration.xml");

            // TODO: once the previous call returns, the silo is up and running.
            //       This is the place your custom logic, for example calling client logic
            //       or initializing an HTTP front end for accepting incoming requests.

            var adderGrain = GrainClient.GrainFactory.GetGrain<IAdder>(Guid.NewGuid());
            var multiplierGrain = GrainClient.GrainFactory.GetGrain<IMultiplier>(Guid.NewGuid());
            try
            {
                WriteLine("\n\nCalling Add...\n\n");
                WriteLine($"\n\n{adderGrain.AddAsync(2, 3).Result}\n\n");

                WriteLine("Let see idempotent forward recovery, calling Add again...\n\n");
                WriteLine($"{adderGrain.AddAsync(4, 5).Result}\n\n");

                WriteLine("Subscribing to Multiplier...\n\n");
                var multiplierResultReceiver = new MultiplierResultReceiver();
                var obj = GrainClient.GrainFactory.CreateObjectReference<IMultiplierResultReceiver>(multiplierResultReceiver).Result;
                multiplierGrain.Subscribe(obj).Wait();

                WriteLine("Calling Multiply...\n\n");
                multiplierGrain.MultiplyAsync(2, 3).Wait();

                WriteLine("\n\nWaiting for result...\n\n");
                multiplierResultReceiver.WaitForCompletion();

                WriteLine("--- Idempotent forward recovery is not applicable for this situation, grain would repeat the callback in case of failure if it was previously persisted before completion, the Multiply operation would simply do nothing when repeated. ---\n\n");

                WriteLine("Unsubscribing from Multiplier...\n\n");
                multiplierGrain.Unsubscribe(obj).Wait();
            }
            catch (Exception e)
            {
                WriteLine($"\n\n{e.ToString()}\n\n");
            }

            WriteLine("\n\nOrleans Silo is running.\nPress Enter to terminate...\n\n");
            Console.ReadLine();

            hostDomain.DoCallBack(ShutdownSilo);
        }

        static void InitSilo(string[] args)
        {
            hostWrapper = new OrleansHostWrapper(args);

            if (!hostWrapper.Run())
            {
                Console.Error.WriteLine("Failed to initialize Orleans silo");
            }
        }

        static void ShutdownSilo()
        {
            if (hostWrapper != null)
            {
                hostWrapper.Dispose();
                GC.SuppressFinalize(hostWrapper);
            }
        }

        private static OrleansHostWrapper hostWrapper;
    }
}
