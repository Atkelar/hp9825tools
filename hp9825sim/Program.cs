using System.Threading.Tasks;
using CommandLineUtils;

namespace HP9825Simulator
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            using (var host = new SingleCommandHost<CpuSimulatorProcess>("hp9825sim", true))
            {
                return await host.Run(args);
            }
        }
    }
}