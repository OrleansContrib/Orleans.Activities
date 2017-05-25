using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime.Configuration;

using Orleans.Activities.Samples.HelloWorld.GrainInterfaces;

namespace Orleans.Activities.Samples.HelloWorld.SiloHost
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

            var helloGrain = GrainClient.GrainFactory.GetGrain<IHello>(Guid.NewGuid());
            try
            {
                WriteLine("\n\nCalling SayHello...\n\n");
                WriteLine($"\n\n{helloGrain.SayHello("Good morning, my friend!").Result}\n\n");

                WriteLine("Let see idempotent forward recovery, calling SayHello again...\n\n");
                WriteLine($"\n\n{helloGrain.SayHello("Ooops").Result}\n\n");

                WriteLine("Wait to timeout the waiting for our farewell...\n\n");
                for (int i = 10; i > 0; --i)
                {
                    if (i % 5 == 0)
                        Write(i.ToString());
                    else
                        Write(".");
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }

                WriteLine("\n\n\nCalling SayBye...\n\n");
                WriteLine($"{helloGrain.SayBye().Result}\n\n");
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
