using HP9825CPU;
using CommandLineUtils;
using System.Threading.Tasks;
using System;
using System.IO;

namespace HP9825Utils
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

        [Argument("ofs", "Offset", HelpText = "The input file is loaded into a 'working memory'. Normally, it is loaded to address 0 in that, but it can be moved to any (word!) offset here.", DefaultValue = "0")]
        public int LoadToOffset { get; set; } = 0;

        [Argument("sz", "Size", HelpText = "The number of bytes to read; when not specified, reads until either the file or the working memory is exhausted. When specified, will be range checked.")]
        public int LoadSize { get; set; } = -1;

        public Memory MakeBuffer()
        {
            return Memory.MakeMemory(Use16Bit);
        }

        public async Task<(int ReturnCode, int Offset, int WordCount, string ActualFilename)> ReadBuffer(Memory mem)
        {
            string inputfile = Filename;
            if (Path.GetExtension(inputfile).Length == 0)
                inputfile = Path.ChangeExtension(inputfile, ".bin");

            int ofs = LoadToOffset;
            if (ofs < 0 || ofs >= mem.Length)
            {
                Console.WriteLine("Load offset out of range: {0}, 0..{1}", ofs, mem.Length);
                return (2, 0, 0, string.Empty);
            }

            int size = mem.Length - ofs;    // init with maximum for memory.
            if (UseHighLowFiles)
            {
                string org = Path.GetExtension(inputfile);
                string hFile = System.IO.Path.ChangeExtension(inputfile, ".high" + org);
                string lFile = System.IO.Path.ChangeExtension(inputfile, ".low" + org);
                var fil = new FileInfo(lFile);
                var fih = new FileInfo(hFile);
                if (!fil.Exists || !fih.Exists)
                {
                    Console.WriteLine("Input file {0} or {1} not found!", hFile, lFile);
                    return (1, 0, 0, string.Empty);
                }
                if (fil.Length != fih.Length)
                {
                    Console.WriteLine("Input files {0} or {1} not the same size!", hFile, lFile);
                    return (1, 0, 0, string.Empty);
                }
                // number of words...
                if (LoadSize < 0)
                {
                    size = Math.Min(size, (int)fil.Length);
                }
                else
                {
                    if (LoadSize == 0 || LoadSize + ofs > mem.Length)
                    {
                        Console.WriteLine("Size out of range! {0} starting at {1} was requested, memory is from 0..{2}", LoadSize, ofs, mem.Length);
                        return (2, 0, 0, string.Empty);
                    }
                    if (LoadSize > fil.Length)
                    {
                        Console.WriteLine("File too small for requested size! {0} was requested, file has {1}...", LoadSize, fil.Length);
                        return (2, 0, 0, string.Empty);
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
                {
                    Console.WriteLine("Input file {0} not found!", inputfile);
                    return (1, 0, 0, string.Empty);
                }
                if (LoadSize < 0)
                {
                    if ((fi.Length % 2) != 0)
                    {
                        Console.WriteLine("Input file wasn't even length; words required!");
                        return (2, 0, 0, string.Empty);
                    }
                    size = Math.Min(size, (int)fi.Length / 2);
                }
                else
                {
                    if (LoadSize == 0 || LoadSize + ofs > mem.Length)
                    {
                        Console.WriteLine("Size out of range! {0} starting at {1} was requested, memory is from 0..{2}", LoadSize, ofs, mem.Length);
                        return (2, 0, 0, string.Empty);
                    }
                    if (LoadSize * 2 > fi.Length)
                    {
                        Console.WriteLine("File too small for requested size! {0} was requested, file has {1} byets ({2} words)...", LoadSize, fi.Length, fi.Length / 2);
                        return (2, 0, 0, string.Empty);
                    }
                    size = LoadSize;
                }
                using (var br = new BinaryReader(File.OpenRead(inputfile)))
                {
                    mem.Load16Bit(br, ofs, size, !UseLittleEndian);
                }
            }
            return (0, ofs, size, inputfile);
        }
    }
}