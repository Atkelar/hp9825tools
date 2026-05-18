using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CommandLineUtils;
using HP9825CPU;

namespace HP9825Utils
{
    [Process("tlist", "TapeListing", HelpMessage = "Lists the content of the tape, similar to the 'tlist' command.")]
    public class GetCartridgeListingCommand
        : ProcessBase
    {
        private CartridgeInputFileOptions? InputFrom;
        private CartridgeAddressOptions? TapeAddress;
        private ReturnCodeGroup<CartridgeJobCodes>? Results;

        protected override void BuildArguments(ParameterHandler builder)
        {
            InputFrom = builder.AddOptions<CartridgeInputFileOptions>();
            TapeAddress = builder.AddOptions<CartridgeAddressOptions>();
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

            using (var tm = new TapeCartridgeManager(tc))
            {
                foreach(var trk in TapeAddress.GetTracks())
                {
                    WriteLine("Track #{0}", trk);
                    if (tm.IsEmpty(trk))
                    {
                        WriteLine(" <empty>");
                    }
                    else
                    {
                            using(var tab = Out.Table(x=>
                                x.Column(10, c=>c.Head("Index").Align(HorizontalAlignment.Right))
                                    .Column(10, 25, c=>c.Head("Type").Align(HorizontalAlignment.Left))
                                    .Column(6, c=>c.Head("F.Size").Align(HorizontalAlignment.Right))
                                    .Column(6, c=>c.Head("U.Size").Align(HorizontalAlignment.Right))
                                    .Column(6, c=>c.Head("Gen#").Align(HorizontalAlignment.Right))
                                    .Column(4, c=>c.Head("Sec?").Align(HorizontalAlignment.Right))
                                    .Column(4, c=>c.Head("ExFl").Align(HorizontalAlignment.Center).Format("x4"))
                                    ))
                            {
                                foreach(var entry in tm.ReadDirectory((TapeTrack)trk))
                                {
                                    if(entry.Index >= TapeAddress.StartIndex && (!TapeAddress.EndIndex.HasValue || TapeAddress.EndIndex.Value >= entry.Index))
                                    {
                                        tab.Line(entry.Index, entry.TranslatedType, entry.FileSize, entry.UsedSize, entry.Generation);
                                    }
                                }
                            }
                            WriteLine();
                    }
                    WriteLine();
                }
            }


            //tc.LoadedFromPackage
        }

    }
}
