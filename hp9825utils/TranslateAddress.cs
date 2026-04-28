using System;
using System.Threading.Tasks;
using CommandLineUtils;
using HP9825CPU;

namespace HP9825Utils
{
    [Process("ta", "TranslateAddress", HelpMessage = "Translates address between normal and inverted address bus mappings. Useful to derive addresses for ROM programming.")]
    public class TranslateAddressCommand
        : ProcessBase
    {
        protected override void BuildArguments(ParameterHandler builder)
        {
            Settings = builder.AddOptions<MappingSettings>();
        }

        public MappingSettings Settings {get;set;}

        protected override async Task RunNow()
        {
            int bankSize = Settings.BankSize;
            int targetBank = Settings.Bank;
            
            Output.WriteLine( VerbosityLevel.Normal, "Aligning ROMs for banks of {0:x4} words, using bank {1}", bankSize, targetBank);
            using (var tab = this.Output.Table(VerbosityLevel.Normal, 
                x=>x.Column(10,30, x=>x.Align(HorizontalAlignment.Left).Head("Name"))
                    .Column(10, x=>x.Align(HorizontalAlignment.Right).Head("Normal").Format("x4"))
                    .Column(10, x=>x.Align(HorizontalAlignment.Right).Head("Length"))
                    .Column(10, x=>x.Align(HorizontalAlignment.Right).Head("InvertedBase").Format("x4"))
                    .Column(10, x=>x.Align(HorizontalAlignment.Right).Head("InvertedLimit").Format("x4"))
                    .Column(10, x=>x.Align(HorizontalAlignment.Right).Head("BankFrom").Format("x4"))
                    .Column(10, x=>x.Align(HorizontalAlignment.Right).Head("BankTo").Format("x4"))
                    .Column(10, x=>x.Align(HorizontalAlignment.Right).Head("Target (dec)"))
                    ))
            {
                PrintMapping(tab, OptionRom.Strings, bankSize, targetBank);
                PrintMapping(tab, OptionRom.Plotter, bankSize, targetBank);
                PrintMapping(tab, OptionRom.Matrix, bankSize, targetBank);
                PrintMapping(tab, OptionRom.MassMemory, bankSize, targetBank);
                PrintMapping(tab, OptionRom.SystemProgramming, bankSize, targetBank);
                PrintMapping(tab, OptionRom.GeneralIO, bankSize, targetBank);
                PrintMapping(tab, OptionRom.ExtendedIO, bankSize, targetBank);
                PrintMapping(tab, OptionRom.AdvancedProgramming, bankSize, targetBank);
            }
        }

        private void PrintMapping(ITableFormatter tab, OptionRom rom, int bankSize, int targetBank)
        {
            int address, length;
            MemoryManager.TranslateRomOptions(rom, out address, out length);
            int invertedBaseAddress = (~address & 0x7FFF);
            int invertedMaxAddress = (~(address + length - 1) & 0x7FFF);
            int bankBase = bankSize * targetBank;

            tab.Line(rom, address,length, invertedBaseAddress, invertedMaxAddress, invertedMaxAddress + bankBase, invertedBaseAddress + bankBase, invertedMaxAddress + bankBase);
        }
    }
}