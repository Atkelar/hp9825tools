using CommandLineUtils;

namespace HP9825Utils
{
    public class ROMHPLOptions
    {
        [Argument("rom", "RomName", DefaultValue = "system", HelpText = "The name of the ROM export to create; Will be used as a default filename as well as defaults for the addresses.")]
        public string RomName { get; set; } = "system";
    }
}