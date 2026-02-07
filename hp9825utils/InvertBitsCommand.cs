using CommandLineUtils;
using HP9825CPU;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
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
            Memory mem = InOptions.MakeBuffer();

            var inputdata = await InOptions.ReadBuffer(mem);

            // we loaded "size" words to "ofs"; start inverting!
            for (int i = 0; i < inputdata.WordCount; i++)
            {
                mem[i + inputdata.Offset] = (~mem[i + inputdata.Offset]) & 0xFFFF;
            }

            using(var t = Out.Table(VerbosityLevel.Normal, 
                x=> x.RowCountTemplate("{0} rows").Separators(true,true, true)
                    .Column(5, c=>c.Head("Test").Format("000").Align(HorizontalAlignment.Right, TextTrimming.End))
                    .Column(3, 10, c=>c.Align(HorizontalAlignment.Center))))
            {
                t.Line(12,3,4,5);
                t.Line(12);
            }

            await OutOptions.WriteNow(mem, inputdata.ActualFilename, inputdata.Offset, inputdata.WordCount);
        }

        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {

            InOptions.RegisterErrors(reg);
            OutOptions.RegisterErrors(reg);
            return base.BuildReturnCodes(reg);
        }

        protected override void BuildArguments(ParameterHandler builder)
        {
            InOptions = builder.AddOptions<InputFileOptions>();
            OutOptions = builder.AddOptions<OutputFileOptions>("o");
        }

        public InputFileOptions InOptions { get; set; }
        public OutputFileOptions OutOptions { get; set; }

        public override void WriteExtendedHelp(OutputHandlerBase output, string? page)
        {
            if (string.IsNullOrEmpty(page))
                output.WriteLine(VerbosityLevel.Normal, SplitMode.Word, @"The normal assembler output of the hp9825 tools is 'normal' - i.e. bit = 0 is stored as 0. The processor and system is 'negative logic' however, and so a bit = 0 needs to be stored in the EPROMS as a 1 and vice versa. 
This command will invert a binary file to fit this requirement.");
            else
                output.WriteLine(VerbosityLevel.Normal, SplitMode.Word, $"Unknown detal {page}!");
        }
    }
}