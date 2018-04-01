using System;
using System.Net;
using System.Threading.Tasks;

using Orleans.Runtime.Configuration;
using Orleans.Runtime.Host;

namespace Orleans.Activities.Samples.Arithmetical.SiloHost
{
    internal class OrleansHostWrapper : IDisposable
    {
        public bool Debug
        {
            get => this.siloHost != null && this.siloHost.Debug;
            set => this.siloHost.Debug = value;
        }

        private Orleans.Runtime.Host.SiloHost siloHost;

        public OrleansHostWrapper(string[] args)
        {
            ParseArguments(args);
            Init();
        }

        public bool Run()
        {
            var ok = false;

            try
            {
                this.siloHost.InitializeOrleansSilo();

                ok = this.siloHost.StartOrleansSilo();

                if (ok)
                {
                    Console.WriteLine(string.Format("Successfully started Orleans silo '{0}' as a {1} node.", this.siloHost.Name, this.siloHost.Type));
                }
                else
                {
                    throw new SystemException(string.Format("Failed to start Orleans silo '{0}' as a {1} node.", this.siloHost.Name, this.siloHost.Type));
                }
            }
            catch (Exception exc)
            {
                this.siloHost.ReportStartupError(exc);
                var msg = string.Format("{0}:\n{1}\n{2}", exc.GetType().FullName, exc.Message, exc.StackTrace);
                Console.WriteLine(msg);
            }

            return ok;
        }

        public bool Stop()
        {
            var ok = false;

            try
            {
                this.siloHost.StopOrleansSilo();

                Console.WriteLine(string.Format("Orleans silo '{0}' shutdown.", this.siloHost.Name));
            }
            catch (Exception exc)
            {
                this.siloHost.ReportStartupError(exc);
                var msg = string.Format("{0}:\n{1}\n{2}", exc.GetType().FullName, exc.Message, exc.StackTrace);
                Console.WriteLine(msg);
            }

            return ok;
        }

        private void Init() => this.siloHost.LoadOrleansConfig();

        private bool ParseArguments(string[] args)
        {
            string deploymentId = null;

            var siloName = Dns.GetHostName(); // Default to machine name

            var argPos = 1;
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a.StartsWith("-") || a.StartsWith("/"))
                {
                    switch (a.ToLowerInvariant())
                    {
                        case "/?":
                        case "/help":
                        case "-?":
                        case "-help":
                            // Query usage help
                            return false;
                        default:
                            Console.WriteLine("Bad command line arguments supplied: " + a);
                            return false;
                    }
                }
                else if (a.Contains("="))
                {
                    var split = a.Split('=');
                    if (string.IsNullOrEmpty(split[1]))
                    {
                        Console.WriteLine("Bad command line arguments supplied: " + a);
                        return false;
                    }
                    switch (split[0].ToLowerInvariant())
                    {
                        case "deploymentid":
                            deploymentId = split[1];
                            break;
                        default:
                            Console.WriteLine("Bad command line arguments supplied: " + a);
                            return false;
                    }
                }
                // unqualified arguments below
                else if (argPos == 1)
                {
                    siloName = a;
                    argPos++;
                }
                else
                {
                    // Too many command line arguments
                    Console.WriteLine("Too many command line arguments supplied: " + a);
                    return false;
                }
            }

            var config = ClusterConfiguration.LocalhostPrimarySilo();
            config.AddMemoryStorageProvider();
            this.siloHost = new Runtime.Host.SiloHost(siloName, config);

            if (deploymentId != null)
                this.siloHost.DeploymentId = deploymentId;

            return true;
        }

        public void PrintUsage() => Console.WriteLine(
@"USAGE: 
    orleans host [<siloName> [<configFile>]] [DeploymentId=<idString>] [/debug]
Where:
    <siloName>      - Name of this silo in the Config file list (optional)
    DeploymentId=<idString> 
                    - Which deployment group this host instance should run in (optional)");

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool dispose)
        {
            this.siloHost.Dispose();
            this.siloHost = null;
        }
    }
}
