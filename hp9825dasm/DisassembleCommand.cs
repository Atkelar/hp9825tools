using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CommandLineUtils;
using CommandLineUtils.Visuals;
using HP9825CPU;

namespace HP9825Disassembler
{
    [Process("dasm", "Disassemble", HelpMessage = "Create assembly source output from a binary input.")]
    internal class DisassembleCommand
        : ProcessBase
    {
        private ReturnCodeGroup<DisassemblerExitCodes> Errors;

        public DisassemblerContextParameters ContextParameters { get; private set; }
        public ListingFormatOptions Format { get; private set; }
        public InputFileOptions InputParameters { get; private set; }

        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            Errors = reg.Register<DisassemblerExitCodes>();
            InputParameters.RegisterErrors(reg);
            return base.BuildReturnCodes(reg);
        }

        protected override void BuildArguments(ParameterHandler builder)
        {
            InputParameters = builder.AddOptions<InputFileOptions>();
            ContextParameters = builder.AddOptions<DisassemblerContextParameters>();
            Format = builder.AddOptions<ListingFormatOptions>("fmt");
            builder.AddOptionalDefault("hp9825.defaults");
            builder.AddOptionalDefault("hp9825asm.defaults");
        }

        protected override async Task RunNow()
        {
            var memory = InputParameters.MakeBuffer();
            var result = await InputParameters.ReadBuffer(memory);
            var map = await ReadMappingFile(result.ActualFilename, result.Offset, result.WordCount);
            // todo: output file for listing...
            var printer = new ListingPrinter(Format.PageWidth, Format.PageHeight, Format.Options, InputParameters.Use16Bit);
            Out.WriteLine(VerbosityLevel.Normal, "Phase 1...");
            if (map.ProcessMemoryPhase1(memory))
            {
                Out.WriteLine(VerbosityLevel.Normal, "Phase 2...");
                if (map.ProcessMemoryPhase2(memory, printer))
                {
                    string OutputFile = string.IsNullOrWhiteSpace(ContextParameters.OutputFile) ? Path.ChangeExtension(InputParameters.Filename, ".listing.txt") : ContextParameters.OutputFile;
                    // TODO: make backups and write mapping file
                    File.WriteAllText(OutputFile, printer.ToString());
                    if (ContextParameters.UpdateMap && MappingFromFile != null)
                    {
                        if (!ContextParameters.NoHistory)
                        {
                            MoveToHistoryFile(MappingFromFile);
                        }
                        using(var tw = File.CreateText(MappingFromFile))
                        {
                            await map.SaveTo(tw);
                        }
                    }
                }
            }
        }

        private void MoveToHistoryFile(string mappingFromFile)
        {
            string histroyExt = string.Format("{0:yyyyMMddHHmmss}.history{1}", DateTime.Now, Path.GetExtension(mappingFromFile));
            string historicFile = Path.ChangeExtension(mappingFromFile, histroyExt);
            File.Move(mappingFromFile, historicFile);
        }

        private string? MappingFromFile = null;

        private async Task<MappingFile> ReadMappingFile(string sourceFilename, int offset, int length)
        {
            MappingFile map;
            if (!string.IsNullOrEmpty(this.ContextParameters.MappingFile))
            {
                string tryFile = ContextParameters.MappingFile;
                if (!File.Exists(tryFile))
                {
                    tryFile = Path.ChangeExtension(tryFile, ".map");
                    if (!File.Exists(tryFile))
                    {
                        if(ContextParameters.CreateNewMapping)
                        {
                            map = MappingFile.Create(InputParameters.Use16Bit, string.Format("MAPPING for {1} Created {0}", DateTime.Now, sourceFilename), offset, length);
                            MappingFromFile = tryFile;
                            return map;
                        }
                        throw this.Errors.Happened(DisassemblerExitCodes.MappingFileError, "File not found!", 0, tryFile);
                    }
                }
                if(ContextParameters.CreateNewMapping)
                    throw this.Errors.Happened(DisassemblerExitCodes.MappingFileError, "File already exists, but new mapping was requested!", 0, tryFile);
                
                using(var r = File.OpenText(tryFile))
                {
                    try
                    {
                        map =  await MappingFile.ReadFrom(r, x => Out.WriteLine(VerbosityLevel.Warnings, SplitMode.Word, "{0}:{1}", tryFile, x));
                    }
                    catch(MappingFileFormatError ex)
                    {
                        throw Errors.Happened(DisassemblerExitCodes.MappingFileError, $"MF{(int)ex.Code:000} - {ex.BaseMessage}", ex.LineNumber, tryFile);
                    }
                    catch(Exception ex)
                    {
                        throw Errors.Happened(DisassemblerExitCodes.MappingFileError, ex.Message, 0, tryFile);
                    }
                }
                MappingFromFile = tryFile;
            }
            else    
                map = MappingFile.Create(InputParameters.Use16Bit, "temporary only", offset, length);
            return map;
        }
    }
}