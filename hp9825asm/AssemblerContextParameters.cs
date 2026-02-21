using CommandLineUtils;
namespace HP9825Assembler
{
    public class AssemblerContextParameters
    {
        [Argument("u16", "Use16Bit", HelpText = "If specified, the assembler will assume a 16-bit CPU; i.e. the A15 line will be used for addressing, not indirection. This will also enable some IOC commands.")]
        public bool Use16Bit { get; set; } = false;

        [Argument("rel", "UseRelative", HelpText = "If specified, the assmebler will assume relative addressing. The 9825 uses 'absolute' addressing however. This mode is enabled/disabled via a CPU pin, so it wouldn't work on the 9825...")]
        public bool UseRelativeAddressing { get; set; } = false;

        [Argument("ln", "FirstLine", HelpText = "The initial line number for listing production.", DefaultValue = "1")]
        public int InitialLineNumber { get; set; } = 1;

        [Argument("l", "Listing", HelpText = "A file name for the listing. If not specified, stdout will be used. Specify '-' for no listing output.")]
        public string? ListingFile { get; set; } = null;

        [Argument("x", "CrossRef", HelpText = "A file name for the cross reference output. If not specified, stdout will be used. Specify '-' for no cross reference output.")]
        public string? CrossRefFile { get; set; } = null;

        [Argument("xs", "SorfRefs", HelpText = "Specify how to sort the cross references. By address (A = default) or by name (N)")]
        public string CrossRefSort { get; set; } = "A";
        

        [Argument("o", "Output", HelpText = "The output filename. If not provided, the assembler will only create the listing.")]
        public string? OutputFile { get; set; } = null;

        [Argument("fa", "FromAddress", HelpText = "The starting address of the output;", DefaultValue = "0")]
        public int FromAddress { get; set; } = 0;

        [Argument("len", "Length", HelpText = "The number of words to output; Without specifying, all from start to memory size will be produced.", DefaultValue = "all")]
        public int Length { get; set; } = 0;

        [Argument("in", "Input", HelpText = "The input file to assemble. Assumes .asm file extension.", Positional = 1, Required = true)]
        public string? InputFile { get; set; } = null;

        [Argument("hl", "UseHighLow", HelpText = "Use high/low output files for EPROM programming.")]
        public bool UseLowHighFiles { get; set; }

        [Argument("c", "Condition", HelpText = "Sets the condition for assembling to either N or Z (see IFN and IFZ pseudo code!).")]
        public string Conditional { get; set; } = string.Empty;

        [Argument("b", "BeautyFile", HelpText = "Output filename for a beautified source file.")]
        public string BeautyFile { get; set; } = string.Empty;


        // [Argument("inv", "Invert", HelpText = "Invert the output files; both data bits and address bits will be inverted.")]
        // public bool UseLowHighFiles { get; set; }
    }
}