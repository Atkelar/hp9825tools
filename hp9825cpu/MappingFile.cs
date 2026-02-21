using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HP9825CPU
{
    public class MappingFile
    {
        private MappingFile(string title, bool is16Bit)
        {
            Title = title;
            Is16Bit = is16Bit;
        }

        public string Title { get; set; }

        public bool Is16Bit { get; private set; }

        public static MappingFile Create(bool use16Bit, string title, int offset, int length)
        {
            var map = new MappingFile(title, use16Bit);
            map._Sections.Add(new MappingSection(offset, offset + length - 1, MappingRegionType.Code, "Initial Code Section", map));
            return map;
        }

        private class ParseContext
        {
            //string Filename
            public int LineNumber { get; set; }
            public Action<string>? WarningTo { get; set; }

            public bool WarningsAsErrors { get; set; }

            public Exception Error(MappingFileErrorCode code, string message)
            {
                return new MappingFileFormatError(code, LineNumber, message);
            }

            internal void Warn(MappingFileErrorCode code, string message)
            {
                if (WarningsAsErrors)
                    throw Error(code, message);
                WarningTo?.Invoke($"{LineNumber} MF{(int)code:000} - {message}");
            }
        }

        public IEnumerable<string> GlobalComments => _GlobalComments;

        public int CommentPadding { get; set; } = 0;
        public int StarLineCounter { get; set; } = 0;   // none...

        private List<string> _GlobalComments = new List<string>();

        public static async Task<MappingFile> ReadFrom(StreamReader input, Action<string>? warningsTo = null)
        {
            MappingFile? instance = null;
            string? line;
            System.Text.StringBuilder? currentCommentBlock = null;
            MappingSection? currentSection = null;
            ParseContext ctx = new ParseContext() { LineNumber = 0, WarningTo = warningsTo };
            while ((line = await input.ReadLineAsync()) != null)
            {
                ctx.LineNumber++;
                if (string.IsNullOrWhiteSpace(line) && currentCommentBlock == null)
                    continue;
                var lTrim = line.Trim();
                if (currentCommentBlock != null)
                {
                    if (lTrim.Equals(".endcomment", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (currentSection == null)
                            instance!._GlobalComments.Add(currentCommentBlock.ToString());
                        currentCommentBlock = null;
                        continue;   // next line please...
                    }
                    else
                    {
                        if (currentSection != null)
                            currentSection.AppendBlockComment(line);
                        else
                            currentCommentBlock.AppendLine(line);
                        continue;
                    }
                }
                line = lTrim;
                if (line.StartsWith('#'))
                    continue;
                if (line.StartsWith(".mapping", StringComparison.InvariantCultureIgnoreCase))
                {
                    // mapping line found.
                    if (instance != null)
                        throw ctx.Error(MappingFileErrorCode.DuplicateIntro, "The .MAPPING line was already parsed!");
                    instance = ParseInstanceFromLine(ctx, line.Substring(8).Trim());
                }
                else
                {
                    // any other line needs a running instance already...
                    if (instance == null)
                        throw ctx.Error(MappingFileErrorCode.MissingIntro, "The .MAPPING line must be the first non-empty line in the mapping file!");
                    if (line.Equals(".comment", StringComparison.InvariantCultureIgnoreCase))
                    {
                        currentCommentBlock = new System.Text.StringBuilder();
                        continue;
                    }
                    if (line.StartsWith(".setting", StringComparison.InvariantCultureIgnoreCase))
                    {
                        instance.ParseSetting(ctx, line.Substring(8).Trim());
                        continue;
                    }
                    if (line.StartsWith(".section", StringComparison.InvariantCultureIgnoreCase))
                    {
                        currentSection = instance.ParseSection(ctx, line.Substring(8).Trim());
                        continue;
                    }
                    if (line.StartsWith(".subsection", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (currentSection == null)
                            throw ctx.Error(MappingFileErrorCode.MissingSection, "Subsections are only allowed within sections!");
                        currentSection.ParseSubsection(ctx, line.Substring(12).Trim());
                        continue;
                    }

                    if (line.StartsWith("**"))
                    {
                        // multi-line empty comment line
                        int numLines = 2;
                        if (line.Length > 2)
                        {
                            numLines = ParseNumber(ctx, line.Substring(2));
                        }
                        if (currentSection != null)
                        {
                            for (int i = 0; i < numLines; i++)
                                currentSection.AppendBlockComment(string.Empty);
                        }
                        else
                        {
                            currentCommentBlock = new System.Text.StringBuilder();
                            for (int i = 0; i < numLines; i++)
                                currentCommentBlock.AppendLine();
                            instance._GlobalComments.Add(currentCommentBlock.ToString());
                            currentCommentBlock = null;
                        }
                        continue;
                    }
                    if (line.StartsWith('*'))
                    {
                        if (currentSection != null)
                            currentSection.AppendBlockComment(line.Substring(1));
                        else
                            instance._GlobalComments.Add(line.Substring(1));
                        continue;
                    }
                    // down here, we have "a symbol line"...
                    //  @ADDR [normal]  @-prefix in the line updates the current location to an explicit one.
                    int? explicitLocation = null;
                    if (line.StartsWith('@'))
                    {
                        var addr = NextWord(ref line);
                        if (addr == null)
                            throw new InvalidOperationException("??");
                        explicitLocation = instance.ParseAddress(ctx, addr.Substring(1));
                    }
                    // after that, we have a "main label" for this location, or just stuffing if "-"
                    string? label = NextWord(ref line);
                    if (line.Length == 0 && label == null)
                        label = "-";
                    if (label == null)
                        throw ctx.Error(MappingFileErrorCode.MissingLabel, "Label name is missing in definition!");
                    label = label.ToUpperInvariant();
                    if (label == "-") // TODO: is "-" a valid label? if so, use something else...
                        label = null;
                    if (label != null && CpuConstants.RegisterNames.Contains(label))
                        ctx.Error(MappingFileErrorCode.LabelIsReservedName, $"The label {label} is a reseved name!");
                    if (label != null && !Assembler.ValidLabel.IsMatch(label))
                        ctx.Error(MappingFileErrorCode.LabelIsInvalid, $"The label {label} does not match the assembler requirements!");

                    var type = NextWord(ref line);
                    if (type == null)
                    {
                        // just the label...
                        if (label == null)
                        {
                            if (explicitLocation.HasValue && currentSection != null)
                                currentSection._CurrentAddress = explicitLocation.Value;
                            else
                            {
                                if (currentSection != null && currentSection.HasOpenBlockComment)
                                {
                                    currentSection.AppendLabel(explicitLocation, label, null, 1, null, null, null, null);
                                }
                                else
                                {
                                    ctx.Warn(MappingFileErrorCode.LabelIsInvalid, "No label and no type?! Incrementing address.");
                                    if (currentSection != null)
                                        currentSection._CurrentAddress++;
                                }
                            }
                            continue;
                        }
                        else
                        {
                            // only the label...
                            if (currentSection == null)
                                throw ctx.Error(MappingFileErrorCode.MissingSection, "Label outside section is invalid!");
                            currentSection.AppendLabel(explicitLocation, label, null);
                            continue;
                        }
                    }
                    else
                    {
                        // type. "*" is default...
                        LabelSectionType? labelType;
                        int length = 1;
                        string? dataTypeKey = null;
                        string? lenStr;
                        int? relativeAddress = null;
                        string? inlineComment = null;
                        switch (type.ToLowerInvariant())
                        {
                            case "*":
                                labelType = null;
                                break;
                            case "code":
                                labelType = LabelSectionType.Code;
                                break;
                            case "dec":
                            case "oct":
                                labelType = LabelSectionType.Data;
                                dataTypeKey = type;
                                if (line.Length > 0 && char.IsAsciiDigit(line[0]))
                                {
                                    lenStr = NextWord(ref line);
                                    if (lenStr == null) throw new InvalidOperationException("??!");
                                    length = ParseNumber(ctx, lenStr);
                                    if (length < 1 || length > 0xFFFF)
                                        throw ctx.Error(MappingFileErrorCode.LabelIsInvalid, $"The length for {type} is invalid: {length}!");
                                }
                                break;
                            case "bss":
                            case "asc":
                                // need a length next
                                labelType = LabelSectionType.Data;
                                dataTypeKey = type;
                                lenStr = NextWord(ref line);
                                if (lenStr == null)
                                    throw ctx.Error(MappingFileErrorCode.LabelIsInvalid, $"Missing length for {type}!");
                                length = ParseNumber(ctx, lenStr);
                                if (length < 1 || length > 0xFFFF)
                                    throw ctx.Error(MappingFileErrorCode.LabelIsInvalid, $"The length for {type} is invalid: {length}!");
                                break;
                            case "def":
                                labelType = LabelSectionType.Data;
                                dataTypeKey = type;
                                // defines something... as data. Interpret as "search for label".
                                if (line.StartsWith('-') || line.StartsWith('+'))
                                {
                                    // we want relative labelling...
                                    var numStr = NextWord(ref line) ?? throw new InvalidOperationException();
                                    relativeAddress = ParseNumber(ctx, numStr);
                                }
                                break;
                            default:
                                throw ctx.Error(MappingFileErrorCode.MissingSection, $"The label type {type} is undefined!");
                        }
                        string[]? aliases = null;
                        if (line.Length > 0)
                        {
                            if (line.StartsWith('('))
                            {
                                int idx = line.IndexOf(')');
                                if (idx < 0)
                                    throw ctx.Error(MappingFileErrorCode.MissingSection, $"The label {label} is invalid: missing ')' for alias list!");
                                aliases = line.Substring(1, idx - 1).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                line = line.Substring(idx + 1).Trim();
                            }
                            if (line.StartsWith('['))    // long-alias...
                            {
                                throw ctx.Error(MappingFileErrorCode.LabelIsInvalid, $"Long-name labels currently not supported!");
                            }
                            if (line.Length > 0)
                                inlineComment = line;
                        }
                        if (currentSection == null)
                            throw ctx.Error(MappingFileErrorCode.MissingSection, $"The label {label} is missing a parent section!");
                        if (label != null && instance.KnowsLabel(label))
                            ctx.Warn(MappingFileErrorCode.DuplicateLabel, $"The label {label} is declared multiple times! Unpredictable results may occur!");
                        var def = instance.DefinitionForRange(explicitLocation.GetValueOrDefault(currentSection._CurrentAddress), length);
                        if (def != null)
                            ctx.Warn(MappingFileErrorCode.LabelOverlapping, $"The label {label} deinfed {instance.FormatAddress(explicitLocation.GetValueOrDefault(currentSection._CurrentAddress))} overlaps with {def.Label} at {instance.FormatAddress(def.Location)}-{instance.FormatAddress(def.EndLocation)}!");
                        
                        currentSection.AppendLabel(explicitLocation, label, labelType, length, dataTypeKey, relativeAddress, aliases, inlineComment);
                        continue;
                    }
                    //ctx.Warn(MappingFileErrorCode.UnknownDirective, $"Directive unknown, ignoring line: {line}");
                }
            }
            if (instance == null)
                throw ctx.Error(MappingFileErrorCode.MissingIntro, "The .MAPPING line is missing!");
            return instance;
        }

        private LabelDefinition? DefinitionForRange(int address, int length)
        {
            int lookTo = address + length - 1;
            foreach(var s in _Sections)
            {
                if (s.From <= lookTo && s.To >= address)
                {
                    var def = s.GetOverlappingLabelDefinition(address, length);
                    if (def != null)
                        return def;
                }
            }
            return null;
        }

        private static int ParseNumber(ParseContext ctx, string value)
        {
            int result;
            if (value.EndsWith("b", StringComparison.InvariantCultureIgnoreCase))
            {
                // octal string...
                try
                {
                    result = Convert.ToInt32(value.Substring(0, value.Length - 1), 8);
                }
                catch (Exception)
                {
                    throw ctx.Error(MappingFileErrorCode.InvalidNumeral, $"Octal value invalid: {value}");
                }
            }
            else
            {
                if (value.EndsWith("h", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        return Convert.ToInt32(value.Substring(0, value.Length - 1), 16);
                    }
                    catch (Exception)
                    {
                        throw ctx.Error(MappingFileErrorCode.InvalidNumeral, $"Hex value invalid: {value}");
                    }
                }
                else
                {
                    if (!int.TryParse(value, out result))
                        throw ctx.Error(MappingFileErrorCode.InvalidNumeral, $"Decimal value invalid: {value}");
                }
            }
            return result;
        }

        private MappingSection ParseSection(ParseContext ctx, string line)
        {
            MappingSection section;

            // .SECTION 40B-27777B CODE Main System ROM

            string? title = null;
            int from;
            int to;
            MappingRegionType type = MappingRegionType.Data;

            string? word = NextWord(ref line);
            if (word == null)
                throw ctx.Error(MappingFileErrorCode.SyntaxError, "Section directive is missing address!");
            ParseAddressRange(ctx, word, out from, out to);

            word = NextWord(ref line);
            if (word != null)
            {
                switch (word.ToLowerInvariant())
                {
                    case "code":
                        type = MappingRegionType.Code;
                        break;
                    case "data":
                        type = MappingRegionType.Data;
                        break;
                    case "ignore":
                        type = MappingRegionType.Ignore;
                        break;
                    default:
                        throw ctx.Error(MappingFileErrorCode.InvalidOption, $"The section type '{word}' is undefined!");
                }
                if (line.Length > 0)
                    title = line;
            }

            section = new MappingSection(from, to, type, title, this);

            _Sections.Add(section);
            return section;
        }

        private void ParseAddressRange(ParseContext ctx, string word, out int from, out int to)
        {
            var p = word.Split('-');
            if (p.Length != 2)
                throw ctx.Error(MappingFileErrorCode.SyntaxError, "Address range is not FROM-TO!");
            from = ParseAddress(ctx, p[0]);
            to = ParseAddress(ctx, p[1]);
        }

        private int ParseAddress(ParseContext ctx, string value)
        {

            int result = ParseNumber(ctx, value);
            if (result < 0 || result > (Is16Bit ? 0xFFFF : 0x7FFF))
                throw ctx.Error(MappingFileErrorCode.AddressOutOfRange, $"Address is out of range for the selected bit-width: {value} parsed to {result}.");
            return result;
        }

        private static string? NextWord(ref string line)
        {
            line = line.TrimStart();
            if (line.Length == 0)
            {
                line = string.Empty;
                return null;
            }
            int idx = line.IndexOf(' ');
            string r;
            if (idx < 0)
            {
                r = line;
                line = string.Empty;
                return r;
            }
            r = line.Substring(0, idx);
            line = line.Substring(idx + 1).TrimStart();
            return r;
        }

        private List<MappingSection> _Sections = new List<MappingSection>();

        NumberFormatType NumberBase = NumberFormatType.Octal;

        private async Task WriteSettings(TextWriter target, bool includeDefault = false)
        {
            await WriteSettingIf(CommentPadding != 0 || includeDefault, target, "CPAD", CommentPadding);
            await WriteSettingIf(NumberBase != NumberFormatType.Octal || includeDefault, target, "BASE",
                NumberBase switch { NumberFormatType.Octal => "OCT", NumberFormatType.Hex => "HEX", NumberFormatType.Decimal => "DEC", NumberFormatType.Binary => "BIN", _ => throw new NotImplementedException() }
            );
            await WriteSettingIf(StarLineCounter > 0 || includeDefault, target, "STARCOUNT", StarLineCounter);
        }

        private async Task WriteSettingIf(bool condition, TextWriter target, string name, object value)
        {
            if (condition)
                await target.WriteLineAsync(string.Format(".SETTING {0} {1}", name, value));
        }

        private void ParseSetting(ParseContext ctx, string line)
        {
            int num;
            int idx = line.IndexOf(' ');
            if (idx < 0)
            {
                switch (line.ToLowerInvariant())
                {
                    default:
                        throw ctx.Error(MappingFileErrorCode.InvalidOption, $"Unrecognized setting {line}!");
                }
            }
            else
            {
                string name = line.Substring(0, idx);
                string value = line.Substring(idx + 1).Trim();
                switch (name.ToLowerInvariant())
                {
                    case "starcount":
                        num = ParseNumber(ctx, value);
                        if (num < 0 || num > 40)
                            throw ctx.Error(MappingFileErrorCode.ValueOutOfRange, $"The 'star line counter' {num} is out of range. 0-40 only!");
                        StarLineCounter = num;
                        break;
                    case "cpad":
                        num = ParseNumber(ctx, value);
                        if (num < 0 || num > 5)
                            throw ctx.Error(MappingFileErrorCode.ValueOutOfRange, $"The comment padding {num} is out of range. 0-5 only!");
                        CommentPadding = num;
                        break;
                    case "base":
                        switch (value.ToLowerInvariant())
                        {
                            case "oct":
                                NumberBase = NumberFormatType.Octal;
                                break;
                            case "dec":
                                NumberBase = NumberFormatType.Decimal;
                                break;
                            case "hex":
                                NumberBase = NumberFormatType.Hex;
                                break;
                            case "bin":
                                NumberBase = NumberFormatType.Binary;
                                break;
                            default:
                                throw ctx.Error(MappingFileErrorCode.InvalidOptionValue, $"Number format {value} is undefined!");
                        }
                        break;
                    default:
                        throw ctx.Error(MappingFileErrorCode.InvalidOption, $"Unrecognized setting {name} with value {value}!");
                }
            }
        }

        private static MappingFile? ParseInstanceFromLine(ParseContext ctx, string line)
        {
            if (line.StartsWith('('))
            {
                int idx = line.IndexOf(')');
                if (idx < 0)
                    throw ctx.Error(MappingFileErrorCode.SyntaxError, ".MAPPING expects (xx) title syntax, missing ')'.");
                var opts = line.Substring(1, idx - 1).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                bool? is16Bit = null;
                foreach (var o in opts)
                {
                    switch (o)
                    {
                        case "16":
                            if (is16Bit.HasValue)
                                throw ctx.Error(MappingFileErrorCode.InvalidOption, "Mapping file option 15/16 overdefined! Only one allowed!");
                            is16Bit = true;
                            break;
                        case "15":
                            if (is16Bit.HasValue)
                                throw ctx.Error(MappingFileErrorCode.InvalidOption, "Mapping file option 15/16 overdefined! Only one allowed!");
                            is16Bit = false;
                            break;
                        default:
                            throw ctx.Error(MappingFileErrorCode.InvalidOption, $"Option {o} not known for mapping file!");
                    }
                }
                return new MappingFile(line.Substring(idx + 1).Trim(), is16Bit.GetValueOrDefault());
            }
            else
            {
                return new MappingFile(line, false);
            }
        }

        public bool ProcessMemoryPhase1(Memory memory)
        {
            return ProcessMemory(memory, null, true);
        }

        public bool ProcessMemoryPhase2(Memory memory, ListingPrinter printer)
        {
            return ProcessMemory(memory, printer, false);
        }

        private string? _StarLine;
        private string StarLine => _StarLine != null && _StarLine.Length == StarLineCounter ? _StarLine : new string('*', StarLineCounter);

        private bool ProcessMemory(Memory memory, ListingPrinter? printer, bool creaetMissingLabels)
        {
            // we go section by section... and create matching headings in the printer, including "org"s.
            AssemblyLine? line = null;
            int[] numBuf = new int[8];
            foreach (var s in _Sections)
            {
                if (s.Type != MappingRegionType.Ignore)
                {
                    int address = s.From;
                    if (printer != null)
                    {
                        if (!string.IsNullOrWhiteSpace(s.Title))
                        {
                            line = AssemblyLine.FromHeader(SourceLineRef.Unknown, s.Title);
                            line.CreateOutput(printer);
                        }
                        line = AssemblyLine.FromOrg(SourceLineRef.Unknown, address, printer.GetFormattedAddress(address, true));
                        line.CreateOutput(printer);
                        line = null;
                    }
                    address = s.From;
                    while (address <= s.To)
                    {
                        var subSection = s.SubSectionFor(address);
                        if (printer != null && subSection != null && subSection.From == address)
                        {
                            // yes, starts here!
                            if (!string.IsNullOrWhiteSpace(subSection.Title))
                            {
                                line = AssemblyLine.FromHeader(SourceLineRef.Unknown, subSection.Title);
                                line.CreateOutput(printer);
                                line = null;
                            }
                        }
                        var def = s.GetLabelDefinition(address);

                        string? label = null;
                        string? comment = null;
                        int value = memory[address];

                        if (def != null && def.Location == address && printer != null)    // exact match...
                        {
                            label = def.Label;
                            comment = def.InlineComment;
                            if (def.BlockComments != null && def.BlockComments.Length > 0)  // prepend the intro-comment, including padding.
                            {
                                if (def.BlockComments.Any(x => x.Length > 0))
                                    AddPaddingComments(printer, CommentPadding);
                                foreach (var bl in def.BlockComments)
                                {
                                    if (bl.StartsWith('*') && bl.Trim('*').Length == 0)
                                    {
                                        line = AssemblyLine.FromComment(SourceLineRef.Unknown, null, StarLine);
                                    }
                                    else
                                    {
                                        line = AssemblyLine.FromComment(SourceLineRef.Unknown, null, bl);
                                    }
                                    line.CreateOutput(printer);
                                }
                                if (def.BlockComments.Any(x => x.Length > 0))
                                    AddPaddingComments(printer, CommentPadding);
                            }
                        }
                        // fuzzy match... output best effort.
                        if (def == null || def.Location != address)
                        {
                            switch (s.GetSectionType(address))
                            {
                                case MappingRegionType.Code:
                                    line = Disassembler.Disassemble(
                                        memory[address], address, label, comment, true,
                                        (a, b) => s.FindBestLabelFor(a, b, creaetMissingLabels, true, !creaetMissingLabels));
                                    if (printer == null) line = null;
                                    address++;
                                    break;
                                case MappingRegionType.Data:
                                    if (printer != null) line = AssemblyLine.FromConstant(SourceLineRef.Unknown, address, "OCT", 8, new int[] { value }, null, null);
                                    address++;
                                    break;
                                case MappingRegionType.Ignore:
                                    if (printer != null) line = AssemblyLine.FromBSS(SourceLineRef.Unknown, address, 1, "1", label, comment);
                                    address++;
                                    break;
                                default:
                                    throw new NotImplementedException();
                            }
                            if (printer != null)
                                line?.CreateOutput(printer);
                        }
                        else
                        {
                            label = def.Label;
                            comment = def.InlineComment;
                            switch (def.Type)
                            {
                                case LabelSectionType.Code:
                                    line = Disassembler.Disassemble(memory[address], address, label, comment, true,
                                       (a, b) => s.FindBestLabelFor(a, b, creaetMissingLabels, true, !creaetMissingLabels));
                                    if (printer == null) line = null;
                                    address++;
                                    break;
                                case LabelSectionType.Data:
                                    switch ((def.DataTypeKey ?? "dec").ToLowerInvariant())
                                    {
                                        case "def":
                                            bool indirect = Is16Bit ? false : ((value & 0x8000) != 0);
                                            if (indirect) value &= 0x7FFF;
                                            //&& 
                                            string? labelFor;
                                            labelFor = s.FindBestLabelFor(address, value - def.RelativeAddress.GetValueOrDefault(), creaetMissingLabels, false, !creaetMissingLabels);
                                            if (printer != null)
                                            {
                                                if (labelFor == null)
                                                    labelFor = printer.GetFormattedAddress(value - def.RelativeAddress.GetValueOrDefault(), true);
                                                if (labelFor == def.Label)
                                                    // uh-oh... shouldn't be. Use 
                                                    labelFor = "*"; // instead.
                                                if (def.RelativeAddress.HasValue)
                                                    labelFor = RelativeAddress(labelFor, def.RelativeAddress.Value);
                                                // TODO: value to output...
                                                line = AssemblyLine.FromDef(SourceLineRef.Unknown, address, null, labelFor, indirect, label, comment);
                                            }
                                            address++;
                                            break;
                                        case "asc":
                                            if (printer != null) 
                                                line = AssemblyLine.FromAscii(SourceLineRef.Unknown, address, ExtractAscii(memory, address, def.Length), def.Length.ToString(), label, comment);
                                            address += def.Length;
                                            break;
                                        case "dec":
                                        case "oct":
                                            int num = def.Length;
                                            while (num > 0)
                                            {
                                                int lLen = Math.Min(num, 8);
                                                for (int i = 0; i < lLen; i++)
                                                    numBuf[i] = memory[address + i];
                                                if (printer != null) 
                                                {
                                                    def.DataTypeKey ??= "DEC";
                                                    line = AssemblyLine.FromConstant(
                                                        SourceLineRef.Unknown, 
                                                        address, 
                                                        def.DataTypeKey.ToUpperInvariant(), 
                                                        def.DataTypeKey.Equals("dec", StringComparison.InvariantCultureIgnoreCase) ? 10 : 8,
                                                        lLen == 8 ? numBuf : numBuf.Take(lLen), 
                                                        label, 
                                                        comment);
                                                }
                                                address += lLen;
                                                label = null;
                                                comment = null;
                                                num -= lLen;
                                                if (num > 0)
                                                {
                                                    if (printer != null) line?.CreateOutput(printer);
                                                }
                                            }
                                            break;
                                        case "bss":
                                            if (printer != null) line = AssemblyLine.FromBSS(SourceLineRef.Unknown, address, def.Length, def.Length.ToString(), label, comment);
                                            address += def.Length;
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }
                                    break;
                                case LabelSectionType.BSS:
                                    if (printer != null) line = AssemblyLine.FromBSS(SourceLineRef.Unknown, address, def.Length, def.Length.ToString(), label, comment);
                                    address += def.Length;
                                    break;
                                default:
                                    //address++;
                                    throw new NotImplementedException();
                                    break;
                            }
                            if (printer != null) line?.CreateOutput(printer);
                            if (printer != null && def.Label != null && def.Aliases != null && def.Aliases.Length > 0)
                            {
                                // create EQU lines...
                                foreach (var alias in def.Aliases)
                                {
                                    line = AssemblyLine.FromEqu(SourceLineRef.Unknown, alias, def.Label, null, null, null);
                                    line.CreateOutput(printer);
                                }
                            }
                        }
                    }
                }
            }
            return true;
        }

        private static string? RelativeAddress(string? baseName, int offset)
        {
            if (string.IsNullOrWhiteSpace(baseName))
                return null;
            return baseName + (offset < 0 ? "-" : "+") + Math.Abs(offset).ToString();
        }

        private void AddPaddingComments(ListingPrinter? printer, int numLines)
        {
            if (numLines > 0 && printer != null)
            {
                var line = AssemblyLine.FromComment(SourceLineRef.Unknown, null, string.Empty);
                while (numLines > 0)
                {
                    line.CreateOutput(printer);
                    numLines--;
                }
            }
        }

        private string ExtractAscii(Memory memory, int address, int length)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                sb.Append((char)((memory[address + i] >> 8) & 0xFF));
                sb.Append((char)((memory[address + i]) & 0xFF));
            }
            return sb.ToString();
        }

        private class MappingSection
        {
            public int From { get; set; }
            public int To { get; set; }
            public MappingRegionType Type { get; }
            public bool HasOpenBlockComment => (_CommentCollector?.Count ?? 0)>0;

            public string? Title;

            public MappingSection(int from, int to, MappingRegionType type, string? title, MappingFile parent)
            {
                From = from;
                To = to;
                Type = type;
                Title = title;
                _CurrentAddress = from;
                _Parent = parent;
            }

            public string? FindBestLabelForNarrow(int address, int target, bool createIfNotFound, bool exactOnly, bool recordReference)
            {
                // first step: look in our section, then look in all other sections in the parent...
                foreach (var item in _Definitions)
                {
                    if (item.Location == target)    // exact match is always the one to go for!
                    {
                        if (recordReference) item.AddReferenceFrom(address, target);
                        return item.Label != null ? (item.Location == target ? item.Label : RelativeAddress(item.Label, target - item.Location)) : string.Empty;  // TODO: source address based aliases?
                    }
                }
                // label was not found as an exact match; see if we have a "range?" it falls into
                if (exactOnly)
                    return null;
                foreach (var item in _Definitions)   // try again, fuzzy...
                {
                    if (item.Location < target && item.Location + item.Length > target)
                    {
                        if (recordReference) item.AddReferenceFrom(address, target);
                        return item.Label != null ? RelativeAddress(item.Label, target - item.Location) : string.Empty;  // TODO: source address based aliases?
                    }
                }
                return null;
            }

            public string? FindBestLabelFor(int address, int target, bool createIfNotFound, bool exactOnly, bool recordReference)
            {
                bool hadUnlabelledResult = false;
                // TODO: look up lable for address, relative to target. Add cross-reference.
                var result = FindBestLabelForNarrow(address, target, createIfNotFound, true, recordReference);
                if (result != null)  // real result...
                {
                    if (result == string.Empty)
                    {
                        hadUnlabelledResult = true;
                    }
                    else
                        return result;
                }
                // not found here... continue...
                foreach (var s in _Parent._Sections)
                {
                    if (s != this)
                    {
                        // not again!
                        result = s.FindBestLabelForNarrow(address, target, createIfNotFound, true, recordReference);
                        if (result != null)  // real result...
                        {
                            if (result == string.Empty)
                            {
                                hadUnlabelledResult = true;
                            }
                            else
                                return result;
                        }
                    }
                }
                if (exactOnly && !createIfNotFound)
                    return null;
                if (!exactOnly)
                {
                    result = FindBestLabelForNarrow(address, target, createIfNotFound, false, recordReference);
                    if (result != null)  // real result...
                    {
                        if (result == string.Empty)
                        {
                            hadUnlabelledResult = true;
                        }
                        else
                            return result;
                    }
                    foreach (var s in _Parent._Sections)
                    {
                        if (s != this)
                        {
                            // not again!
                            result = s.FindBestLabelForNarrow(address, target, createIfNotFound, false, recordReference);
                            if (result != null)  // real result...
                            {
                                if (result == string.Empty)
                                {
                                    hadUnlabelledResult = true;
                                }
                                else
                                    return result;
                            }
                        }
                    }
                }

                // fuzzy search was also not successful or not requested; create a new label if so...
                if (createIfNotFound && !hadUnlabelledResult)   // don't create lable if we had *anything* at all
                {
                    // we only create new labels in a section that matches!
                    if (target >= From && target <= To)
                    {
                        // we are the one!
                        LabelDefinition newDef = CreateNewLabel(target, string.Format("First Ref from {0}", _Parent.FormatAddress(address)));
                        if (recordReference)
                            newDef.AddReferenceFrom(address, target);
                        return newDef.Label;
                    }
                    foreach (var s in _Parent._Sections)
                    {
                        if (s != this && target >= s.From && target <= s.To)
                        {
                            LabelDefinition newDef = s.CreateNewLabel(target, string.Format("First Ref from {0}", _Parent.FormatAddress(address)));
                            if (recordReference)
                                newDef.AddReferenceFrom(address, target);
                            return newDef.Label;
                        }
                    }
                }
                return null;    // nothing "real" found...
            }

            private int _LatestGeneratedLabelId = 0;

            private string MakeGeneratedLabelBasedOnAddress(int address)
            {
                return string.Format("!{0:X4}", address);
            }

            private string MakeGeneratedLabel(int tryNum)
            {
                return string.Format("?{0:X}", tryNum);
            }

            private LabelDefinition CreateNewLabel(int target, string? comment)
            {
                var tryThis = MakeGeneratedLabelBasedOnAddress(target);
                if (_Parent.KnowsLabel(tryThis))
                {
                    int tryNum = _LatestGeneratedLabelId;
                    do
                    {
                        tryNum++;
                        tryThis = MakeGeneratedLabel(tryNum);
                    } while (_Parent.KnowsLabel(tryThis));
                    _LatestGeneratedLabelId = tryNum;
                }
                // find type on subsection!

                LabelSectionType type = this.Type == MappingRegionType.Ignore ? LabelSectionType.BSS : (this.Type == MappingRegionType.Code ? LabelSectionType.Code : LabelSectionType.Data);
                var def = new LabelDefinition(tryThis, target, target, 1, null, type, type == LabelSectionType.Data ? "oct" : null, null, null, comment);
                _Definitions.Add(def);
                if (!_indexBaName.ContainsKey(tryThis))
                    _indexBaName.Add(tryThis, def);
                return def;
            }

            List<string>? _CommentCollector = null;

            internal int _CurrentAddress;
            private MappingFile _Parent;

            internal void AppendBlockComment(string line)
            {
                _CommentCollector ??= new List<string>();
                _CommentCollector.Add(line);
            }

            List<MappingSection>? _Subsections = null;

            MappingSection? _CurrentSubSection = null;

            internal void ParseSubsection(ParseContext ctx, string line)
            {
                var word = NextWord(ref line);
                string? title = null;
                MappingRegionType type = Type;
                int from = _CurrentAddress;
                // optional "@" 
                if (word != null && word.StartsWith('@'))
                {
                    from = _Parent.ParseAddress(ctx, word.Substring(1));
                    if (from < From)
                    {
                        ctx.Warn(MappingFileErrorCode.SubsectionAnormal, $"Subsection defined as starting at {from} but parent section starts at {From}!");
                    }
                    else
                    {
                        if (from < _CurrentAddress)
                            ctx.Warn(MappingFileErrorCode.SubsectionAnormal, $"Subsection defined as starting at {from} but parent section already is at {_CurrentAddress}!");
                    }
                    if (from > To)
                    {
                        ctx.Warn(MappingFileErrorCode.SubsectionAnormal, $"Subsection starts at {from} but parent section ends at {To}!");
                    }
                    word = NextWord(ref line);  // carry on.
                }
                if (word != null)
                {
                    switch (word)
                    {
                        case "*":
                            type = this.Type;
                            break;
                        case "DATA":
                            type = MappingRegionType.Data;
                            break;
                        case "CODE":
                            type = MappingRegionType.Code;
                            break;
                        case "IGNORE":
                            type = MappingRegionType.Ignore;
                            break;
                        default:
                            throw ctx.Error(MappingFileErrorCode.InvalidOption, $"Unknown subsection type: {word}");
                    }
                    if (line.Length > 0)
                        title = line;
                }
                // terminate last subsection, if any...
                _CurrentAddress = from;
                if (_CurrentSubSection != null)
                {
                    _CurrentSubSection.To = _CurrentAddress - 1;
                    if (_CurrentSubSection.To < _CurrentSubSection.From)
                        ctx.Warn(MappingFileErrorCode.SubsectionAnormal, $"Sub-section ended at {_CurrentSubSection.To} with new section, but started at {_CurrentSubSection.From}!");
                }
                _CurrentSubSection = new MappingSection(from, To, type, title, _Parent);
                _Subsections ??= new List<MappingSection>();
                _Subsections.Add(_CurrentSubSection);
            }

            List<LabelDefinition> _Definitions = new List<LabelDefinition>();

            internal void AppendLabel(
                int? explicitLocation,
                string? label,
                LabelSectionType? type,
                int length = 1,
                string? dataTypeKey = null,
                int? relativeAddress = null,
                string[]? aliases = null,
                string? inlineComment = null)
            {
                var loc = explicitLocation.GetValueOrDefault(_CurrentAddress);
                var st = type.GetValueOrDefault(Type == MappingRegionType.Code ? LabelSectionType.Code : (Type == MappingRegionType.Data ? LabelSectionType.Data : LabelSectionType.BSS));
                var x = new LabelDefinition(label, explicitLocation, loc, length, type, st, dataTypeKey, relativeAddress, aliases, inlineComment);
                x.BlockComments = _CommentCollector?.ToArray();
                _CommentCollector?.Clear();
                _CurrentAddress = loc + length;
                _Definitions.Add(x);
                if (label != null && !_indexBaName.ContainsKey(label))
                    _indexBaName.Add(label, x);
            }

            internal LabelDefinition? GetOverlappingLabelDefinition(int address, int length)
            {
                int lookTo = address + length - 1;
                foreach (var def in _Definitions)
                {
                    if (address <= def.EndLocation && lookTo >= def.Location)   // overlap!
                        return def;
                }
                return null;
            }

            internal LabelDefinition? GetLabelDefinition(int address)
            {
                LabelDefinition? alternative = null;
                foreach (var def in _Definitions)
                {
                    if (def.Location == address)
                        return def;
                    if (def.Location <= address && def.Location + def.Length > address)
                        alternative = def;
                }
                return alternative;
            }

            internal MappingRegionType GetSectionType(int address)
            {
                if (_Subsections != null)
                    foreach (var s in _Subsections)
                    {
                        if (s.From <= address && s.To >= address)
                            return s.Type;
                    }
                return this.Type;
            }

            private Dictionary<string, LabelDefinition> _indexBaName = new Dictionary<string, LabelDefinition>();

            internal bool KnowsLabel(string tryThis)
            {
                return _indexBaName.ContainsKey(tryThis);
            }

            internal MappingSection? SubSectionFor(int address)
            {
                if (_Subsections == null)
                    return null;
                foreach (var s in _Subsections)
                {
                    if (s.From <= address && s.To >= address)
                        return s;
                }
                return null;
            }

            internal async Task SaveTo(TextWriter target)
            {
                await target.WriteLineAsync();  // empty line for readability.
                await target.WriteAsync(string.Format(".SECTION {0}-{1}", _Parent.FormatAddress(From), _Parent.FormatAddress(To)));
                if (Type != MappingRegionType.Data || !string.IsNullOrWhiteSpace(Title))
                {
                    // type next...
                    await target.WriteAsync(string.Format(" {0}", Type switch { MappingRegionType.Data => "DATA", MappingRegionType.Code => "CODE", MappingRegionType.Ignore => "IGNORE", _ => throw new NotImplementedException() }));
                    if (!string.IsNullOrWhiteSpace(Title))
                        await target.WriteAsync(" " + Title);
                }
                await target.WriteLineAsync();
                await target.WriteLineAsync();  // empty line for readability.
                // write any labels from this section, in order...
                // save in "compact" format... avoid @ if "natural" address matches, start subsections when appropriate...
                int naturalNextAddress = From;
                List<MappingSection> pendingSubsections = _Subsections != null ? _Subsections.OrderBy(x => x.From).ToList() : new List<MappingSection>();
                foreach (var d in _Definitions.Where(x => x.Location + x.Length > From && x.Location <= To).OrderBy(x => x.Location))
                {
                    while (pendingSubsections.Count > 0 && pendingSubsections[0].From <= naturalNextAddress)
                    {
                        var p = pendingSubsections[0];
                        pendingSubsections.RemoveAt(0);
                        await WriteSubsectionHeader(target, p, naturalNextAddress != p.From);
                        naturalNextAddress = p.From;    // start in the new section...
                    }
                    await WriteDefinition(target, d, d.Location != naturalNextAddress || d.ExplicitLocation.HasValue, false);
                    naturalNextAddress = d.Location + d.Length;
                    d.Saved = true;
                }
                foreach (var p in pendingSubsections)
                {
                    await WriteSubsectionHeader(target, p, true);
                }
            }

            private async Task WriteDefinition(TextWriter target, LabelDefinition d, bool includeAddress, bool forceTypeOutput)
            {
                if (d.BlockComments != null && d.BlockComments.Length > 0)
                {
                    // TODO: detect and carry over .comment vs. *...
                    if (d.BlockComments.Count(x=>x.Length>0) > 3)
                    {
                        await target.WriteLineAsync(".COMMENT");
                        foreach (var l in d.BlockComments)
                            await target.WriteLineAsync(l);
                        await target.WriteLineAsync(".ENDCOMMENT");
                    }
                    else
                    {
                        int i = 0;
                        while (i < d.BlockComments.Length)
                        {
                            if (d.BlockComments[i].Length==0 && i+1 < d.BlockComments.Length && d.BlockComments[i+1].Length == 0)
                            {
                                // we have a collapsible line...
                                int end = i;
                                while (end < d.BlockComments.Length && d.BlockComments[end].Length==0)
                                    end++;
                                int count = end-i;
                                await target.WriteAsync("**");
                                if(count > 2)
                                    await target.WriteLineAsync(count.ToString());
                                else
                                    await target.WriteLineAsync();
                                i = end;
                            }
                            else
                            {
                                await target.WriteAsync('*');
                                await target.WriteLineAsync(d.BlockComments[i]);
                                i++;
                            }
                        }
                    }
                }
                if (includeAddress)
                {
                    await target.WriteAsync('@');
                    await target.WriteAsync(_Parent.FormatAddress(d.Location));
                    await target.WriteAsync(' ');
                }
                await target.WriteLineAsync(d.GetDefinitionLine(forceTypeOutput));
            }

            private async Task WriteSubsectionHeader(TextWriter target, MappingSection subSection, bool includeAddress)
            {
                await target.WriteLineAsync();
                await target.WriteAsync(".SUBSECTION");
                if (includeAddress)
                {
                    await target.WriteAsync(" @");
                    await target.WriteAsync(_Parent.FormatAddress(From));
                }
                if (subSection.Type != this.Type || !string.IsNullOrWhiteSpace(subSection.Title))
                {
                    await target.WriteAsync(' ');
                    if (subSection.Type == this.Type)
                        await target.WriteAsync("*");
                    else
                        await target.WriteAsync(subSection.Type switch { MappingRegionType.Data => "DATA", MappingRegionType.Code => "CODE", MappingRegionType.Ignore => "IGNORE", _ => throw new NotImplementedException() });
                    if (!string.IsNullOrWhiteSpace(subSection.Title))
                    {
                        await target.WriteAsync(" ");
                        await target.WriteAsync(subSection.Title);
                    }
                }
                await target.WriteLineAsync();
                await target.WriteLineAsync();
            }

            internal void ClearSavedMarkers()
            {
                foreach (var d in _Definitions)
                {
                    d.Saved = false;
                }
            }

            // return true if we had *any* label output.
            internal async Task<bool> WriteMissedLabels(TextWriter target, bool firstMissingLabel)
            {
                bool hadEntries = false;
                foreach (var d in _Definitions)
                {
                    if (!d.Saved)
                    {
                        if (firstMissingLabel)
                        {
                            await target.WriteLineAsync();
                            await target.WriteLineAsync("#");
                            await target.WriteLineAsync("#   The following labels were not saved, because they ended up ouside some sections...");
                            await target.WriteLineAsync("#");
                            await target.WriteLineAsync();
                            await target.WriteLineAsync(string.Format(".SECTION 0B-{0} DATA Catch missed label section.", _Parent.Is16Bit ? "177777B" : "77777B"));
                            await target.WriteLineAsync();
                            firstMissingLabel = false;
                        }
                        hadEntries = true;
                        // weird entry, write full info...
                        await WriteDefinition(target, d, true, true);
                    }
                }
                return hadEntries;
            }
        }

        private bool KnowsLabel(string tryThis)
        {
            foreach (var s in _Sections)
                if (s.KnowsLabel(tryThis))
                    return true;
            return false;
        }

        private string FormatAddress(int target, bool includeTypeCharacter = true)
        {
            if (target < 0 || target > 0xFFFF)
                throw new ArgumentOutOfRangeException(nameof(target), target, "Invalid address!");
            bool assume16 = Is16Bit || target > 0x7FFF;     // shouldn't happen, but we won't trimm or complain here...
            switch (NumberBase)
            {
                case NumberFormatType.Octal:
                    var temp = ("000000" + Convert.ToString(target, 8));
                    return temp.Substring(temp.Length - (assume16 ? 6 : 5)) + (includeTypeCharacter ? "B" : string.Empty);
                case NumberFormatType.Hex:
                    return target.ToString("x4") + (includeTypeCharacter ? "H" : string.Empty);
                case NumberFormatType.Decimal:
                    return target.ToString("00000");
                case NumberFormatType.Binary:
                    temp = ("0000000000000000" + Convert.ToString(target, 2));
                    return temp.Substring(temp.Length - (assume16 ? 16 : 15)) + (includeTypeCharacter ? "N" : string.Empty);
            }
            throw new NotImplementedException();
        }

        public async Task SaveTo(TextWriter target)
        {
            await WriteIntro(target);
            foreach (var cb in _GlobalComments)
            {
                await WriteCommentBlock(target, cb);
            }
            await WriteSettings(target, true);
            foreach (var s in _Sections)
            {
                s.ClearSavedMarkers();
                await s.SaveTo(target);
            }
            bool firstMissingLabel = true;
            foreach (var s in _Sections)
            {
                firstMissingLabel = !await s.WriteMissedLabels(target, firstMissingLabel);
            }
        }

        private async Task WriteCommentBlock(TextWriter target, string cb)
        {
            await target.WriteLineAsync(".COMMENT");
            await target.WriteLineAsync(cb);
            await target.WriteLineAsync(".ENDCOMMENT");
        }

        private async Task WriteIntro(TextWriter target)
        {
            // write the complete ".MAPPING" line.
            await target.WriteAsync(".MAPPING (");
            if (Is16Bit)
                await target.WriteAsync("16");
            else
                await target.WriteAsync("16");
            if (!string.IsNullOrWhiteSpace(Title))
            {
                await target.WriteAsync(") ");
                await target.WriteLineAsync(Title);
            }
            else
                await target.WriteLineAsync(")");
        }

        private class LabelDefinition
        {
            public string? Label { get; }
            public int? ExplicitLocation { get; }
            public int Location { get; }
            public int Length { get; }
            public int EndLocation { get; }

            public LabelSectionType Type { get; private set; }
            public LabelSectionType? ExplicitType { get; }
            public string? DataTypeKey { get; internal set; }
            public int? RelativeAddress { get; }
            public string? InlineComment { get; }
            public string[]? Aliases { get; }

            public LabelDefinition(string? label, int? explicitLocation, int loc, int length, LabelSectionType? explicitType, LabelSectionType type, string? dataTypeKey, int? relativeAddress, string[]? aliases = null, string? inlineComment = null)
            {
                this.Label = label;
                this.ExplicitLocation = explicitLocation;
                this.Location = loc;
                this.Length = length;
                this.EndLocation = this.Location + this.Length - 1;
                Type = type;
                ExplicitType = explicitType;
                DataTypeKey = dataTypeKey;
                RelativeAddress = relativeAddress;
                InlineComment = inlineComment;
                Aliases = aliases;
            }

            public string[]? BlockComments { get; internal set; }
            public bool Saved { get; internal set; }

            private struct ReferenceInfo
            {
                public int Address;
                public int RangeOffset;
            }

            private List<ReferenceInfo>? ReferencedFrom = null;

            internal void AddReferenceFrom(int address, int target)
            {
                ReferencedFrom ??= new List<ReferenceInfo>();
                ReferencedFrom.Add(new ReferenceInfo() { Address = address, RangeOffset = target - this.Location });
            }

            internal string GetDefinitionLine(bool forceTypeOutput)
            {
                StringBuilder sb = new StringBuilder();

                if (string.IsNullOrWhiteSpace(Label))
                    sb.Append("-");
                else
                    sb.Append(Label);
                if (ExplicitType.HasValue || forceTypeOutput || !string.IsNullOrWhiteSpace(InlineComment) || (Aliases != null && Aliases.Length > 0))
                {
                    if (ExplicitType.HasValue)
                    {
                        sb.Append(" ");
                        switch (ExplicitType.Value)
                        {
                            case LabelSectionType.Code:
                                sb.Append("CODE");
                                if (Length > 1)
                                {
                                    sb.Append(' ');
                                    sb.Append(Length);
                                }
                                break;
                            case LabelSectionType.BSS:
                                sb.Append("BSS ");
                                sb.Append(Length);
                                break;
                            case LabelSectionType.Data:
                                sb.Append((DataTypeKey ?? "DEC").ToUpperInvariant());
                                bool commentNeedsLength = CheckForDitig(InlineComment);
                                if (Length > 1 || commentNeedsLength || (DataTypeKey != null && (DataTypeKey.Equals("BSS", StringComparison.InvariantCultureIgnoreCase) || DataTypeKey.Equals("ASC", StringComparison.InvariantCultureIgnoreCase))))
                                {
                                    sb.Append(' ');
                                    sb.Append(Length);
                                }
                                break;
                        }
                    }
                    else
                        sb.Append(" *");
                    if (Aliases != null && Aliases.Length > 0)
                    {
                        sb.Append(" (");
                        for (int i = 0; i < Aliases.Length; i++)
                        {
                            if (i > 0)
                                sb.Append(',');
                            sb.Append(Aliases[i]);
                        }
                        sb.Append(")");
                    }
                    if (!string.IsNullOrWhiteSpace(InlineComment))
                    {
                        sb.Append(' ');
                        sb.Append(InlineComment);
                    }
                }

                return sb.ToString();
            }

            private bool CheckForDitig(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return false;
                return char.IsAsciiDigit(value.Trim()[0]);
            }
        }
        internal enum LabelSectionType
        {
            Code,
            BSS,
            Data,
        }
    }
}