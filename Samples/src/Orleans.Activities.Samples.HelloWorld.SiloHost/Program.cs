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
using System.Threading;
using System.Threading.Tasks;

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

            Orleans.GrainClient.Initialize("DevTestClientConfiguration.xml");

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

                WriteLine("Wait to timeout the waiting for our farewell... (We have to wait at least 1 minute, this is the minimum Reminder time in Orleans.)\n\n");
                for (int i = 65; i > 0; --i)
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
