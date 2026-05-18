using CommandLineUtils;
using System;

namespace HP9825Utils
{

    class Program
    {
        static async System.Threading.Tasks.Task<int> Main(string[] args)
        {
            using(var host = new MultiCommandHost("hp9825utils"))
            {
                host.SetupBanner<Program>("9825 CPU Tools", "Atkelar", 2026);
                host.AddOptionalDefaults("~/.hp9825/hp9825.defaults");
                host.AddOptionalDefaults(".hp9825.defaults");
                host.AddOptionalDefaults("~/.hp9825/hp9825util.defaults");
                host.AddOptionalDefaults(".hp9825util.defaults");

                host.Register<InvertBitsCommand>();
                host.Register<CreateImageCommand>();
                host.Register<MergeIntoCommand>();
                host.Register<TranslateAddressCommand>();
                host.Register<CreateCartridgeCommand>();
                host.Register<GetCartridgeListingCommand>();
                host.Register<CartridgeExportFileCommand>();
                host.Register<ExtractHPLTables>();

                return await host.Run(args);
            }
        }
    }

}