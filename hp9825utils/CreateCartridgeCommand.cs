using System;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CommandLineUtils;
using HP9825CPU;

namespace HP9825Utils
{
    [Process("cc", "CreateCartridge", HelpMessage = "Creates a new - empty - tape cartridge image file.")]
    public class CreateCartridgeCommand
        : ProcessBase
    {
        CartridgeMetadataParameters? Metadata;
        CartridgeOutputFileOptions? OutputTo;
        private ReturnCodeGroup<CartridgeJobCodes> Results;

        protected override void BuildArguments(ParameterHandler builder)
        {
            Metadata = builder.AddOptions<CartridgeMetadataParameters>();
            OutputTo = builder.AddOptions<CartridgeOutputFileOptions>();
        }

        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            Results = reg.Register<CartridgeJobCodes>();
            return base.BuildReturnCodes(reg);
        }

        protected override async Task RunNow()
        {
            if(OutputTo == null || Results == null || Metadata == null)
                throw new InvalidOperationException();
            if (!OutputTo.Validate())
            {
                throw Results.Happened(CartridgeJobCodes.OutputOverwrite, OutputTo.Filename);
            }
            Metadata.Validate(Results);
            var tc = TapeCartridge.Create(Metadata.Label, Metadata.Length);
            tc.Comment = Metadata.Comment;

            Write("Creating tape image {0} in {1}...", tc.Label, OutputTo.Filename);
            await tc.Save(OutputTo.Filename, !OutputTo.UseMultiFile);
            WriteLine(" done!");
        }
    }
}