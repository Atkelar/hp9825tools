using CommandLineUtils;

namespace HP9825Disassembler
{
    public class DisassemblerContextParameters
    {
        /// <summary>
        /// Don't create running backups of the mapping file. The file is overwritten in place!
        /// </summary>
        [Argument("nh", "NoHistory", HelpText = "When specified, will overwrite the mapping file instead of creating running backups.")]
        public bool NoHistory { get; set; }

        /// <summary>
        /// Update the mapping file.
        /// </summary>
        [Argument("um", "UpdateMap", HelpText ="Update the mapping file. Unless no-history option is used, it will create a new file after renaming the old one.")]
        public bool UpdateMap { get; set; }

        [Argument("mf", "MappingFile", HelpText = "The name of the mapping file to use. If it doesn't exist, it will be created. If none is specified, the entire input range is treated as code for a first iteration.", Positional = 1)]
        public string? MappingFile { get; set; }
        [Argument("new", "CreateNewMapping", HelpText = "When provided, will create a new mapping file instead of loading an existing one.")]
        public bool CreateNewMapping { get; set; }

        [Argument("out", "Output", HelpText = "The output file to use; defaults to the input filename with the .listing.txt extension.", Positional = 2)]
        public string? OutputFile { get; set; }
    }
}