using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace HP9825CPU
{
    public class HPLCompilerTables
    {
        public void SetMnemonicClassInfo(IEnumerable<int> tableData)
        {
            var list = tableData.Reverse().ToArray();
            for (int index = 0;index< _Mnemonics.Length;index++)
            {
                var w = list[_Mnemonics[index].Code - 1];
                _Mnemonics[index].Class = (byte)((w >> 8) & 0xFF);
                _Mnemonics[index].Token = (byte)((w & 0xFF));
                index++;
            }
        }

        public int RomId { get; set; }

        private void ParseCompileTable(Memory mem, int address, List<MnemonicDefinition> temp, int? fixupEntries = null)
        {
            StringBuilder sb = new StringBuilder();

            int offset = address;
            bool lowByte = false;
            bool eot = false;
            while (offset < 0x7FFF)
            {
                byte b;
                if (lowByte)
                {
                    b = (byte)(mem[offset] & 0xFF);
                    lowByte = false;
                    offset++;
                }
                else
                {
                    b = (byte)((mem[offset] >> 8) & 0xFF);
                    lowByte = true;
                }
                if (b == 0x80)
                {
                    eot = true;
                    if (!fixupEntries.HasValue)
                        break;
                }
                else
                {
                    if ((b & 0x80)==0)
                    {
                        sb.Append(HP9825Charset.FromHP9825(b));
                    }
                    else
                    {
                        var md=new MnemonicDefinition()
                        {
                            Name = sb.ToString(),
                            Code = (byte)(b & 0x7F)
                        };
                        var w = mem[address - md.Code];
                        md.Class = (byte)((w >> 8) & 0xFF);
                        md.Token = (byte)((w & 0xFF));
                        
                        temp.Add(md);
                        sb.Clear();
                        if (eot && fixupEntries.HasValue)
                        {
                            fixupEntries--;
                            if (fixupEntries == 0)
                                break;
                        }
                    }
                }
            }
        }

        public void SetCompileTable(Memory mem, int startOffset, int? fixupEntries = null)
        {
            if (startOffset < 0)
            {
                NeedsCompileCallback = true;
                return;
            }
            List<MnemonicDefinition> defs = new List<MnemonicDefinition>();
            ParseCompileTable(mem, startOffset, defs, fixupEntries);
            _Mnemonics = defs.ToArray();
        }

        public int MnemonicCount => _Mnemonics.Length;

        private MnemonicDefinition[] _Mnemonics = Array.Empty<MnemonicDefinition>();

        private void ParseReverseCompileTable(Memory mem, int startOffset, int numberOfEntries, List<ReverseTableEntry> temp)
        {
            int offset = startOffset;
            bool lowByte = false;
            int max = numberOfEntries;
            while (offset < 0x7FFF && max > 0)
            {
                byte b;
                if (lowByte)
                {
                    b = (byte)(mem[offset] & 0xFF);
                    lowByte = false;
                    offset++;
                }
                else
                {
                    b = (byte)((mem[offset] >> 8) & 0xFF);
                    lowByte = true;
                }
                // if (b == 0x00)
                //     break;
                temp.Add(new ReverseTableEntry() 
                {
                    Priority = (byte)((b >> 4) & 0xF),
                    Class = (MnemonicClass)(b & 0xF),
                    MnemonicASCII = 0
                });
                max--;
            }
        }

        public void SetReverseCompileTable(Memory mem, int startOffset, bool isSystemFormat, int? numberOfEntries = null)
        {
            if (startOffset == -1)
            {
                _ReverseTable = Array.Empty<ReverseTableEntry>();
                NeedsReverseCallback = true;
                return;
            }
            List<ReverseTableEntry> temp = new List<ReverseTableEntry>();
            if (isSystemFormat)
            {
                if (!numberOfEntries.HasValue)
                    throw new InvalidOperationException("System format doesn't have a terminator! Need count!");
                for(int i = 0; i< numberOfEntries.Value;i++)
                {
                    var x = mem[i + startOffset];
                    temp.Add(new ReverseTableEntry() 
                        { 
                            Priority = (byte)((x >> 12) & 0xF),
                            Class = (MnemonicClass)((x >> 8) & 0xF),
                            MnemonicASCII = (byte)(x & 0xFF)
                        });
                }
            }
            else
            {
                ParseReverseCompileTable(mem, startOffset, numberOfEntries ?? 0xFF, temp);
            }
            _ReverseTable = temp.ToArray();
        }

        public static async Task<HPLCompilerTables> LoadFrom(TextReader file, HPLCompilerTables.CompilerCallbackDelegate? compilerCallback = null, HPLCompilerTables.ReverseCallbackDelegate? reverseCallback = null)
        {
            HPLCompilerTables result = new HPLCompilerTables();
            List<ReverseTableEntry> rev = new List<ReverseTableEntry>();
            List<MnemonicDefinition> mnem = new List<MnemonicDefinition>();
            using (var r = XmlReader.Create(file, new XmlReaderSettings() { Async = true }))
            {
                while (!r.EOF && r.NodeType != XmlNodeType.Element && r.LocalName != "hplCompiler" && r.Depth != 1)
                    await r.ReadAsync();
                if (r.EOF)
                    throw new InvalidOperationException("Missing hplCompiler root element!");
                result.RomId = int.Parse(r.GetAttribute("romid") ?? "0", System.Globalization.CultureInfo.InvariantCulture);

                if (!await r.ReadAsync())
                    throw new InvalidOperationException("root element empty?!");
                while(!r.EOF && r.Depth <= 2)
                {
                    if (r.NodeType == XmlNodeType.Element && r.Depth == 1)
                    {
                        switch(r.LocalName)
                        {
                            case "reverse":
                                result.NeedsReverseCallback = await ReadReverseElements(r, rev);
                                break;
                            case "mnemonics":
                                result.NeedsCompileCallback = await ReadMnemonicsElement(r, mnem);
                                break;
                            case "aux-rev":
                                string name = r.GetAttribute("name") ?? "";
                                List<ReverseTableEntry> temp = new List<ReverseTableEntry>();
                                await ReadReverseElements(r, temp);
                                result._AuxReverseTables[name] = temp.ToArray();
                                break;
                            case "aux-mnem":
                                name = r.GetAttribute("name") ?? "";
                                List<MnemonicDefinition> temp2 = new List<MnemonicDefinition>();
                                await ReadMnemonicsElement(r, temp2);
                                result._AuxCompileTables[name] = temp2.ToArray();
                                break;
                            default: 
                                throw new InvalidOperationException($"unexpected element: {r.LocalName}?!");
                        }
                    }
                    if (!await r.ReadAsync())
                        break;
                }
            }
            result._ReverseTable = rev.ToArray();
            result._Mnemonics = mnem.ToArray();
            result.CompileCallback = compilerCallback;
            result.ReverseCallback = reverseCallback;
            return result;
        }

        private static async Task<bool> ReadMnemonicsElement(XmlReader r, List<MnemonicDefinition> mnem)
        {
            if (r.GetAttribute("callback") == "true")
                return true;

            var startTag = r.LocalName;
            
            while (await r.ReadAsync())
            {
                if (r.NodeType == XmlNodeType.EndElement && r.LocalName == startTag)
                {
                    return false;
                }
                if (r.NodeType == XmlNodeType.Element && r.LocalName == "def")
                {
                    mnem.Add(new MnemonicDefinition()
                    {
                        Class = (byte)int.Parse(r.GetAttribute("class") ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                        Code = (byte)int.Parse(r.GetAttribute("code") ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                        Token =(byte)int.Parse(r.GetAttribute("token") ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                        Name = r.GetAttribute("name") ?? ""
                    });
                }
            }
            return false;
        }

        private static async Task<bool> ReadReverseElements(XmlReader r, List<ReverseTableEntry> rev)
        {
            if (r.GetAttribute("callback") == "true")
                return true;
            string openingTag = r.LocalName;
            
            while (await r.ReadAsync())
            {
                if (r.NodeType == XmlNodeType.EndElement && r.LocalName == openingTag)
                    return false;
                if (r.NodeType == XmlNodeType.Element && r.LocalName == "def")
                {
                    rev.Add(new ReverseTableEntry()
                    {
                        Class = Enum.Parse<MnemonicClass>(r.GetAttribute("class") ?? "?", true),
                        Priority = (byte)int.Parse(r.GetAttribute("prio") ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                        MnemonicASCII =(byte)int.Parse(r.GetAttribute("char") ?? "0", System.Globalization.CultureInfo.InvariantCulture)
                    });
                }
            }
            return false;
        }

        public static async Task<HPLCompilerTables> LoadFrom(string filename, HPLCompilerTables.CompilerCallbackDelegate? compilerCallback = null, HPLCompilerTables.ReverseCallbackDelegate? reverseCallback = null)
        {
            using(var r = File.OpenText(filename))
            {
                return await LoadFrom(r, compilerCallback, reverseCallback);
            }
        }

        private ReverseTableEntry[] _ReverseTable = Array.Empty<ReverseTableEntry>();

        public bool NeedsReverseCallback { get; private set; }
        public bool NeedsCompileCallback { get; private set; }

        public int ReverseCount => _ReverseTable.Length;

        internal ReverseTableEntry? ReverseEntry(int index)
        {
            if (index <0 || index >= _ReverseTable.Length)
                return null;
            return _ReverseTable[index];
        }

        internal struct MnemonicDefinition
        {
            public string Name;
            public byte Code;
            public byte Class;
            public byte Token;
        }

        internal struct ReverseTableEntry
        {

            public static readonly ReverseTableEntry RK1 = new HPLCompilerTables.ReverseTableEntry() { Class = MnemonicClass.Operand, Priority = 0, MnemonicASCII = 0x28 };
            public static readonly ReverseTableEntry RK2 = new HPLCompilerTables.ReverseTableEntry() { Class = MnemonicClass.Operand, Priority = 0, MnemonicASCII = 0x5B };

            public byte Priority;
            public MnemonicClass Class;
            public byte MnemonicASCII;
            public char MnemonicChar => HP9825Charset.FromHP9825(MnemonicASCII);

            internal int ToStorageFormat()
            {
                return (Priority << 12) | (((int)Class) << 8) | MnemonicASCII;
            }
        }

        public async Task WriteTo(System.IO.TextWriter file)
        {
            using (var xw = XmlWriter.Create(file, new XmlWriterSettings() { Async = true, Indent = true }))
            {
                await xw.WriteStartDocumentAsync();
                await xw.WriteStartElementAsync(null, "hplCompiler", null);
                await xw.WriteAttributeStringAsync(null, "romid", null, RomId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await xw.WriteStartElementAsync(null, "reverse", null);
                if (NeedsReverseCallback)
                    await xw.WriteAttributeStringAsync(null, "callback", null, "true");
                else
                {
                    foreach(var e in _ReverseTable)
                    {
                        await WriteDef(xw, e);
                    }
                }
                await xw.WriteEndElementAsync();
                foreach(var x in _AuxReverseTables)
                {
                    await xw.WriteStartElementAsync(null, "aux-rev", null);
                    await xw.WriteAttributeStringAsync(null, "name", null, x.Key);
                    foreach(var e in x.Value)
                    {
                        await WriteDef(xw, e);
                    }
                    await xw.WriteEndElementAsync();
                }
                await xw.WriteStartElementAsync(null, "mnemonics", null);
                if (NeedsCompileCallback)
                    await xw.WriteAttributeStringAsync(null, "callback", null, "true");
                else
                {
                    foreach(var e in _Mnemonics)
                    {
                        await WriteDef(xw, e);
                    }
                }
                await xw.WriteEndElementAsync();
                foreach(var x in _AuxCompileTables)
                {
                    await xw.WriteStartElementAsync(null, "aux-mnem", null);
                    await xw.WriteAttributeStringAsync(null, "name", null, x.Key);
                    foreach(var e in x.Value)
                    {
                        await WriteDef(xw, e);
                    }
                    await xw.WriteEndElementAsync();
                }
                await xw.WriteEndElementAsync();
            }
        }

        private async Task WriteDef(XmlWriter xw, MnemonicDefinition e)
        {
            await xw.WriteStartElementAsync(null, "def", null);
            await xw.WriteAttributeStringAsync(null, "class", null, e.Class.ToString());
            await xw.WriteAttributeStringAsync(null, "name", null, e.Name);
            await xw.WriteAttributeStringAsync(null, "token", null, e.Token.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await xw.WriteAttributeStringAsync(null, "code", null, e.Code.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await xw.WriteEndElementAsync();
        }

        private async Task WriteDef(XmlWriter xw, ReverseTableEntry e)
        {
            await xw.WriteStartElementAsync(null, "def", null);
            await xw.WriteAttributeStringAsync(null, "class", null, e.Class.ToString());
            await xw.WriteAttributeStringAsync(null, "prio", null, e.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if(e.MnemonicASCII != 0)
            {
                await xw.WriteAttributeStringAsync(null, "char", null, e.MnemonicASCII.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await xw.WriteCommentAsync(e.MnemonicChar.ToString());
            }
            await xw.WriteEndElementAsync();
        }

        public async Task WriteTo(string filename)
        {
            using(var f = File.CreateText(filename))
            {
                await WriteTo(f);
            }
        }

        internal MnemonicDefinition? MnemonicByCode(int code)
        {
            if (NeedsCompileCallback)
                throw new InvalidOperationException("Compile needs a callback!");
            return FindCodeInTable(_Mnemonics, code);
        }

        private MnemonicDefinition? FindCodeInTable(MnemonicDefinition[] mnemonics, int code)
        {
            foreach(var x in mnemonics)
                if (x.Code == code)
                    return x;
            return null;
        }

        public abstract class CallbackContext
        {
            private Func<byte?> _NextBye;
            protected HPLCompilerTables _Parent;

            internal CallbackContext(Func<byte?> nextByte, HPLCompilerTables parent)
            {
                _NextBye = nextByte;
                _Parent = parent;
            }
            /// <summary>
            /// Reads the next byte from the source compiled code buffer; will NOT rewind!
            /// </summary>
            /// <returns>The byte, or null if EOF</returns>
            public byte? GetNextByte()
            {
                return _NextBye();
            }
        }

        public class ReverseCallbackContext
            : CallbackContext
        {
            internal ReverseCallbackContext(Func<byte?> nextByte, HPLCompilerTables parent)
                : base(nextByte, parent)
            {
            }
            internal string? Ascii = null;
            internal ReverseTableEntry? Entry = null;

            public void SetResultFromAux(string tableName, int byteCode, string? ascii = null)
            {
                if (!_Parent._AuxReverseTables.TryGetValue(tableName, out var tab))
                    throw new InvalidOperationException($"Callback asked for aux. reverse table '{tableName}' which wasn't found!");
                if (byteCode <0 || byteCode > tab.Length)
                    throw new InvalidOperationException($"Callback asked for aux. reverse table '{tableName}' with byte code {byteCode} that was out of range. Maximum = {tab.Length}!");
                Entry = tab[byteCode];
                Ascii = ascii;
            }

            internal void SetResultEntry(MnemonicClass mnemonicClass, byte priority = 0, string? ascii = null)
            {
                Entry = new ReverseTableEntry() { Class = mnemonicClass, Priority = priority };
                Ascii = ascii;
            }
        }
        public class CompileCallbackContext
            : CallbackContext
        {
            internal CompileCallbackContext(Func<byte?> nextByte, HPLCompilerTables parent)
                : base(nextByte, parent)
            {
            }
           
            internal MnemonicDefinition? Definition;

            public void SetFromAuxilaryTable(string tableName, byte opCode)
            {
                if (!_Parent._AuxCompileTables.TryGetValue(tableName, out var tab))
                    throw new InvalidOperationException($"Callback asked for aux. compile table '{tableName}' which wasn't found!");
                foreach(var x in tab)
                    if (x.Code == opCode)
                    {
                        Definition = x;
                        return;
                    }
                throw new InvalidOperationException($"Callback asked for aux. reverse table '{tableName}' with byte code {opCode} that was not found!");
            }

            public void SetEntry(byte opCode, string mnemonic, byte mnemonicClass = 0, byte token = 0)
            {
                Definition = new MnemonicDefinition() { Name = mnemonic, Class = mnemonicClass, Token = token,Code = opCode };
            }

        }

        public delegate void ReverseCallbackDelegate(int infoFromGuide, byte byteCode, ReverseCallbackContext ctx);
        public delegate void CompilerCallbackDelegate(int infoFromGuide, byte opCode, CompileCallbackContext ctx);

        public ReverseCallbackDelegate? ReverseCallback { get; set; }
        public CompilerCallbackDelegate? CompileCallback { get; set; }

        internal (ReverseTableEntry? Entry, string? Ascii) CallReverse(int infoFromGuide, byte byteCode, Func<byte?> getNextByte)
        {
            if (ReverseCallback == null)
                throw new InvalidOperationException($"Callback requested for reverse compile, but no callback method defined!");
            var ctx = new ReverseCallbackContext(getNextByte, this);
            ReverseCallback(infoFromGuide, byteCode, ctx);
            if(ctx.Entry == null && ctx.Ascii == null)
                throw new InvalidOperationException($"The reverse compile callback for {infoFromGuide} byte code {byteCode} returned no result!");
            return (ctx.Entry, ctx.Ascii);
        }

        private Dictionary<string, ReverseTableEntry[]> _AuxReverseTables = new Dictionary<string, ReverseTableEntry[]>();
        private Dictionary<string, MnemonicDefinition[]> _AuxCompileTables = new Dictionary<string, MnemonicDefinition[]>();

        public void SetAuxilaryReverseCompileTable(Memory mem, string name, int address, int count)
        {
            List<ReverseTableEntry> temp = new List<ReverseTableEntry>();
            ParseReverseCompileTable(mem, address, count, temp);
            _AuxReverseTables[name] = temp.ToArray();
        }

        public void SetAuxilaryCompileTable(Memory mem, string name, int address, int? fixup = null)
        {
            List<MnemonicDefinition> temp = new List<MnemonicDefinition>();
            ParseCompileTable(mem, address, temp, fixup);
            _AuxCompileTables[name] = temp.ToArray();
        }

        internal MnemonicDefinition? CallCompile(int infoFromGuide, byte byteCode, Func<byte?> getNextByte)
        {
            if (CompileCallback==null)
                throw new InvalidOperationException("Callback requested but no callback defined!");
            var ctx = new CompileCallbackContext(getNextByte, this);
            CompileCallback(infoFromGuide, byteCode, ctx);
            if(ctx.Definition == null)
                throw new InvalidOperationException($"The compile callback for {infoFromGuide} byte code {byteCode} returned no result!");
            return ctx.Definition;
        }
    }
}