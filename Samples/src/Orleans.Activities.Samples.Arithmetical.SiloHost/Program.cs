using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime.Configuration;

using Orleans.Activities.Samples.Arithmetical.GrainInterfaces;

namespace Orleans.Activities.Samples.Arithmetical.SiloHost
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly")]
        public class MultiplierResultReceiver : IMultiplierResultReceiver, IDisposable
        {
            private AutoResetEvent completed;

            public MultiplierResultReceiver()
            {
                completed = new AutoResetEvent(false);
            }

            public void WaitForCompletion()
            {
                completed.WaitOne(TimeSpan.FromSeconds(10));
            }

            public void ReceiveResult(int result)
            {
                WriteLine("Multiplier result received\n\n");
                WriteLine($"{result}\n\n");
                completed.Set();
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly")]
            public void Dispose()
            {
                completed.Dispose();
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

            var config = ClientConfiguration.LocalhostSilo();
            GrainClient.Initialize(config);

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
                multiplierGrain.SubscribeAsync(obj).Wait();

                WriteLine("Calling Multiply...\n\n");
                multiplierGrain.MultiplyAsync(2, 3).Wait();

                WriteLine("\n\nWaiting for result...\n\n");
                multiplierResultReceiver.WaitForCompletion();

                WriteLine("--- Idempotent forward recovery is not applicable for this situation, grain would repeat the callback in case of failure after reactivation from it's previously persisted state, the Multiply operation would simply do nothing when repeated. ---\n\n");
                WriteLine("Wait the workflow to complete...\n\n");
                for (int i = 5; i > 0; --i)
                {
                    if (i % 5 == 0)
                        Write(i.ToString());
                    else
                        Write(".");
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }

                WriteLine("\n\nCalling Multiply again...\n");
                multiplierGrain.MultiplyAsync(3, 3).Wait();

                WriteLine("\n\nWaiting for result...\n\n");
                multiplierResultReceiver.WaitForCompletion();

                WriteLine("Unsubscribing from Multiplier...\n\n");
                multiplierGrain.UnsubscribeAsync(obj).Wait();
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
