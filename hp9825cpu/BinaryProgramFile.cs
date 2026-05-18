using System;
using System.IO;
using System.Threading.Tasks;

namespace HP9825CPU
{
    internal class BinaryProgramFile
        : FileContent
    {
        public BinaryProgramFile(int track, int fileIndex, int fileSize, int generation)
            : base(track, fileIndex, fileSize, FileType.BinaryProgram, generation)
        {
        }

        private int[] _Data = Array.Empty<int>();

        public override int UsedSize => _Data.Length * 2;

        public const int BinaryLocation = 0x7D68;

        protected override async Task ExportContentNow(TextWriter f)
        {
            // the dump will pull the ID table from the end and prefix it as comments.
            // Thus it's going to be more readable.
            int baseAddress = BinaryLocation - (_Data.Length - 7);

            int tabAddress = _Data.Length-7;

            await f.WriteLineAsync();
            await f.WriteLineAsync(string.Format("# ID: {0}", _Data[tabAddress + 5]));
            if (_Data[tabAddress + 4] > 0)
                await f.WriteLineAsync(string.Format("# Init   @{0}", Convert.ToString(_Data[tabAddress + 4], 8)));
            if (_Data[tabAddress + 6] > 0)
                await f.WriteLineAsync(string.Format("# Uninit @{0}", Convert.ToString(_Data[tabAddress + 6], 8)));
            if (_Data[tabAddress + 3] != 0xFFFF)
                await f.WriteLineAsync(string.Format("# Command table @{0}", Convert.ToString(_Data[tabAddress + 3], 8)));
            if (_Data[tabAddress] != 0)
                await f.WriteLineAsync(string.Format("# Execution fx  @{0}", Convert.ToString(_Data[tabAddress], 8)));
            if (_Data[tabAddress + 1] != 0)
            {
                if (_Data[tabAddress + 1] >= 0x8000)
                    await f.WriteLineAsync(string.Format("# Compile fx    @{0}", Convert.ToString((_Data[tabAddress + 1] & 0x7FFF), 8)));
                else
                    await f.WriteLineAsync(string.Format("# Compile table @{0}", Convert.ToString(_Data[tabAddress + 1], 8)));
            }
            if (_Data[tabAddress + 2] != 0)
            {
                if (_Data[tabAddress + 2] >= 0x8000)
                    await f.WriteLineAsync(string.Format("# Reverse fx    @{0}", Convert.ToString((_Data[tabAddress + 2] & 0x7FFF), 8)));
                else
                    await f.WriteLineAsync(string.Format("# Reverse table @{0}", Convert.ToString(_Data[tabAddress + 2], 8)));
            }
            await f.WriteLineAsync();

            // TODO: dump tables if present...

            await MemoryFile.DumpMemory(f, _Data, baseAddress, 0, _Data.Length-7);
        }

        protected override void ReadInputFile(ITapeFileReader input)
        {
            // What we know:
            // Bianry programs require the file size plus 7 words @16545 - measured from FWBA to LWAM
            // FWBA -> First Word Binary Area is moving target, saved to AROMS[0] upon successful load.
            // BINRY (@76550) holds 7 words; these are always loaded here with the last 7 words of the binary program.
            // These entries are treated just the same as the ROM card's expansion block, other than the "ui" call.
            // +0 -> Execution code address
            // +1 -> Compile table address (or callback address with bit15 set)
            // +2 -> Reverse compile table (or callback address with bit15 set)
            // +3 -> Command table address; -1 for "not used".
            // +4 -> Binary initialization code address; 0 for "not used"
            // +5 -> "ROM ID" word.
            // +6 -> Binary "uninitialize" (binui) code address; 0 for "not used"
            // Addressing thus starts in reverse memory location, based on the first of the 7 words landing on @76550
            // and so the addresses are "absolute" within the binary file.
            // variables are allocated in the memory below that binary program. Thus the limitation to only load
            // binary programs smaller than the previously reserved (by ldb) space.
            // Confusing: LWAM is an alias for ABNRY - which is the pointer to @76550;
            // so the first ROMS table entry will either be 0 for "not loaded" or 76550; (or 1 as a flag during rcm/ldm)

            // Command table use; @10437 (EXCK) is...
            //  * looping through the ROM pointers (binary to ROM 16, mainframe section is kept separate.)
            //  * if "command table" is non -1, then...
            //  * calls "CTFC" and if found (ret 2) then...
            //  * negates "B", Adds command table base address -> address of code to jump to.
            //  * if failed, interpreter statement execution continues...

            // Command table structure: bytes with ASCII of command, followed by (0x80 | opcode); 
            // The command table is prefixed with a list of JSM addresses, sorted backwards from the
            // base address and indexed by the negative "opcode" value; i.e. ...[opcode2][opcode1][first chars in table]...
            // care must be taken with the system mainframe command table; it starts with a "0" which is EOT
            // Thus, the "LIST" command is stored as "<0>LIST" with opcode 6, but seems to be never used?!
            // Address #6 isn't present in the address table! Odd choice... maybe leftover from some earlier dev work?

            int words = input.UsedSize / 2;
            int[] data = new int[words];
            for (int i = 0; i<words; i++)
            {
                var x = input.ReadWord() ?? throw new InvalidOperationException($"Binary file exhausted after {i} words, with {words} total!");
                data[i] = (x);
            }
            _Data = data;
        }
    }
}