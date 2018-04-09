using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Hosting;

using Orleans.Activities.Samples.Arithmetical.GrainImplementations;

namespace DevSiloHost
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            ISiloHost host = null;
            try
            {
                host = await StartSiloHostAsync();
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
                if (host != null)
                    await host.StopAsync();
            }
        }

        private static async Task<ISiloHost> StartSiloHostAsync()
        {
            var host = new SiloHostBuilder()
                .UseLocalhostClustering()
                .UseInMemoryReminderService()
                .AddMemoryGrainStorage("MemoryStore")
                .ConfigureLogging(logging => logging.AddConsole()
                    .AddFilter(typeof(AdderGrain).FullName, LogLevel.Trace)
                    .AddFilter(typeof(MultiplierGrain).FullName, LogLevel.Trace))
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(AdderGrain).Assembly).WithReferences())
                .Build();
            await host.StartAsync();
            return host;
        }
    }
}
