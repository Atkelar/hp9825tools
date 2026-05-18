using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HP9825CPU
{
    public class MemoryFile 
        : FileContent
    {
        private int[] _Data = Array.Empty<int>();
        private int _RomWord;

        public MemoryFile(int track, int fileIndex, int fileSize, int generation, int extendedFlag2)
            : base (track, fileIndex, fileSize, FileType.MemoryFile, generation)
        {
            _RomWord = extendedFlag2;
        }

        public override int UsedSize => _Data.Length * 2;

        internal static async Task DumpMemory(TextWriter f, int[] words, int baseAddress, int startOffset, int length)
        {
            int addr = baseAddress;
            await f.WriteLineAsync();
            await f.WriteLineAsync(string.Format("MEM: {0,5}", Convert.ToString(baseAddress, 8)));
            StringBuilder charsForLine = new StringBuilder();
            if ((addr % 8) != 0)    // leading row to align rows to 8-based numbers.
            {
                int num = baseAddress % 8;
                for(int i = 0; i < num;i++)
                {
                    await f.WriteAsync("       ~");
                    charsForLine.Append('╳');
                    charsForLine.Append('╳');
                }
            }
            for(int idx = 0; idx < length; idx++)
            {
                if ((addr % 8) == 0)
                {
                    if (addr > baseAddress) 
                        await f.WriteLineAsync(string.Format("  #   {0,5} {1}", Convert.ToString(addr-8,8), charsForLine));
                    charsForLine.Clear();
                }
                if ((addr % 1024)==0)
                    await f.WriteLineAsync(string.Format("#   --- {0,5} - {1,-5}", Convert.ToString(addr,8), Convert.ToString(addr + 1023,8)));
                await f.WriteAsync(string.Format("  {0,6}", Convert.ToString(words[idx + startOffset],8)));
                charsForLine.Append(HP9825Charset.FromHP9825((byte)((words[idx + startOffset] >> 8) & 0xFF)));
                charsForLine.Append(HP9825Charset.FromHP9825((byte)(words[idx + startOffset] & 0xFF)));
                addr++;
            }
            while ((addr % 8) != 0)
            {
                await f.WriteAsync("       ~");
                charsForLine.Append('╳');
                charsForLine.Append('╳');
                addr++;
            }
            await f.WriteLineAsync(string.Format("  #   {0,5} {1}", Convert.ToString(addr-8,8), charsForLine));
            
            await f.WriteLineAsync();
        }

        protected override async Task ExportContentNow(TextWriter f)
        {
            await f.WriteLineAsync(string.Format("#  Roms present during save:"));
            if (_RomWord == 0)
                await f.WriteLineAsync("#  none");
            else
                await f.WriteLineAsync(string.Format("REQUIRES: {0}", MakeROMRequireString()));
            Console.WriteLine(_RomWord);
            // the saved memory is between 
            // OFWAM (=lowest available RAM address in system, init'd by the detect RAM code)
            // and
            // JSTK-1 (=JSM stack area) anything above J-Stack isn't saved!
            // that includes: a whole lot of temporaries, the OP1/OP2/RES values as well as the AR1 accumulator.
            // naturally, the temps are used by the load code, so... well... chicken/egg, these can't be saved.
            // worried about the RES particularly. That is - to my current knowledge - the "res" value...
            // The call stack gets saved/restored via copy in the compile stack range...

            int baseAddress = 0x7F9B - _Data.Length; // JSTK - 1 - # of words...

            // output in octall blocks...

            await DumpMemory(f, _Data, baseAddress, 0, _Data.Length);

        }

        private string MakeROMRequireString()
        {
            if (_RomWord == 0)
                return string.Empty;
            var sb = new StringBuilder();
            AppendIfSet(sb, _RomWord,0x100, "ADVPGM");
            AppendIfSet(sb, _RomWord,0x800, "STRING");
            // TODO check up on other ROM init codes to find out the bit pattern!
            return sb.ToString();
        }

        private void AppendIfSet(StringBuilder sb, int romWord, int mask, string name)
        {
            if ((romWord & mask)==mask)
            {
                if (sb.Length>0)
                    sb.Append(", ");
                sb.Append(name);
            }
        }

        protected override void ReadInputFile(ITapeFileReader input)
        {
            int words = input.UsedSize / 2;
            int[] data = new int[words];
            for (int i = 0; i<words; i++)
            {
                var x = input.ReadWord() ?? throw new InvalidOperationException($"Memory file exhausted after {i} words, with {words} total!");
                data[i] = x;
            }
            _Data = data;
        }
    }
}