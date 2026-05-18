using System;
using System.Threading.Tasks;
using CommandLineUtils;

namespace HP9825Utils
{
    [Process("mrk", "MarkCartridge", HelpMessage = "Marks the cartridge for use, i.e. creates empty files on the cartridge.")]
    public class MarkCartridgeCommand
        : ProcessBase
    {
        private CartridgeInputFileOptions? InputFrom;
        private ReturnCodeGroup<CartridgeJobCodes>? Results;

        protected override void BuildArguments(ParameterHandler builder)
        {
            InputFrom = builder.AddOptions<CartridgeInputFileOptions>();
        }

        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            Results = reg.Register<CartridgeJobCodes>();
            return base.BuildReturnCodes(reg);
        }

        protected async override Task RunNow()
        {
            if (InputFrom == null || Results == null)
                throw new InvalidOperationException();
            InputFrom.Validate(Results);
        }
    }
}