using System;
using System.Threading.Tasks;
using CommandLineUtils;

namespace HP9825Disassembler
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("9825 CPU Disassembler, {0} (c) 2026 by Atkelar", typeof(Program).Assembly.GetName().Version);
            using(var host = new SingleCommandHost<DisassembleCommand>("hp9825dasm", true))
            {
                host.SetupBanner<Program>("9825 CPU Disassembler", "Atkelar", 2026);
                return await host.Run(args);
            }
        }
    }
}