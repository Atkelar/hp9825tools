using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CommandLineUtils;
using HP9825CPU;

namespace HP9825Utils
{
    [Process("texp", "TapeExport", HelpMessage = "Exportes files from the tape image.")]
    public class CartridgeExportFileCommand
        : ProcessBase
    {
        private CartridgeInputFileOptions? InputFrom;
        private CartridgeAddressOptions? TapeAddress;
        private ExportFileOptions? ExportFile;
        private HplOptions? HplInput;
        private ReturnCodeGroup<CartridgeJobCodes>? Results;


        protected override void BuildArguments(ParameterHandler builder)
        {
            InputFrom = builder.AddOptions<CartridgeInputFileOptions>();
            TapeAddress = builder.AddOptions<CartridgeAddressOptions>();
            ExportFile = builder.AddOptions<ExportFileOptions>();
            HplInput = builder.AddOptions<HplOptions>("hpl");
        }
        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            Results = reg.Register<CartridgeJobCodes>();
            return base.BuildReturnCodes(reg);
        }

        protected async override Task RunNow()
        {
            if (InputFrom == null || Results == null || TapeAddress == null)
                throw new InvalidOperationException();
            InputFrom.Validate(Results);
            TapeAddress.Validate(true, true, Results);

            Write("Reading cartridge from {0}...", InputFrom.Filename);
            var tc = await TapeCartridge.Load(InputFrom.Filename);
            WriteLine("done!");

            // TODO: make parameterized version!
            HPLCompilerContext? context = await HplInput.LoadFiles();

            bool useFileNumbers = TapeAddress.StartIndex != TapeAddress.EndIndex;

            using (var tm = new TapeCartridgeManager(tc))
            {
                foreach(var trk in TapeAddress.GetTracks())
                {
                    if (!tm.IsEmpty(trk))
                    {
                        foreach(var entry in tm.ReadDirectory((TapeTrack)trk))
                        {
                            if(entry.Index >= TapeAddress.StartIndex && (!TapeAddress.EndIndex.HasValue || TapeAddress.EndIndex.Value >= entry.Index))
                            {
                                // got a file we want...
                                var file = tm.ReadFile((TapeTrack)trk, entry.Index);
                                if (file != null)
                                {
                                    string OutputFile = ExportFile.MakeFilename(InputFrom.Filename, useFileNumbers ? entry.Index : null);
                                    if (!ExportFile.ForceOverwrite  && File.Exists(OutputFile))
                                        throw Results.Happened(CartridgeJobCodes.OutputOverwrite, OutputFile);
                                    using (var f = System.IO.File.CreateText(OutputFile))
                                    {
                                        if(file is ICompilerContextual c)
                                            c.HPLContext = context;
                                        await file.ExportTo(f);
                                    }
                                }

                            }
                        }
                    }
                    WriteLine();
                }
            }
        }
    }
}