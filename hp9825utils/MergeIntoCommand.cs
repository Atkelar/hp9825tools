using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.XPath;
using CommandLineUtils;

namespace HP9825Utils
{
    [Process("mi", "MergeInto", HelpMessage = "Merges a binary from one source into a (larger) target image, optionally reversing the byte order for addressing inversion.")]
    public class MergeIntoCommand
        : ProcessBase
    {
        public ModificationFileOptions? Modify { get; private set; }
        public InputFileOptions? Input { get; private set; }
        public LocalOptions? Local { get; private set; }
        public ReturnCodeGroup<MergeErrors>? Errors { get; private set; }

        protected override void BuildArguments(ParameterHandler builder)
        {
            Local =  builder.AddOptions<LocalOptions>();
            Modify = builder.AddOptions<ModificationFileOptions>();
            Input = builder.AddOptions<InputFileOptions>("in");
        }

        public enum MergeErrors
        {
            [ReturnCode("Merge offset/size mismatch: requested {0} words at {1}, but limits are 0..{2}", HelpMessage = "Happens when the provided output options for offset and/or length don't add up for a valid block.")]
            TargetRangeInvalid=1,
        }

        protected override async Task RunNow()
        {
            var mem = Modify.MakeBuffer();

            var source = Input.MakeBuffer();
            var x = await Input.ReadBuffer(source);

            await Modify.ReadBuffer(mem);

            int length = 0;
            if (Local.TargetSize > 0 )
                length = Local.TargetSize;
            else
                length = x.WordCount;
            
            int offs = Local.TargetOffset;
            if (offs < 0 || offs + length > source.Length)
                throw Errors!.Happened(MergeErrors.TargetRangeInvalid, length, offs, source.Length);

            Out?.WriteLine(VerbosityLevel.Normal, SplitMode.Word, "Merging {1:x4} words to {0:x4}.", offs, length);

            int ti, td;
            if (Local.Reverse)
            {
                td = -1;
                ti = offs + length - 1;
            }
            else
            {
                ti = offs;
                td = 1;
            }

            for(int i = 0; i < length; i++)
            {
                int word = source[x.Offset + i];
                if (Local.Negate)
                    word = (~word) & 0xFFFF;
                mem[ti] = word;
                ti += td;
            }

            await Modify.WriteNow(mem);
        }

        public class LocalOptions
        {
            [Argument("n", "Negate", HelpText = "Invert the bytes of the input file, to line up with the negative logic.")]
            public bool Negate { get; set; }
            [Argument("r", "Reverse", HelpText = "Reverse the addresses in the output; This will also 'top' align the written output.")]
            public bool Reverse { get; set; }

            [Argument("to", "TargetOffset", HelpText = "The lower address for the read buffer in the output. Defautls to 0.")]
            public int TargetOffset {get;set;} = 0;
            [Argument("ts", "TargetSize", HelpText = "The number of words to write to the output. Defaults to the number of words read from the input. If there were less bytes read than this, the remainder is NOT modified!")]
            public int TargetSize {get;set;}
        }
        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            Modify!.RegisterErrors(reg);
            Errors = reg.Register<MergeErrors>();
            return base.BuildReturnCodes(reg);
        }

    }


}