using CommandLineUtils;

namespace HP9825Disassembler
{
    internal enum DisassemblerExitCodes
    {
        [ReturnCode("Error in parsing/reading the mappnig file: {0} ({2}:{1})", HelpMessage = "Occurs when the mapping file could not be parsed.")]
        MappingFileError = 10,
    }
}