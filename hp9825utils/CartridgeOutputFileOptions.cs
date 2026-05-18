using System;
using System.IO;
using CommandLineUtils;
using HP9825CPU;

namespace HP9825Utils
{
    internal class CartridgeOutputFileOptions
    {
        internal bool Validate()
        {
            var ext = System.IO.Path.GetExtension(Filename);
            if(string.IsNullOrWhiteSpace(ext) || ext == ".")
                Filename = System.IO.Path.ChangeExtension(Filename, ".tape");
            if (System.IO.File.Exists(Filename) && !Overwrite)
                return false;
            return true;
        }

        [Argument("out", "OutputFile", HelpText = "The output filename. Defaults to the .tape extension.", Positional = 2, Required = true)]
        public string Filename { get; set; } = string.Empty;

        [Argument("ow", "Overwite", HelpText = "Overwrite an existing file. Only the main file for the tape image ist checked!")]
        public bool Overwrite {get;set;}

        [Argument("mf", "MultiFile", HelpText = "Use multifile format - normally, a tape image is a packaged .tape single file. If this option is specified, it will be saved as a collection of related files with the .tape being the metadata file. For diagnostic purposes mostly.")]
        public bool UseMultiFile {get;set;}
    }
}