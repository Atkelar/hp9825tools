using CommandLineUtils;

namespace makefont
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {

            using(var host = new SingleCommandHost<MakeFontCommand>("makefont", true))
            {
                host.SetupBanner<Program>("9825 Make Font", "Atkelar", 2026);
                return await host.Run(args);
            }
        }
    }
}