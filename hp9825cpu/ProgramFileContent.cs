using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Threading.Tasks;

namespace HP9825CPU
{
    public class ProgramFileContent
        : FileContent, ICompilerContextual
    {
        public ProgramFileContent(int? track, int? fileIndex, int fileSize, int generation) 
            : base(track, fileIndex, fileSize, FileType.UserProgram, generation)
        {
        }

        public HPLProgram Program { get; private set; }
        public HPLCompilerContext? HPLContext { get; set; }

        protected override async Task ExportContentNow(TextWriter f)
        {
            await f.WriteLineAsync(string.Format("# got {0} lines of code.", Program.Lines));
            await f.WriteLineAsync();
            foreach (var l in Program)
            {
                if (HPLContext != null)
                    await f.WriteLineAsync(string.Format("{0,3} {1}", l.Number, l.ReverseCompile(HPLContext)));
                else
                    await f.WriteLineAsync(string.Format("{0,3} {1}", l.Number, l.ToString()));
            }
        }

        protected override void ReadInputFile(ITapeFileReader input)
        {
            // structure of "user program" is as follows:
            // See pat. page 250, 11 and 12!
            // line bridge -> line data -> ....
            // a line bridge is a word with two bytes: the 
            // left (high order) one is the "previous" line pointer
            // the right one is the next line pointer.
            // NOTE: only 7 bits (0-127) are possible. The 8th
            // bit indicates "Trace Flag" for high and "Stop Flag"
            // for low order bytes!
            // upon reading, we dissect the prev/next pointers
            // which should guide us through the program line by line
            // and one after another. "out of sequence" lines seem
            // to not exist, but we catch that error and report it.

            int lastLineLength = -1; // expecting this as a back-pointer.
            int lineNumber = 0;
            List<int> linecontent = new List<int>();
            HPLProgram prog = new HPLProgram();
            while (!input.EndOfFile)
            {
                if (lastLineLength == 0)
                    throw new InvalidOperationException($"Next line ref was null, but more data is coming in at line {lineNumber}");
                if (lastLineLength==-1)
                    lastLineLength = 0;
                int bridge = input.ReadWord() ?? 0;
                bool trace, stop;
                trace = (bridge & 0x8000) != 0;
                stop = (bridge & 0x80) != 0;
                int lll = (bridge & 0x7F00) >> 8;
                int tll = bridge & 0x7F;
                if (lll != lastLineLength)
                    throw new InvalidOperationException($"Last line ref in bridge expected to be {lastLineLength} but was {lll} in line nr. {lineNumber}");
                lastLineLength = tll;   // next loop.

                if (tll > 0)
                {
                    linecontent.Clear();
                    for (int i = 1; i < tll; i++)   // bridge included in length, start at 1 for words...
                    {
                        var lineWord = input.ReadWord();
                        if (!lineWord.HasValue)
                            throw new InvalidOperationException($"Line {lineNumber} reported to have {tll-1} words but EOF at word {i}!");
                        linecontent.Add(lineWord.Value);
                    }

                    HPLLine hpLine = new HPLLine(linecontent, stop, trace);
                    prog.Store(hpLine);
                }
                lineNumber++;
            }
            this.Program = prog;
        }
    }
}