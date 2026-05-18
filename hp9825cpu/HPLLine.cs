using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HP9825CPU
{
    public class HPLLine
    {
        private List<byte> _LineData;

        public HPLLine(ICollection<int> linecontent, bool stop, bool trace)
        {
            Trace = trace;
            Stop = stop;
            // TODO: validate parsing of line content...
            // What we know and where did we find it:
            // EOL => 127 - "GLEOL" is looking for that as an "end of line" marker.
            // thus, the lines are bytes, padded with whitespace after EOL if required.

            // Byte code indicates index into "Reverse Compile Class Table".

            // Character string: length byte + nbytes chars. (RS4)
            // Optional-ROM code: 2nd Byte + (if 1st byte << 8 or T1 == B411)string (RS3)
            // Real number or ENT literal: n-bytes, B34 term. (RS5)
            // GTO/GSB: 2 bytes address.

            // especially the "callbacks in ROM tables" make life VERY hard...

            List<byte> lineData = new List<byte>();
            foreach(var i in linecontent)
            {
                byte b = (byte)((i >> 8) & 0xFF);
                if (b == 127)   // EOL, ignore rest...
                    break;
                lineData.Add(b);
                b = (byte)(i & 0xFF);
                if (b == 127)
                    break;
                lineData.Add(b);
            }

            _LineData = lineData;

        }

        private byte? ReverseScan(List<byte> lineData, ref int startIndex, HPLCompilerContext context)
        {
            /*
            RSCAN - Sucky documentation... no input parameters mentioned, only exit conditions.
            ACBUF - low end of buffer.
            C - address of source.
            C is both returned and saved in "LEFTE" for option ROM access if needed (adv. prog. '' handling relies on that.)
            */
            if (startIndex < 0)
                return null;
            if (startIndex >= lineData.Count)
            {
                startIndex = lineData.Count - 1;    // GLEOL returns with index of EOL - 1 in "C"
                return 127;
            }
            // OK, finally got it: "C" is the "current previous byte" pointer; We start at the beginning
            // and scan for the next "command byte" that is <= "C"...
            int start = 0;
            int i=0;
            while (i<lineData.Count)
            {
                if (i > startIndex)    // we have arrived...
                {
                    startIndex = start - 1; // don't forget: return byte before current byte!
                    return lineData[start];
                }

                start = i;
                var entry = context.MainframeInfo.ReverseEntry(lineData[i]) ?? throw new InvalidOperationException($"Didn't find reverse compile info for {lineData[i]} in {Number}");
                switch(entry.Class)
                {
                    case MnemonicClass.Literal:
                    case MnemonicClass.RealNumber:
                        // terminated with B34 = 28 dec.
                        while (i < lineData.Count && lineData[i] != 28)
                            i++;
                        break;
                    case MnemonicClass.GtoOrGsb:
                    case MnemonicClass.IntegerNumber:
                        i+=2;
                        break;
                    case MnemonicClass.OptionalROM:
                        i++;   
                        if (((lineData[i] << 8) | lineData[i-1]) == 265)
                        {
                            goto case MnemonicClass.CharacterString;
                        }
                        break;
                    case MnemonicClass.CharacterString:
                        i++;
                        int len = lineData[i];
                        i+=len;
                        break;
                }
                i++;
            }
            if (startIndex > i)
                throw new InvalidProgramException($"Didn't find previous byte in {Number} @ {startIndex} after {i} with {lineData.Count} bytes?!");
            startIndex = start - 1; // don't forget: return byte before current byte!
            return lineData[start];
        }

        public string ReverseCompile(HPLCompilerContext context, bool includeEof = false)
        {
            StringBuilder sb = new StringBuilder();
            // we start with EOL outside the loop...
            int idx = _LineData.Count;
            int last = -1;
            string? ascii = null;
            bool immfg = false;
            Stack<(HPLCompilerTables.ReverseTableEntry entry, string? ascii)> stack = new Stack<(HPLCompilerTables.ReverseTableEntry entry, string? ascii)>();
            while (idx>=0)
            {
                byte? currentByte = ReverseScan(_LineData, ref idx, context);
                if (!currentByte.HasValue)
                    break;
                var entry = context.MainframeInfo.ReverseEntry(currentByte.Value);
                if (!entry.HasValue)
                    throw new InvalidOperationException($"Missing reverse compile info for {currentByte.Value} - only defined for {context.MainframeInfo.ReverseCount}!");
                // TODO: validate detection: "non mainframe?" byte? Detection is skip if X-128 is "negative" but doesn't add up? That is mid-table...?
                ascii = null;
                int b = currentByte.Value;
                b-=128;
                if (b >= 0)
                {
                    // here we need to do something... call TSCAN?
                    // B = AMTBL, A = code byte + 1
                    var mn = context.MainframeInfo.MnemonicByCode(b + 1);
                    if (!mn.HasValue)
                        throw new InvalidOperationException($"Mnemonic code {b + 1} not found in mainframe ROM!");
                    ascii = mn.Value.Name;
                }
                // RL4
                // RL4 is the looping point for "Option ROM" based codes; if we got one of these, we nedd to re-run from here with the new info.
                bool loopRL4 = true;
                while (loopRL4)
                {
                    // assume we are done;
                    loopRL4 = false;
                    bool skipOut = false;
                    bool skipSet = false;
                    // guide => entry.
                    // ascii => 0 if none...?
                    switch(entry.Value.Class)
                    {
                        // Pat. Page 338
                        case MnemonicClass.EndOfLine:
                            // RC1
                            stack.Clear();
                            last = 0;
                            goto case MnemonicClass.Operand;
                        case MnemonicClass.UnexpectedCode:
                            // ASYER
                            throw new InvalidOperationException($"Unexpected mnemonic class in decompile line {Number}, byte {currentByte.Value}");
                        case MnemonicClass.GtoOrGsb:
                        case MnemonicClass.Operand:
                            // RC2
                            if (!skipOut) OutputNormal(sb, entry.Value, ascii);
                            // RC2A - continue for "out string"
                            if (!skipSet) immfg = false;
                            // RC3 - continue for "out number"...
                            while (stack.Count>0)
                            {
                                (entry, ascii) = stack.Pop();
                                if (!(entry.Value.Priority == 0xB && entry.Value.Class == MnemonicClass.BinaryOperator && entry.Value.MnemonicASCII == 0))
                                {
                                    // not implied multiply... output info.
                                    OutputNormal(sb, entry.Value, ascii);
                                    immfg = false;
                                }
                                last = entry.Value.Priority == 7 ? 8 : entry.Value.Priority;    // change 7 to 8 in 05463
                                if (entry.Value.Class == MnemonicClass.BinaryOperator)
                                    break;
                            }
                            break;
                        case MnemonicClass.UnaryOperator:
                        case MnemonicClass.BinaryOperator:
                            bool forceParenthesis = false;
                            if (stack.Count > 0)
                            {
                                (var tEntry, _)= stack.Peek();
                                var pOld = tEntry.Priority == 13 ? 12 : tEntry.Priority;
                                forceParenthesis = entry.Value.Priority - pOld < 0;
                            }
                            // RC5
                            if (forceParenthesis || (last - entry.Value.Priority) > 0)
                            {
                                // RC6 case...
                                // RCPAR: pushes 0 (ascii) and RK1 10050 ("guide"), then prints B51 ')'
                                stack.Push((HPLCompilerTables.ReverseTableEntry.RK1, null));
                                OutputCode(sb, ')');
                                immfg = false;
                                last = 1;
                            }
                            // RC7
                            stack.Push((entry.Value, ascii));
                            switch (entry.Value.Priority)
                            {
                                case 14:
                                    // RC8
                                    // stack paranthesis...
                                    stack.Push((HPLCompilerTables.ReverseTableEntry.RK1, null));
                                    OutputCode(sb, ')');
                                    goto case 0xFF;
                                case 15:
                                    // RC9
                                    // stack square brackets...
                                    stack.Push((HPLCompilerTables.ReverseTableEntry.RK2, null));
                                    OutputCode(sb, ']');
                                    goto case 0xFF;
                                case 0xFF:  // can't happen, is here to provide a "RCOM" jump target...
                                    // RCOM
                                    immfg = false;
                                    last = 1;
                                    break;
                            }
                            break;
                        case MnemonicClass.Literal:
                            // we have a 0x1C...0x1C formatted string... no length info...
                            int len = 0;
                            while (_LineData[idx+2+len] != 0x1C)
                                len++;
                            for(int i = len; i>0;i--)
                            {
                                sb.Insert(0, HP9825Charset.FromHP9825(_LineData[idx + 2 + i]));
                            }
                            break;
                        case MnemonicClass.CharacterString:
                            len = _LineData[idx+2];
                            sb.Insert(0, '"');
                            for(int i = len; i>0;i--)
                            {
                                if (_LineData[idx+ 2 + i] == 34)
                                    sb.Insert(0, "\"\"");
                                else
                                    sb.Insert(0, HP9825Charset.FromHP9825(_LineData[idx + 2 + i]));
                            }
                            sb.Insert(0, '"');
                            skipOut = true;
                            goto case MnemonicClass.Operand;
                        case MnemonicClass.RealNumber:
                            // REOUOT: @05610
                            immfg = !immfg;
                            if (!immfg)
                            {
                                // we need a "()"
                                stack.Push((HPLCompilerTables.ReverseTableEntry.RK1, null));
                                OutputCode(sb, ')');
                            }
                            // read compressed floating point number... that is tricky...
                            bool hasExponent = false;
                            bool showExponent = false;
                            switch (_LineData[idx+1])
                            {
                                case 0x30:
                                    // O = false, RN2
                                    hasExponent = false;
                                    showExponent = false;
                                    break;
                                case 0x31:
                                    // O = true, RN2
                                    hasExponent = false;
                                    showExponent = true;
                                    break;
                                case 0x32:
                                    // O = true, ...
                                    // exponent is next byte...
                                    hasExponent = true;
                                    showExponent = false;
                                    break;
                                case 0x33:
                                    // O = false, ...
                                    // exponent is next byte...
                                    hasExponent = true;
                                    showExponent = true;
                                    break;
                                default:
                                    throw new InvalidOperationException("Unexpected real number style?!");
                            }
                            // o= false -> no exponent!
                            int[] numParts = new int[4];    // prepare floating point number...
                            int tmpIdx = idx+2; // first number digit...
                            if (hasExponent)
                            {
                                if (_LineData[tmpIdx] >= 0x80)
                                    numParts[0] = (_LineData[tmpIdx] << 6) | 0xC000;
                                else
                                    numParts[0] = _LineData[tmpIdx] << 6;
                                tmpIdx++;
                            }
                            int numIdx = 1;
                            bool highByte = true;
                            while (_LineData[tmpIdx] != 0x1C)
                            {
                                numParts[numIdx] |= (_LineData[tmpIdx] << (highByte ? 8 : 0));
                                tmpIdx++;
                                highByte = !highByte;
                                if (highByte)
                                    numIdx++;
                            }
                            FloatingPointNumber f = new FloatingPointNumber(numParts[0], numParts[1], numParts[2], numParts[3]);
                            var exp = f.Exponent;
                            if (showExponent)
                            {
                                sb.Insert(0, exp.ToString());
                                sb.Insert(0,"e");
                                exp = 0;
                            }
                            int digitIndex = 12;
                            if (exp < 11)
                            {
                                bool nonzero = false;
                                // RF2 - output from right to left, starting at first non-zero digit.   
                                for(int i = 0;i < 11-exp; i++)
                                {
                                    var d = digitIndex > 0? f.Digit(digitIndex) : 0;
                                    digitIndex--;
                                    if (d != 0 || nonzero)
                                    {
                                        sb.Insert(0, d.ToString());
                                        nonzero = true;
                                    }
                                }
                                if (nonzero)
                                    sb.Insert(0, '.');
                            }
                            else
                            {
                                // can't have any digits after decimal...
                                for (int i = 12; i<=exp;i++)
                                {
                                    sb.Insert(0,'0');
                                }
                            }
                            // RF 4
                            while (digitIndex > 0)
                            {
                                sb.Insert(0, f.Digit(digitIndex).ToString());
                                digitIndex--;
                            }

                            //sb.Insert(0, f.ToString());
                            skipOut = true;
                            skipSet = true;
                            goto case MnemonicClass.Operand;
                        case MnemonicClass.IntegerNumber:
                            short num = (short)((_LineData[idx+2] << 8) | _LineData[idx+3]);
                            sb.Insert(0, num.ToString());
                            break;
                        case MnemonicClass.Ignore:
                            break;
                        case MnemonicClass.OptionalROM:
                            int romIndex = currentByte.Value;
                            var tabs = context.GetPlugInContext(romIndex) ?? throw new InvalidOperationException(string.Format("Missing ROM {0} in line {1}!", romIndex, Number));
                            
                            var RT3 = _LineData[idx+2];
                            //if (romIndex == 6)  // special case for string rom...
                            bool earlyOut = false;
                            if (tabs.NeedsReverseCallback)
                            {
                                    int tmpIndex = idx+1;
                                var tmp = tabs.CallReverse(0, RT3, () => {tmpIndex++; return tmpIndex < _LineData.Count ? _LineData[tmpIndex] : null;});
                                if  (tmp.Entry == null) 
                                    throw new InvalidOperationException($"Option ROM {romIndex} requested callbac for reverse compiling, call for {RT3} failed!");
                                if (tmp.Ascii != null)
                                {
                                    // TODO: maybe optimize that a bit more...
                                    ascii = tmp.Ascii;
                                    earlyOut = true;
                                }
                                entry = tmp.Entry;
                            }
                            else
                            {
                                entry = tabs.ReverseEntry(RT3 - 1);
                                if(!entry.HasValue)
                                    throw new InvalidOperationException($"Couldn't find option rom reverse compile entry {RT3 - 1} for rom {romIndex} in {Number}");
                            }
                            if (!earlyOut)
                            {
                                if (tabs.NeedsCompileCallback)
                                {
                                    int tmpIndex = idx+1;
                                    var mn = tabs.CallCompile(entry.Value.ToStorageFormat(), RT3, () => {tmpIndex++; return tmpIndex < _LineData.Count ? _LineData[tmpIndex] : null;});
                                    if (!mn.HasValue)
                                    {
                                        throw new InvalidOperationException($"Mnemonic code {RT3} with callback not found in ROM {romIndex} - current {sb}!");
                                    }
                                    ascii = mn.Value.Name;
                                }
                                else
                                {
                                    var mn = tabs.MnemonicByCode(RT3);
                                    if (!mn.HasValue)
                                        throw new InvalidOperationException($"Mnemonic code {RT3} not found in ROM {romIndex}!");
                                    ascii = mn.Value.Name;
                                }
                            }
                            loopRL4 = true; // and again!
                            break;
                        default:
                            throw new InvalidOperationException($"Unexpected mnemonic class: {entry.Value.Class}");
                    }
                }
            }
            if (!includeEof && sb.Length>0)
                sb.Length--;
            return sb.ToString();
        }

        private void OutputNormal(StringBuilder sb, HPLCompilerTables.ReverseTableEntry entry, string? ascii)
        {
            // entry = A, ascii = B
            // NMOUT
            var mi = entry.MnemonicChar;
            if (ascii == null)
                OutputCode(sb, mi);
            else
            {
                // a = character count?
                foreach(var c in ascii.Reverse())
                    OutputCode(sb, c);
            }
        }

        private void OutputCode(StringBuilder sb, char mi)
        {
            // RCOUT
            if (mi == HP9825Charset.FromHP9825(0))  // skip zero char...
                return;
            sb.Insert(0, mi);
        }

        public bool Stop { get; private set; }
        public bool Trace { get; private set; }

        public int Number { get; internal set; }
    }
}