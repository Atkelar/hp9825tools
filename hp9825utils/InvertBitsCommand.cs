using CommandLineUtils;
using HP9825CPU;
using System.Threading.Tasks;

namespace HP9825Utils
{
    [Process("id", "InvertData", HelpMessage = "Invert data bits in a binary image, to suit EPROM programming.")]
    public class InvertBitsCommand
        : ProcessBase
    {
        public InvertBitsCommand()
        {}

        protected override async Task RunNow()
        {
            Memory mem = Input.MakeBuffer();

            var inputdata = await Input.ReadBuffer(mem);

            // we loaded "size" words to "ofs"; start inverting!
            for (int i = 0; i < inputdata.WordCount; i++)
            {
                mem[i + inputdata.Offset] = (~mem[i + inputdata.Offset]) & 0xFFFF;
            }

            await Output.WriteNow(mem, inputdata.ActualFilename, inputdata.Offset, inputdata.WordCount);
        }

        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            
            Input.RegisterErrors(reg);
            Output.RegisterErrors(reg);
            return base.BuildReturnCodes(reg);
        }

        protected override void BuildArguments(ParameterHandler builder)
        {
            Input = builder.AddOptions<InputFileOptions>();
            Output = builder.AddOptions<OutputFileOptions>("o");
        }

        public InputFileOptions Input { get; set; }
        public OutputFileOptions Output { get; set; }

        public override string? GetExtendedHelp(string? page)
        {
            if (page == null)
                return @"The normal assembler output of the hp9825 tools is 'normal' - i.e. bit = 0 is stored as 0.
The processor and system is 'negative logic' however, and so a bit = 0 needs to be stored
in the EPROMS as a 1 and vice versa. This command will invert a BIN file to fit this
requirement.
";
            else
                return $"Unknown detal {page}!";
        }
    }
}