using System;
using System.IO;
using CommandLineUtils;

namespace HP9825Utils
{
    internal class CartridgeInputFileOptions
    {
        [Argument("in", "File", HelpText = "The input filename. Defaults to the .tape extension.", Positional = 1, Required = true)]
        public string Filename { get; set; } = string.Empty;

        internal void Validate(ReturnCodeGroup<CartridgeJobCodes> results)
        {
            var ext = System.IO.Path.GetExtension(Filename);
            if(string.IsNullOrWhiteSpace(ext) || ext == ".")
                Filename = System.IO.Path.ChangeExtension(Filename, ".tape");
            if (!System.IO.File.Exists(Filename))
                throw results.Happened(CartridgeJobCodes.InputNotFound, Filename);
        }
    }
}