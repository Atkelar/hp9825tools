using System;
using System.IO;
using System.Threading.Tasks;
using CommandLineUtils;
using HP9825CPU;

namespace HP9825Utils
{
    [Process("ci", "CreateImage", HelpMessage = "Creates a new binary image file.")]
    public class CreateImageCommand
        : ProcessBase
    {
        public OutputFileOptions? Output { get; private set; }
        public LocalOptions? Local { get; private set; }

        public class LocalOptions
        {
            [Argument("n", "Negate", HelpText = "Create the empty file with all ones instead of all zero. Use as a baseline for EPROM programming images to keep 'empty' sections at 'zero' in the negative logic..")]
            public bool Negate { get; set; }
            [Argument("u16", "Use16Bit", HelpText = "When specified, assume 16-bit addressing mode. Extends the valid ragnes of offsets.")]
            public bool Use16Bit { get; set; }
        }


        protected override void BuildArguments(ParameterHandler builder)
        {
            Output = builder.AddOptions<OutputFileOptions>();
            Local = builder.AddOptions<LocalOptions>();
        }

        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            Output!.RegisterErrors(reg);
            return base.BuildReturnCodes(reg);
        }

        protected override async Task RunNow()
        {
            if (string.IsNullOrWhiteSpace(Output!.Filename)) // TODO: build some overide capability for mandatory/positional settings! Syntax and parsing updates would be nice...
                throw ReturnCode.ParseError.Happened("Filename", "Ouptut file name is missing!");
            Memory mem = Memory.MakeMemory(Local!.Use16Bit);
            if (Local!.Negate)
            {
                for(int i = 0; i < mem.Length; i++)
                    mem[i] = 0xFFFF;
            }

            await Output.WriteNow(mem, null, 0, mem.Length);
        }
    }
}