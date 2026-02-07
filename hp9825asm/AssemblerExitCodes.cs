using CommandLineUtils;

namespace HP9825Assembler
{
    public enum AssemblerExitCodes
    {
        [ReturnCode("Assembly error: {0}", HelpMessage = "Happens whenever there is an error in the input file; The error message will contain the details.")]
        AssmeblyError = 1,
        [ReturnCode("Handling error: {0}", HelpMessage = "Happens when the provided input and settings didn't add up. Not strictly a fault in the source code, but might be.")]
        GenericError = 2,
    }
}