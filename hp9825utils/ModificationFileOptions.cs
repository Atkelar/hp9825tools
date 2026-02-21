using HP9825CPU;
using CommandLineUtils;
using System.Threading.Tasks;
using System;
using System.IO;

namespace HP9825Utils
{
    public class ModificationFileOptions
    {
        public ModificationFileOptions()
        {
        }

        [Argument("f", "File", HelpText = "The file name (or base filename) to modify. Defaults to .bin extension.", Positional = 1, Required = true)]
        public string Filename { get; set; }

        [Argument("hl", "UseHighLow", HelpText = "When specified, the filename is interpreted as a base name and 'high.' and 'low.' is added for the double-byte version.")]
        public bool UseHighLowFiles { get; set; }

        [Argument("le", "LittleEndian", HelpText = "When specified, the input file will be read as little endian. Normally big endian is used for 16-bit binaries. No effect when UseHighLow is also used.")]
        public bool UseLittleEndian { get; set; }

        public Memory MakeBuffer()
        {
            return Memory.MakeMemory(true);
        }

        internal void RegisterErrors(ReturnCodeHandler reg)
        {
            Errors = reg.Register<ModificationRelatedErrors>(30);
        }

        private ReturnCodeGroup<ModificationRelatedErrors>? Errors;

        enum ModificationRelatedErrors
        {
            [ReturnCode("Modification file not found: {0}", HelpMessage = "Happens when the provided file isn't found in the filesystem.")]
            FileNotFound=1,
            [ReturnCode("Input file size invalid: {0}, {1} ({2})", HelpMessage = "Happens when the input file size doesn't add up to expectations.")]
            SizeProblem=2,
        }

        public string ActualFilename { get; set; }
        public int? LoadedSize { get; set; }

        private string? _HiFile, _LoFile;

        public async Task ReadBuffer(Memory mem)
        {
            string inputfile = Filename;
            if (Path.GetExtension(inputfile).Length == 0)
                inputfile = Path.ChangeExtension(inputfile, ".bin");

            if (UseHighLowFiles)
            {
                string org = Path.GetExtension(inputfile);
                string hFile = System.IO.Path.ChangeExtension(inputfile, ".high" + org);
                string lFile = System.IO.Path.ChangeExtension(inputfile, ".low" + org);
                var fil = new FileInfo(lFile);
                var fih = new FileInfo(hFile);
                if (!fil.Exists)
                    throw Errors!.Happened(ModificationRelatedErrors.FileNotFound, hFile);
                if (!fih.Exists)
                    throw Errors!.Happened(ModificationRelatedErrors.FileNotFound, lFile);
                if (fil.Length != fih.Length)
                    throw Errors!.Happened(ModificationRelatedErrors.SizeProblem, hFile, fih.Length, "low/high file sizes don't match!");
                // number of words...
                int size = (int)fil.Length;
                if (size <=0 || size > mem.Length)
                    throw Errors!.Happened(ModificationRelatedErrors.SizeProblem, hFile, fih.Length, "Size not valid for a buffer. Maximum 64k words!");

                using (var brl = new BinaryReader(File.OpenRead(lFile)))
                {
                    using (var brh = new BinaryReader(File.OpenRead(hFile)))
                    {
                        mem.LoadDual8Bit(brl, brh, 0, size);
                    }
                }
                LoadedSize = size;
                _HiFile=hFile;
                _LoFile=lFile;
            }
            else
            {
                var fi = new FileInfo(inputfile);
                if (!fi.Exists)
                    throw Errors!.Happened(ModificationRelatedErrors.FileNotFound, inputfile);
                if ((fi.Length % 2) != 0)
                    throw Errors!.Happened(ModificationRelatedErrors.SizeProblem, inputfile, fi.Length, "file not word-aligned!");
                int size = (int)fi.Length / 2;
                if (size <=0 || size > mem.Length)
                    throw Errors!.Happened(ModificationRelatedErrors.SizeProblem, inputfile, fi.Length, "Size not valid for a buffer. Maximum 64k words!");

                using (var br = new BinaryReader(File.OpenRead(inputfile)))
                {
                    mem.Load16Bit(br, 0, size, !UseLittleEndian);
                }
                LoadedSize = size;
            }
            ActualFilename = inputfile;
        }

        public async Task WriteNow(Memory mem)
        {
            if (ActualFilename == null || !LoadedSize.HasValue)
                throw new InvalidOperationException();

            string outFile = ActualFilename;

            if (UseHighLowFiles)
            {
                string hFile = _HiFile ?? throw new InvalidOperationException();
                string lFile = _LoFile ?? throw new InvalidOperationException();

                Console.WriteLine("Updating {0} and {1}...", hFile, lFile);
                using (var bwh = new BinaryWriter(File.Create(hFile)))
                {
                    using (var bwl = new BinaryWriter(File.Create(lFile)))
                    {
                        mem.DumpDual8Bit(bwl, bwh, 0, LoadedSize!.Value);
                    }
                }
            }
            else
            {
                Console.WriteLine("Updating {0}...", outFile);
                using (var bw = new BinaryWriter(File.Create(outFile)))
                {
                    mem.Dump16Bit(bw, 0, LoadedSize.Value, !UseLittleEndian);
                }
            }
        }

    }
}