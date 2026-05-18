using CommandLineUtils;

namespace HP9825Utils
{
    public enum CartridgeJobCodes
    {
        [ReturnCode("The output file {0} already exists! Use the overwrite parameter to force overwriting!", HelpMessage = "Occurs when the output file already exists and the overwrite option has not been provided.")]
        OutputOverwrite = 20,
        [ReturnCode("The metadata for the tape is invalid! {0} with value of {1} faild validation: {2}", HelpMessage = "Occurs when the selected metadata for the tape doesn't meet requirements.")]
        InvalidMetadata = 21,
        [ReturnCode("The input file {0} was not found!", HelpMessage = "Occurs when the input file couldn't be found.")]
        InputNotFound = 22,
        [ReturnCode("The cartridge address was invalid: {0} was specified: {1}", HelpMessage = "Occurs when the provided tape target or source addresses fail validation!")]
        InvalidAddressing = 23,
    }
}