using System.Threading.Tasks;
using CommandLineUtils;

namespace HP9825CPUSimulator
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            using (var host = new SingleCommandHost<CpuSimulatorProcess>("hp9825cpusim", true))
            {
                return await host.Run(args);
            }
        }
    }
}