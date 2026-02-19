using HP9825CPU;
using CommandLineUtils;
using System.Threading.Tasks;
using System;
using System.IO;

namespace HP9825Disassembler
{
    public class InputFileOptions
    {
        public InputFileOptions()
        {
        }

        [Argument("in", "InputFile", HelpText = "The input file name (or base filename) to use. Defaults to .bin extension.", Positional = 1, Required = true)]
        public string Filename { get; set; }

        [Argument("hl", "UseHighLow", HelpText = "When specified, the input filename is interpreted as a base name and 'high.' and 'low.' is added for the double-byte version.")]
        public bool UseHighLowFiles { get; set; }

        [Argument("u16", "Use16Bit", HelpText = "When specified, assume 16-bit addressing mode. Extends the valid ragnes of offsets.")]
        public bool Use16Bit { get; set; }

        [Argument("le", "LittleEndian", HelpText = "When specified, the input file will be read as little endian. Normally big endian is used for 16-bit binaries. No effect when UseHighLow is also used.")]
        public bool UseLittleEndian { get; set; }

        [Argument("o", "Offset", HelpText = "The input file is loaded into a 'working memory'. Normally, it is loaded to address 0 in that, but it can be moved to any (word!) offset here.", DefaultValue = "0")]
        public int LoadToOffset { get; set; } = 0;

        [Argument("s", "Size", HelpText = "The number of bytes to read; when not specified, reads until either the file or the working memory is exhausted. When specified, will be range checked.")]
        public int LoadSize { get; set; } = -1;

        public Memory MakeBuffer()
        {
            return Memory.MakeMemory(Use16Bit);
        }

        internal void RegisterErrors(ReturnCodeHandler reg)
        {
            Errors = reg.Register<InputRelatedErrors>(20);
        }

        private ReturnCodeGroup<InputRelatedErrors>? Errors;

        enum InputRelatedErrors
        {
            [ReturnCode("Input file not found: {0}", HelpMessage = "Happens when the provided input file isn't found in the filesystem.")]
            FileNotFound=1,
            [ReturnCode("Input file size invalid: {0}, {1} ({2})", HelpMessage = "Happens when the input file size doesn't add up to expectations.")]
            SizeProblem=2,
            [ReturnCode("Range specification invalid. Requested {0}-{1}, valid {2}-{3} ({4})", HelpMessage = "Happens when the input range (offset/length) results in an invalid range.")]
            RangeProblem=3,
         
           
        }

        public async Task<(int Offset, int WordCount, string ActualFilename)> ReadBuffer(Memory mem)
        {
            string inputfile = Filename;
            if (Path.GetExtension(inputfile).Length == 0)
                inputfile = Path.ChangeExtension(inputfile, ".bin");

            int ofs = LoadToOffset;
            if (ofs < 0 || ofs >= mem.Length)
                throw Errors!.Happened(InputRelatedErrors.RangeProblem, ofs, 1, 0, mem.Length, "The requested start offset was invalid!");

            int size = mem.Length - ofs;    // init with maximum for memory.
            if (UseHighLowFiles)
            {
                string org = Path.GetExtension(inputfile);
                string hFile = System.IO.Path.ChangeExtension(inputfile, ".high" + org);
                string lFile = System.IO.Path.ChangeExtension(inputfile, ".low" + org);
                var fil = new FileInfo(lFile);
                var fih = new FileInfo(hFile);
                if (!fil.Exists)
                    throw Errors!.Happened(InputRelatedErrors.FileNotFound, hFile);
                if (!fih.Exists)
                    throw Errors!.Happened(InputRelatedErrors.FileNotFound, lFile);
                if (fil.Length != fih.Length)
                    throw Errors!.Happened(InputRelatedErrors.SizeProblem, hFile, fih.Length, "low/high file sizes don't match!");
                // number of words...
                if (LoadSize < 0)
                {
                    size = Math.Min(size, (int)fil.Length);
                }
                else
                {
                    if (LoadSize == 0 || LoadSize + ofs > mem.Length)
                    {
                        throw Errors!.Happened(InputRelatedErrors.RangeProblem, ofs, ofs+LoadSize, 0, mem.Length, "Size parameter invalid");
                    }
                    if (LoadSize > fil.Length)
                    {
                        throw Errors!.Happened(InputRelatedErrors.RangeProblem, 0, LoadSize, 0, fil.Length, "The file size was too small for the requesed length!");
                    }
                    size = LoadSize;
                }

                using (var brl = new BinaryReader(File.OpenRead(lFile)))
                {
                    using (var brh = new BinaryReader(File.OpenRead(hFile)))
                    {
                        mem.LoadDual8Bit(brl, brh, ofs, size);
                    }
                }
            }
            else
            {
                var fi = new FileInfo(inputfile);
                if (!fi.Exists)
                    throw Errors!.Happened(InputRelatedErrors.FileNotFound, inputfile);
                if (LoadSize < 0)
                {
                    if ((fi.Length % 2) != 0)
                        throw Errors!.Happened(InputRelatedErrors.SizeProblem, inputfile, fi.Length, "file not word-aligned!");
                    size = Math.Min(size, (int)fi.Length / 2);
                }
                else
                {
                    if (LoadSize == 0 || LoadSize + ofs > mem.Length)
                        throw Errors!.Happened(InputRelatedErrors.RangeProblem, ofs, ofs+LoadSize, 0, mem.Length, "The requested offset/length doesn't add up.");
                    if (LoadSize * 2 > fi.Length)
                        throw Errors!.Happened(InputRelatedErrors.RangeProblem, 0, LoadSize, 0, fi.Length / 2, "The file size was too small for the requesed length!");
                    size = LoadSize;
                }
                using (var br = new BinaryReader(File.OpenRead(inputfile)))
                {
                    mem.Load16Bit(br, ofs, size, !UseLittleEndian);
                }
            }
            return (ofs, size, inputfile);
        }
    }
}