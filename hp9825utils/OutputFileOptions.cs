using CommandLineUtils;
using HP9825CPU;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HP9825Utils
{
    public class OutputFileOptions
    {
        public OutputFileOptions()
        {
        }

        [Argument("f", "FIle", HelpText = "The output file name (or base filename) to use. Defaults to .out.bin extension, uses the same name as the input when not specified.", Positional = 2)]
        public string Filename { get; set; }

        [Argument("o", "Offset", HelpText = "The offset in the working memory to start writing; Uses the same as the input offset if not provided.")]
        public int Offset { get; set; } = -1;

        [Argument("s", "Size", HelpText = "The number of words to write. If not specified, uses the same number of words as the input.")]
        public int Size { get; set; } = -1;

        [Argument("hl", "UseHighLow", HelpText = "When specified, the output file is interpreted as a base name and 'high.' and 'low.' is added for the double-byte version.")]
        public bool UseHighLowFiles { get; set; }

        [Argument("le", "LittleEndian", HelpText = "When specified, the file will be written as little endian. Normally big endian is used for 16-bit binaries.")]
        public bool UseLittleEndian { get; set; }

        [Argument("ovr", "Overwrite", HelpText = "If the output file(s) already exists, it will be overwritten. If not specified, an existing output file will terminate the program.")]
        public bool Overwrite { get; set; }


        internal void RegisterErrors(ReturnCodeHandler reg)
        {
            Errors = reg.Register<OuptutRelatedErrors>(10);
        }

        private ReturnCodeGroup<OuptutRelatedErrors>? Errors;

        enum OuptutRelatedErrors
        {
            [ReturnCode("Output offset/size mismatch: requested {0} words at {1}, but limits are 0..{2}", HelpMessage = "Happens when the provided output options for offset and/or length don't add up for a valid block.")]
            OffsetOrSizeProblem=1,
            [ReturnCode("File {0} already exists. Use Overwrite option to overwrite!", HelpMessage = "Happens when the output file exists, but overwrite was not requested.")]
            ClobberOutput = 2
        }

        public async Task WriteNow(Memory mem, string originalFilename, int defaultOfs, int defaultSize)
        {
            int outOfs = Offset;
            int outSize = Size;
            string outFile = Filename;
            if (string.IsNullOrWhiteSpace(outFile))
                outFile = Path.ChangeExtension(originalFilename, ".out.bin");

            if (outOfs < 0)
                outOfs = defaultOfs;
            if (outSize < 0)
                outSize = defaultSize;
            if (outOfs + outSize > mem.Length || outOfs < 0 || outSize <= 0)
            {
                throw Errors!.Happened(OuptutRelatedErrors.OffsetOrSizeProblem, outSize, outOfs, mem.Length);
            }

            if (UseHighLowFiles)
            {
                string org = Path.GetExtension(outFile);
                string hFile = System.IO.Path.ChangeExtension(outFile, ".out.high" + org);
                string lFile = System.IO.Path.ChangeExtension(outFile, ".out.low" + org);
                if (File.Exists(hFile) && !Overwrite)
                    throw Errors!.Happened(OuptutRelatedErrors.ClobberOutput, hFile);
                if (File.Exists(lFile) && !Overwrite)
                    throw Errors!.Happened(OuptutRelatedErrors.ClobberOutput, lFile);
                Console.WriteLine("Creating {0} and {1}...", hFile, lFile);
                using (var bwh = new BinaryWriter(File.Create(hFile)))
                {
                    using (var bwl = new BinaryWriter(File.Create(lFile)))
                    {
                        mem.DumpDual8Bit(bwl, bwh, outOfs, outSize);
                    }
                }
            }
            else
            {
                if (File.Exists(outFile) && !Overwrite)
                    throw Errors!.Happened(OuptutRelatedErrors.ClobberOutput, outFile);
                Console.WriteLine("Creating {0}...", outFile);
                using (var bw = new BinaryWriter(File.Create(outFile)))
                {
                    mem.Dump16Bit(bw, outOfs, outSize, !UseLittleEndian);
                }
            }
        }
    }
}