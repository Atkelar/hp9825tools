using CommandLineUtils;

namespace makefont
{
    public class FontMakerParameters
    {
        [Argument("ds", "dotspacing", HelpText = "The number of units between the 'pixels'. Zero to connect them, more than zero to include a gap for realism.", DefaultValue = "1")]
        public double DotSpacing { get; set; } = 1;

        [Argument("in", "input", Positional = 1, Required = false, DefaultValue = "font-def.txt")]
        public string InputFile { get; set; } = "font-def.txt";

        [Argument("ps", "pixelsize", HelpText = "The number of units a single pixel is wide and high.", DefaultValue = "128" )]
        public double PixelSize { get; set; } = 128;

        [Argument("con", "conform", HelpText = "The number of units for a character width.", DefaultValue = "1024" )]
        public double ConformSize { get; set; } = 1024;

        [Argument("out", "output", Positional = 2, HelpText = "Output file name. Extensions defaults to '.svg'.")]
        public string OuptutFile { get; set; } = "font.svg";

        [Argument("ov", "overwrite", HelpText = "Overwrite output file. If not specified, an existing output file will cuase an abort.")]
        public bool Overwrite { get; set; }
        [Argument("fid", "fontid", HelpText = "The CSV font ID property to set.")]
        public string FontId { get; set; } = "hp9825a-font";

        [Argument("inv", "invert", HelpText = "If specified, the characters will be inverted.")]
        //[Argument("inv", "invert", HelpText = "If specified, the upper range (128-255) characters will be generated to mirror the lower ones.")]
        public bool InvertChars { get; set; }
    }
}