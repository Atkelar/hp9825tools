using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HP9825CPU
{
    public class KeyFileContent
        : FileContent, IEnumerable<FunctionKeyDefintion>
    {
        private List<FunctionKeyDefintion> _Defs = new List<FunctionKeyDefintion>();

        public KeyFileContent(int? track, int? fileIndex, int fileSize, int generation) 
            : base(track, fileIndex, fileSize, FileType.KeyFile, generation)
        {
        }

        public IEnumerator<FunctionKeyDefintion> GetEnumerator()
        {
            return _Defs.GetEnumerator();
        }

        protected override async Task ExportContentNow(TextWriter f)
        {
            await f.WriteLineAsync(string.Format("# Got {0} function keys defined.", _Defs.Count));
            await f.WriteLineAsync();
            foreach(var def in _Defs)
            {
                await f.WriteLineAsync(string.Format("{0,2}:{1}", def.Key, def.Text));
            }
        }

        protected override void ReadInputFile(ITapeFileReader input)
        {
            // format is: 0xFFxx - where xx is the index of the function key: 0-23 following are the characters that make up the definition...
            // no terminator in key spec; Filler byte (for odd number of chars) is 00, next record is FFxx again, EOF is last key...
            bool first = true;
            StringBuilder sb = new StringBuilder();
            int currentKey = -1;
            while(true)
            {
                int? nextWord = input.ReadWord();
                if (!nextWord.HasValue)
                    break;
                if ((nextWord.Value & 0xFF00) == 0xFF00)
                {
                    // next indicator.
                    first = false;
                    if(currentKey >= 0)
                    {
                        _Defs.Add(new FunctionKeyDefintion(currentKey, sb.ToString()));
                    }
                    currentKey = nextWord.Value & 0xFF;
                    sb.Clear();
                }
                else
                {
                    if (first)
                        throw new InvalidOperationException($"First word in key file isn't a marker word!");
                    int c1 = (nextWord.Value >> 8) & 0xFF;
                    if (c1 != 0)
                        sb.Append(HP9825Charset.FromHP9825((byte)c1));
                    c1 = nextWord.Value & 0xFF;
                    if (c1 != 0)
                        sb.Append(HP9825Charset.FromHP9825((byte)c1));
                }
            }
            if (currentKey>=0)
            {
                _Defs.Add(new FunctionKeyDefintion(currentKey, sb.ToString()));
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _Defs.GetEnumerator();
        }
    }
}