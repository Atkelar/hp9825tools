using CommandLineUtils;
using System;

namespace HP9825Utils
{

    class Program
    {
        static async System.Threading.Tasks.Task<int> Main(string[] args)
        {
            var host = new MultiCommandHost("hp9825utils");
            host.SetupBanner<Program>("9825 CPU Tools", "Atkelar", 2026);

            host.Register<InvertBitsCommand>();

            return await host.Run(args);
        }
    }

}