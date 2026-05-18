using System.IO;
using CommandLineUtils;

namespace HP9825Utils
{

    public class ExportFileOptions
    {
        [Argument("o", "output", HelpText = "The base name of the output file. If more than one file is requested in the export, the file is suffixed with the file index number from the tape. Defaults to the input tape file name with a .txt extension.", Syntax = "output.txt")]
        public string? BaseFilename { get; set; }

        [Argument("f", "force", HelpText = "Force overwrite. If the output file already exists, the job will fail. Use this to overwrite an existing file!")]
        public bool ForceOverwrite {get;set;}

        public string MakeFilename(string inputFilename, int? fileNumber)
        {
            inputFilename = BaseFilename ?? Path.ChangeExtension(inputFilename, ".txt");
            if (fileNumber.HasValue)
            {
                return Path.GetFileNameWithoutExtension(inputFilename) + "-" + fileNumber.Value.ToString() + Path.GetExtension(inputFilename);
            }
            return inputFilename;
        }
    }
}