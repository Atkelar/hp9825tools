using CommandLineUtils;

namespace makefont
{
    public enum FontError
    {
        [ReturnCode("File {0} not found!", HelpMessage = "The specified input file was not found.")]
        FileNotFound = 1,

        [ReturnCode("Directive {2} is unkown in file {0}:{1}", HelpMessage = "The directive (.xy) was not recognized.")]
        InvalidDirective = 2,
        [ReturnCode("FORMAT directive is invalid: {2} in file {0}:{1}", HelpMessage = "The FORMAT directive had an error. Must be #x# format.")]
        InvalidFormat = 3,

        [ReturnCode("Glyph data invalid: {2} in file {0}:{1}", HelpMessage = "The line in the glyph file is invlaid, no associated glyph found.")]
        UnexpectedGlyphData = 4,

        [ReturnCode("Glyph data invalid, size mismatch: {2} in file {0}:{1}", HelpMessage = "The line in the glyph file is past the number of lines or columns in the matrix.")]
        GlyphSizeMismatch = 5,
        [ReturnCode("DEF is invlaid: {2} for {3} in file {0}:{1}", HelpMessage = "The line in the glyph file is past the number of lines or columns in the matrix.")]
        InvalidDef = 6,
        [ReturnCode("File {0} already exists! Use overwrite or another filename.", HelpMessage = "The specified output file was found and override was not specified.")]
        FileExists = 7,

        [ReturnCode("The input file didn't define any glyphs.", HelpMessage = "No glyphs found: Some more work needed...")]
        NoGlyphs = 8,
        [ReturnCode("Invert characters not possible. Range occupied.", HelpMessage = "WHen the option to invert the characters is selecte, but there are defined characters in that rannge.")]
        InvertNotPossible = 9,
    }
}