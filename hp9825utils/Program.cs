using CommandLineUtils;
using System;

namespace HP9825Utils
{

    class Program
    {
        static async System.Threading.Tasks.Task<int> Main(string[] args)
        {
            Console.WriteLine("9825 CPU Tools, {0} (c) 2026 by Atkelar", typeof(Program).Assembly.GetName().Version);

            var host = new MultiCommandHost("hp9825utils");

            host.Register<InvertBitsCommand>();

            return await host.Run(args);
        }
    }

}