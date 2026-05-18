using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLineUtils;
using CommandLineUtils.Visuals;
using HP9825CPU;

namespace HP9825Utils
{
    [Process("mkhpl", "MakeHPLTables", HelpMessage = "Create the HPL tables (for compile/reverse compile of user programs) from ROM images.")]
    public class ExtractHPLTables
        : ProcessBase
    {
        private ReturnCodeGroup<ROMImageErrors>? Results;

        public InputFileOptions InputFrom;
        public ROMHPLOptions HPLOptions;

        protected override void BuildArguments(ParameterHandler builder)
        {
            InputFrom = builder.AddOptions<InputFileOptions>();
            HPLOptions = builder.AddOptions<ROMHPLOptions>();

        }
        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            Results = reg.Register<ROMImageErrors>();
            return base.BuildReturnCodes(reg);
        }

        protected async override Task RunNow()
        {
            if (InputFrom == null || Results == null)
                throw new InvalidOperationException();

            int reverseCompileAddress;
            int? reverseCompileCount = null;
            int compileTableAddress;
            int expectedSize = 0;
            int baseAddress;
            bool isSystem = false;
            int? fixupErrorInMnemonicTable = null;  // the adv. pgm. rom has a malformed table: it's x, x, eot, x - the EOT hides the "=" operator!

            bool dumpHeader = false;

            List<(string Name, int Address, int? Fixup)> auxilaryCompileTables = new List<(string Name, int Address, int? Fixup)>();
            List<(string Name, int Address, int Count)> auxilaryReverseTables = new List<(string Name, int Address, int Count)>();

            var baseName = HPLOptions.RomName.ToLowerInvariant();
            int romId = -1;

            switch (baseName)
            {
                case "system":
                    reverseCompileAddress = 6975;
                    reverseCompileCount = 192;
                    expectedSize = 12 * 1024;
                    compileTableAddress = 825;
                    baseAddress = 0;
                    isSystem = true;
                    break;
                case "sysmatha":
                    reverseCompileAddress = 10407;
                    //reverseCompileCount = 8;
                    compileTableAddress = 10366;
                    expectedSize = 12 * 1024;
                    baseAddress = 0;
                    romId = 15;
                    break;
                case "sysmathb":
                    reverseCompileAddress = 0x2C0E;
                    //reverseCompileCount = 8;
                    compileTableAddress = 0x2C1A;
                    expectedSize = 12 * 1024;
                    baseAddress = 0;
                    romId = 14;
                    break;
                case "strings":
                    expectedSize = 1024;
                    compileTableAddress = 0x4C49;
                    reverseCompileAddress = -1; // we need a callback!
                    baseAddress = 0x4C00;
                    auxilaryReverseTables.Add(("def", 0x4C3B, 16));
                    romId = 6;
                    //dumpHeader = true;
                    break;
                case "advpgm":
                    expectedSize = 1024;
                    compileTableAddress = -1;
                    auxilaryCompileTables.Add(("def", 0x404E, 1));
                    reverseCompileAddress = 0x40c9;
                    reverseCompileCount = 12;
                    baseAddress = 0x4000;
                    romId = 9;
                    break;
                case "genericio":
                    expectedSize = 1024;
                    compileTableAddress = 0x342B;
                    reverseCompileAddress = 0x3443;
                    baseAddress = 0x3400;
                    romId = 12;
                    break;
                case "extendedio":
                    expectedSize = 2048;    // TODO: how does the ROM initialization skip over that 2nd page here?!
                    compileTableAddress = 0x4472;
                    reverseCompileAddress = 0x44d8;
                    baseAddress = 0x4400;
                    romId = 8;
                    break;
                // case "extendedio2":
                //     expectedSize = 2048;
                //     compileTableAddress = 0x4b67;
                //     reverseCompileAddress = 0x4b6e;
                //     baseAddress = 0x4400;
                //     romId = 8;
                //     //dumpHeader = true;
                //     break;
                case "plot62":
                    expectedSize = 1024;
                    compileTableAddress = 0x3823;
                    reverseCompileAddress = 0x3843;
                    baseAddress = 0x3800;
                    romId = 11;
                    break;
                case "plot72":
                    expectedSize = 1024;
                    compileTableAddress = 0x3823;
                    reverseCompileAddress = 0x3843;
                    baseAddress = 0x3800;
                    romId = 11;
                    break;
                case "sysprog":
                    expectedSize = 1024;
                    compileTableAddress = 0x502C;
                    reverseCompileAddress = 0x5052;
                    baseAddress = 0x5000;
                    romId = 4;
                    break;
                case "matrix":
                    expectedSize = 1024;
                    compileTableAddress = 0x3c20;
                    reverseCompileAddress = 0x3c38;
                    baseAddress = 0x3C00;
                    romId = 10;
                    break;
                default:
                    throw Results.Happened(ROMImageErrors.InvalidROMName, baseName);
            }

            var mem = HP9825CPU.Memory.MakeMemory(false); 
            InputFrom.LoadToOffset = baseAddress;
            var result = await InputFrom.ReadBuffer(mem);
            if (result.Offset != baseAddress)
                throw new NotImplementedException();
            if (expectedSize != 0 && result.WordCount != expectedSize)
                throw Results.Happened(ROMImageErrors.SizeMismatch, expectedSize, result.WordCount);
            if(dumpHeader)
            {
                int i = 0;
                while (i< expectedSize)
                {
                    Out.WriteLine("ROM @{0:x4} indicates the following linkage data:", baseAddress+i);
                    DumpAddress(mem, "Execution Routine", baseAddress+i, 0);
                    DumpAddress(mem, "Compile Table", baseAddress+i, 1);
                    DumpAddress(mem, "Reverse Compile Table", baseAddress+i, 2);
                    DumpAddress(mem, "Command Table", baseAddress+i, 3);
                    DumpAddress(mem, "Initialization Routine", baseAddress+i, 4);
                    Out.WriteLine("  ROM ID: {0}", mem[baseAddress+i + 5]);
                    i+=1024;
                }
                return;
            }
            Out.Write("Done reading, creating menmonic table");
            HPLCompilerTables tabs = new HPLCompilerTables();
            tabs.RomId = romId;

            // List<int> tab = new List<int>();
            // for (int i=0;i<reverseCompileCount;i++)
            // {
            //     tab.Add(mem[i+reverseCompileAddress]);
            // }
            tabs.SetCompileTable(mem, compileTableAddress, fixupErrorInMnemonicTable);

            Out.Write(", done. Reverse table");

            if (!isSystem)
                reverseCompileCount ??= tabs.NeedsCompileCallback ? 0xFF : tabs.MnemonicCount;
            tabs.SetReverseCompileTable(mem, reverseCompileAddress, isSystem, reverseCompileCount);

            Out.WriteLine(", done.");

            foreach(var x in auxilaryReverseTables)
            {
                Out.WriteLine(" auxilary reverse: {0}", x.Name);
                tabs.SetAuxilaryReverseCompileTable(mem, x.Name, x.Address, x.Count);
            }
            foreach(var x in auxilaryCompileTables)
            {
                Out.WriteLine(" auxilary compile: {0}", x.Name);
                tabs.SetAuxilaryCompileTable(mem, x.Name, x.Address, x.Fixup);
            }

            // tab.Clear();
            // for(int i = mnemonicsTableAddress-tabs.MnemonicCount;i<mnemonicsTableAddress;i++)
            //     tab.Add(mem[i]);
            
            // tabs.SetMnemonicClassInfo(tab);

            Out.WriteLine(string.Format(" {0} entries in reverse compile table.", tabs.ReverseCount));
            
            Out.WriteLine(string.Format(" {0} mnemonics in compile table.", tabs.MnemonicCount));

            await tabs.WriteTo(baseName + ".hplcomp");
        }

        private void DumpAddress(Memory mem, string label, int baseAddress, int relativeAddress)
        {
            int addr = baseAddress + relativeAddress;
            int loc = mem[addr];
            if (loc == 0xFFFF)
                Out.WriteLine("  {0,20} - missing (-1)", label);
            else
            {
                if (loc < 0x8000)
                    Out.WriteLine("  {2,20} {0:x4} - rel. {1:x4}", loc, loc-baseAddress, label);
                else
                {
                    Out.WriteLine("  {0} is code, not data! refers to {1:x4}", label, loc & 0x7FFF);
                }
            }
        }
    }

}