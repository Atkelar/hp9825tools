using CommandLineUtils;

namespace HP9825Utils
{
    internal enum ROMImageErrors
    {
        [ReturnCode("The provided ROM name {0} was unknown.", HelpMessage = "Happens when the provided ROM name is not a valid well known ROM.")]
        InvalidROMName = 30,
        [ReturnCode("The ROM size of {0} words was not the expected {1}!", HelpMessage = "Happens when the input file doesn't have the expected number of words for the expected ROM image.")]
        SizeMismatch = 31,
    }
}