using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HP9825CPU
{
    public class Assembler
    {

        // Label is defined in CPU manual, page 110
        public static System.Text.RegularExpressions.Regex ValidLabel = new System.Text.RegularExpressions.Regex(@"^[A-Z!/$""?%#@&\.][A-Z0-9!/$""?%#@&\.]{0,4}$");

        /// <summary>
        /// Parse a single line of assembler source.
        /// </summary>
        /// <param name="from">Source code reference for error messages.</param>
        /// <param name="line">The actual line to parse.</param>
        /// <param name="manager">If used, will collect and provide label resolution. Some commands require this.</param>
        /// <param name="use16Bit">True to use the 16bit CPU version.</param>
        /// <param name="useRelative">True to use relative addressing mode. Not yet supported!</param>
        /// <param name="baseAddress">An alternative to the <paramref name="manager"/> for providing a base address only.</param>
        /// <param name="isInIgnorePortion">True to indicate that the line should be ignored from the context perspective (used for IFN/IFZ/XIF!).</param>
        /// <returns>The parsed line. Null if we have to ignore it.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="NotImplementedException"></exception>
        public static AssemblyLine? Parse(SourceLineRef from, string line, LabelManager? manager = null, int? baseAddress = null, bool use16Bit = false, bool useRelative = false, bool isInIgnorePortion = false)
        {
            if (manager != null)    // manager MUST override any random stray base addresses.
                baseAddress = manager.GetPLC();
            AssemblyLine? result = null;
            //line = line.ToUpperInvariant(); // ASM is upper case, be tolerant for this...
            if (line.Trim().StartsWith('*') || string.IsNullOrWhiteSpace(line))
            {
                line = line.Trim();
                if (line.StartsWith("*"))
                    line = line.Substring(1);
                result = AssemblyLine.FromComment(from, null, line);
            }
            else
            {
                string? label = null;
                int idx;
                if (!line.StartsWith(' '))  // first chars are non-blank, must be a label...
                {
                    idx = line.IndexOf(' ');
                    if (idx < 0)    // only label? invalid!
                        throw from.Error(AssemblerErrorCodes.LabelOnly, "Only label detected: '{0}'", line);

                    label = line.Substring(0, idx).ToUpperInvariant();
                    line = line.Substring(idx + 1).Trim();

                    if (!ValidLabel.IsMatch(label))
                        throw from.Error(AssemblerErrorCodes.InvalidLabel, "Label '{0}' doesn't meet label charset requirements!", label);

                    if (!baseAddress.HasValue)
                        throw from.Error(AssemblerErrorCodes.LabelWithoutLocation, "Label '{0}' has no location. Use ORG first!", label);

                    if (manager != null)
                    {
                        if (!manager.SetKnownLocation(label, baseAddress.Value))
                            throw from.Error(AssemblerErrorCodes.DuplicateLabel, "Label '{0}' is already defined elsewhere: {1}!", label, manager.GetLocation(label));
                    }
                }
                else
                    line = line.Trim();
                // now, the first "word" is a mnemonic or a pseudo-instruction...
                idx = line.IndexOf(' ');
                string mnemonic;
                string? operand = null;
                string? suffix = null;
                if (idx > 0)
                {
                    mnemonic = line.Substring(0, idx).ToUpperInvariant();
                    line = line.Substring(idx + 1).Trim();
                }
                else
                {
                    mnemonic = line.ToUpperInvariant();
                    line = string.Empty;
                }
                ExpressionBase? exp = null;
                int? temp2;
                if (isInIgnorePortion)
                {
                    switch (mnemonic)
                    {
                        case "IFN":
                        case "IFZ":
                        case "XIF":
                        case "END":
                            break;
                        default:
                            return null;
                    }
                }
                // TODO: pseudo instructions. We need: ORG, OGG, BSS, EQU, OCT, DEC, REP, ASC - not needed: DEF
                switch (mnemonic)
                {
                    case "ORG":
                        exp = ParseExpressionAsInt(from, ref line, use16Bit ? 0xFFFF : 0x7FFFF, -1, manager, 0, false, true);
                        temp2 = exp.Compute();
                        if (!temp2.HasValue)
                            throw from.Error(AssemblerErrorCodes.ValueUndefined, "ORG must have a defined value: '{0}' does not compute.", exp.ToString());
                        manager?.SetOrg(temp2.Value);
                        baseAddress = temp2.Value;
                        result = AssemblyLine.FromOrg(from, temp2.Value, exp.ToString(), line);
                        break;
                    case "ORR":
                        if (manager != null)
                        {
                            manager.ResetOrg();
                            baseAddress = manager.GetPLC() ?? throw from.Error(AssemblerErrorCodes.OrrMissingOrg, "ORR without ORG!");
                            result = AssemblyLine.FromOrr(from, baseAddress!.Value, line);
                        }
                        else
                            throw new InvalidOperationException("ORR without label manager!");
                        break;
                    case "DFN":
                        throw from.Error(AssemblerErrorCodes.NotImplemented, "DFN is not supported here! Sorry!");
                    case "$$$":
                        result = AssemblyLine.FromComment(from, baseAddress, "$$$ not supported, ignored.");
                        break;
                    case "IFZ": // page 122 - conditional assembly.
                    case "IFN":
                    case "XIF":
                    case "UNL": // page 131 - output control
                    case "LST":
                    case "SUP":
                    case "UNS":
                    case "SKP":
                        result = AssemblyLine.FromPreprocessor(from, mnemonic, line);
                        break;
                    case "SPC":
                        exp = ParseExpressionAsInt(from, ref line, use16Bit ? 0xFFFF : 0x7FFFF, -1, manager, 0, false, true);
                        temp2 = exp.Compute();
                        if (!temp2.HasValue)
                            throw from.Error(AssemblerErrorCodes.ValueUndefined, "SPC must have a defined value!");
                        result = AssemblyLine.FromSpace(from, temp2.Value, exp.ToString(), line);
                        break;
                    case "HED":
                        result = AssemblyLine.FromHeader(from, line);
                        break;
                    case "REP":
                        exp = ParseExpressionAsInt(from, ref line, use16Bit ? 0xFFFF : 0x7FFFF, -1, manager, 0, false, true);
                        temp2 = exp.Compute();
                        if (!temp2.HasValue)
                            throw from.Error(AssemblerErrorCodes.ValueUndefined, "REP must have a defined value!");
                        result = AssemblyLine.FromRep(from, baseAddress, temp2.Value, exp.ToString(), line);
                        break;
                    case "END":
                        manager?.SetEnded();
                        result = AssemblyLine.FromEnd(from, line);
                        break;
                    case "DEF":
                        // examples: DEF .INT...
                        if (!baseAddress.HasValue)
                            throw from.Error(AssemblerErrorCodes.DataWithoutLocation, "DEF without known address!");
                        exp = ParseExpressionAsAddress(from, ref line, use16Bit ? 0xFFFF : 0x7FFFF, baseAddress.Value, manager, use16Bit, false);
                        // ",I" is supported, but only makes sense on 15-bit systems!
                        bool isIndirect = false;
                        if (line.StartsWith(','))
                        {
                            line = line.Substring(1).Trim();
                            if (line.StartsWith('I') && (line.Length == 1 || char.IsWhiteSpace(line[1])))
                            {
                                // got the "I" flag...
                                if (use16Bit)
                                    throw from.Error(AssemblerErrorCodes.InvalidPerCpuMode, "DEF ,I is invalid in 16-bit mode!");
                                isIndirect = true;
                                line = line.Substring(1).Trim();
                            }
                            else
                            {
                                throw from.Error(AssemblerErrorCodes.InvalidSuffix, "Invalid DEF encountered! Expecting ,I at most, found ,{line}");
                            }
                        }
                        result = AssemblyLine.FromDef(from, baseAddress, exp, exp.ToString(), isIndirect, label, line);
                        break;
                    case "BSS":
                        if (!baseAddress.HasValue)
                            throw from.Error(AssemblerErrorCodes.DataWithoutLocation, "BSS without known address!");
                        exp = ParseExpressionAsInt(from, ref line, use16Bit ? 0xFFFF : 0x7FFFF, baseAddress.Value, manager, 0, false, use16Bit);
                        temp2 = exp.Compute();
                        if (!temp2.HasValue)
                            throw from.Error(AssemblerErrorCodes.ValueUndefined, "Undefined size for BSS: {0}", exp.ToString());
                        result = AssemblyLine.FromBSS(from, baseAddress.Value, temp2.Value, exp.ToString(), label, line);
                        break;
                    case "OCT":
                    case "DEC":
                        // parse litarls as either list of decimals or octals; allow negate and full range... No expressions allowed!
                        if (!baseAddress.HasValue)
                            throw from.Error(AssemblerErrorCodes.DataWithoutLocation, "{0} without known address!", mnemonic);
                        List<int> values = new List<int>();
                        while (line.Length > 0)
                        {
                            int idx2 = line.IndexOfAny(ListSeparators);
                            string part;
                            if (idx2 < 0)
                            {
                                part = line;
                                line = string.Empty;
                            }
                            else
                            {
                                part = line.Substring(0, idx2);
                                line = line.Substring(idx2).Trim(); // keep "," if it was the found char!
                            }
                            int value = Convert.ToInt32(part, mnemonic == "OCT" ? 8 : 10);
                            if (value < -32768 || value > 0xFFFF)
                                throw from.Error(AssemblerErrorCodes.IntegerOverflow, "Constant out of range: {0} {1} is {2}...", mnemonic, part, value);
                            values.Add(value);
                            if (line.StartsWith(','))
                            {
                                line = line.Substring(1).Trim();
                            }
                            else
                                break;
                        }
                        if (values.Count == 0)
                            throw from.Error(AssemblerErrorCodes.MissingArguments, "{0} without values!", mnemonic);
                        result = AssemblyLine.FromConstant(from, baseAddress.Value, mnemonic, mnemonic == "OCT" ? 8 : 10, values, label, line);
                        break;
                    case "ASC": // page 127
                                // parse an expression that needs to evaluate to a fixed 1-28 value.
                                // all characters following - until EOL - up to 2n are assumed to be string characters.
                                // if eol is before 2n - fill remaining size with ' '
                                // parse litarls as either list of decimals or octals; allow negate and full range... No expressions allowed!
                        if (!baseAddress.HasValue)
                            throw from.Error(AssemblerErrorCodes.DataWithoutLocation, "ASC without known address!");
                        exp = ParseExpressionAsInt(from, ref line, 0xFFFF, baseAddress.Value, manager, 0, false, true);
                        var nChars = exp.Compute();
                        if (!nChars.HasValue)
                            throw from.Error(AssemblerErrorCodes.ValueUndefined, "Expression {0} didn't yield a word count!", exp.ToString());
                        if (nChars.Value < 1 || nChars.Value > 28)
                            throw from.Error(AssemblerErrorCodes.ValueOutOfRange, "Expression {0} out of range (1..28) for ASC: {1}", exp.ToString(), nChars);

                        if (!line.StartsWith(','))
                            throw from.Error(AssemblerErrorCodes.MissingArguments, "ASC statement is missing ',xx' after word count.");

                        line = line.Substring(1);   // don't trim here, we capture all chars, up to n*2, including spaces.

                        string ascii;
                        if (line.Length >= nChars.Value * 2)
                        {
                            // got more than enough...
                            ascii = line.Substring(0, nChars.Value * 2);
                            line = line.Substring(ascii.Length);
                        }
                        else
                        {
                            ascii = line + new string(' ', nChars.Value * 2 - line.Length);
                            line = string.Empty;
                        }
                        result = AssemblyLine.FromAscii(from, baseAddress.Value, ascii, exp.ToString(), label, line);
                        break;
                    case "ABS":
                        // examples: DEF .INT...
                        if (!baseAddress.HasValue)
                            throw from.Error(AssemblerErrorCodes.DataWithoutLocation, "ABS without known address!");
                        exp = ParseExpressionAsInt(from, ref line, 0xFFFF, baseAddress.Value, manager, 0, true, true);
                        result = AssemblyLine.FromAbs(from, baseAddress.Value, exp, exp.ToString(), label, line);
                        break;
                    case "EQU":
                        if (label == null)
                            throw from.Error(AssemblerErrorCodes.MissingLabel, "EQU without label!");
                        if (!baseAddress.HasValue)
                            throw from.Error(AssemblerErrorCodes.DataWithoutLocation, "EQU without known address!");
                        exp = ParseExpressionAsInt(from, ref line, 0xFFFF, baseAddress.Value, manager, 0, false, true);
                        manager?.SetLabelUnknown(label); // prepare for fixup operations...
                        result = AssemblyLine.FromEqu(from, label, exp.ToString(), exp, manager, line);
                        break;
                }

                if (result == null) // not yet, continue searching...
                {

                    // find out if our mnemonic...
                    if (!baseAddress.HasValue)
                        throw from.Error(AssemblerErrorCodes.InstructionWithoutLocation, "Only ORG and some preprocessor instructions are allowed without known address: {0}", mnemonic);
                    if (baseAddress.Value < 0 || baseAddress.Value > (use16Bit ? 0xFFFF : 0x7FFF))
                        throw from.Error(AssemblerErrorCodes.AddressOutOfRange, "Base address is outside of bounds: {0:x4}!", baseAddress);

                    CmdStructure? foundCmd = null;
                    int opCodeBase = 0;

                    foreach (var cmd in CpuConstants.Commands)
                    {
                        foreach (var vr in cmd.GetVersions())
                        {
                            // got mnemonic updates... check for those...
                            if (vr.Mnemonic == mnemonic)
                            {
                                foundCmd = cmd;
                                opCodeBase = vr.OpCode;
                            }
                        }
                    }

                    if (!foundCmd.HasValue)
                        throw from.Error( AssemblerErrorCodes.UnknownMnemonic, "Unknown instruction: {0}", mnemonic);

                    if (foundCmd.Value.Is16Bit && !use16Bit)
                        throw from.Error(AssemblerErrorCodes.InvalidPerCpuMode, "16 bit instruction on 15 bit assembly: {0}", mnemonic);

                    // check if it needs arguments and/or a suffix. Then, add what's left of the line to the comment...

                    exp = null;

                    switch (foundCmd.Value.OperandType)
                    {
                        case OperandType.None:
                            break;
                        case OperandType.NValue:
                            exp = ParseExpressionAsInt(from, ref line, foundCmd.Value.OperandMask, baseAddress.Value, manager, -1, false);
                            operand = exp.ToString();
                            break;
                        case OperandType.RegIndex:
                            idx = ParseRegisterName(from, ref line, foundCmd.Value.OperandMask, mnemonic);
                            operand = CpuConstants.RegisterNames[idx];
                            opCodeBase |= idx;
                            break;
                        case OperandType.SkipValue:
                            exp = ParseExpressionAsInt(from, ref line, foundCmd.Value.OperandMask, baseAddress.Value, manager, 0, true);
                            exp.Insert(new MakeDeltaValueExpression(baseAddress.Value));
                            operand = exp.ToString();
                            break;
                        case OperandType.MemoryAddress:
                            exp = ParseExpressionAsAddress(from, ref line, foundCmd.Value.OperandMask, baseAddress.Value, manager, use16Bit, true);
                            operand = exp.ToString();
                            break;
                        case OperandType.SkipValueNonRelative:
                            exp = ParseExpressionAsInt(from, ref line, foundCmd.Value.OperandMask, baseAddress.Value, manager, 0, true);
                            operand = exp.ToString();
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    // suffix update can only be one...
                    var su = foundCmd.Value.Updates?.FirstOrDefault(x => x.IsSuffixUpdate);
                    if (su != null) // see if we have a ",..." option that fits...
                    {
                        if (line.StartsWith(','))
                        {
                            // we got a candidate...
                            line = line.Substring(1).Trim();
                            // check for "yes/yes" option
                            if (su.Value.ConditionMask != 0)
                            {
                                int outcome;
                                string check = su.Value.What.NonZeroReplace.ToString();
                                if (line.StartsWith(check, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    outcome = su.Value.Mask;
                                    suffix = check;
                                }
                                else
                                {
                                    check = su.Value.What.ZeroReplace.ToString();
                                    if (!line.StartsWith(check, StringComparison.InvariantCultureIgnoreCase))
                                        throw from.Error(AssemblerErrorCodes.InvalidSuffix, "Cannot map suffix for {0}. Only {1} and {2} are defined here, but found {3}!", mnemonic, su.Value.What.NonZeroReplace, su.Value.What.ZeroReplace, line.Length > 0 ? line[0] : "<EOL>");
                                    outcome = 0;
                                    suffix = check;
                                }
                                opCodeBase |= outcome;
                                opCodeBase |= su.Value.ConditionMask;
                            }
                            else
                            {
                                // only "on" option available.
                                string check = su.Value.What.NonZeroReplace.ToString();
                                if (line.StartsWith(check, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    opCodeBase |= su.Value.Mask;
                                    suffix = check;
                                }
                                else
                                {
                                    // allow alternatively "zero" replacement if it is there...
                                    check = su.Value.What.ZeroReplace.ToString();
                                    if (line.StartsWith(check, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        suffix = check;
                                    }
                                    else
                                        throw from.Error(AssemblerErrorCodes.InvalidSuffix, "Option is not reconized for {0}. Only {1} defined here, but found {2}!", mnemonic, su.Value.What.NonZeroReplace, line.Length > 0 ? line[0] : "<EOL>");
                                }
                            }
                            if (line.Length > 1 && !char.IsWhiteSpace(line[1]))
                                throw from.Error(AssemblerErrorCodes.InvalidSuffix, "Option is invalid (single char required), but found {0}!", line[0]);

                            line = line.Substring(1).Trim();
                        }
                    }
                    result = AssemblyLine.FromInstruction(from, baseAddress.Value, opCodeBase, exp, mnemonic, operand, suffix, label, line);
                }
            }

            var len = result.InstructionOutputSize();
            baseAddress += len;
            if (manager != null)
            {
                if (!manager.MovePLC(len))
                    throw from.Error(AssemblerErrorCodes.InputAfterEnd, " Tried to emit code after END!");
            }

            return result;
        }

        //private class ExpressionSegment

        private static int ParseRegisterName(SourceLineRef from, ref string line, int mask, string mnemonic)
        {
            for (int i = 0; i < CpuConstants.RegisterNames.Length; i++)
            {
                var thisReg = CpuConstants.RegisterNames[i];
                if (line.StartsWith(thisReg, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (line.Length == thisReg.Length)
                    {
                        line = string.Empty;
                        // register name is the last thing, we found it.
                        if (i > mask)
                            throw from.Error(AssemblerErrorCodes.InvaldRegister, "The register {0} is not supported for the instruction {1}!", thisReg, mnemonic);
                        return i;
                    }
                    var c = line[thisReg.Length];
                    if (c == ',' || char.IsWhiteSpace(c))
                    {
                        line = line.Substring(thisReg.Length).Trim();
                        if (i > mask)
                            throw from.Error(AssemblerErrorCodes.InvaldRegister, "The register {0} is not supported for the instruction {1}!", thisReg, mnemonic);
                        return i;
                    }
                }
            }
            throw from.Error(AssemblerErrorCodes.SyntaxError, "Expected a register name for {1} but found no match: {0}", line, mnemonic);
        }

        internal abstract class ExpressionBase
        {
            protected ExpressionBase? Next;

            public bool Negate { get; set; }

            public void Append(ExpressionBase exp)
            {
                if (Next == null)
                    Next = exp;
                else
                    Next.Append(exp);
            }

            public abstract int? Compute(int? leftValue = null);

            public void Insert(ExpressionBase exp)
            {
                if (exp.Next != null)
                    throw new InvalidOperationException();
                exp.Next = this.Next;
                this.Next = exp;
            }

            protected virtual void Stringify(StringBuilder sb)
            {
                if (Negate)
                    sb.Append('-');
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                var x = this;
                while (x != null)
                {
                    x.Stringify(sb);
                    x = x.Next;
                }
                return sb.ToString();
            }
        }

        private class MakeDeltaValueExpression
            : ExpressionBase
        {
            public MakeDeltaValueExpression(int baseAddress)
            {
                BaseAddress = baseAddress;
            }
            private int BaseAddress;

            public override int? Compute(int? leftValue = null)
            {
                if (leftValue.HasValue)
                    throw new InvalidOperationException();
                var x = Next?.Compute(null);
                if (x.HasValue)
                {
                    return x.Value - BaseAddress;
                }
                return null;
            }
        }
        private class LeadExpression
            : ExpressionBase
        {
            public bool AllowNegative { get; set; }
            public int Correction { get; set; }
            public int Mask { get; set; }

            public override int? Compute(int? leftValue = null)
            {
                if (leftValue.HasValue)
                    throw new InvalidOperationException("Lead got input?!");

                var x = Next?.Compute(null);    // next one is relevant...
                if (x.HasValue)
                {
                    if (x.Value < 0 && !AllowNegative)
                    {
                        throw new InvalidOperationException("The expression resulted in a negative value that was not allowed!");
                    }
                    int r = x.Value + Correction;
                    int min = AllowNegative ? -(Mask >> 1) - 1 : 0;
                    int max = AllowNegative ? (Mask >> 1) : Mask;

                    if (r < min || r > max)
                    {
                        throw new InvalidOperationException("The expression resulted in a value that does not fit into the field!");
                    }
                    return r & Mask;
                }
                return null;
            }
        }

        private class HereExpression
            : ExpressionBase
        {
            private int value;
            public HereExpression(int location)
            {
                value = location;
            }

            protected override void Stringify(StringBuilder sb)
            {
                base.Stringify(sb);
                sb.Append('*');
            }

            public override int? Compute(int? leftValue = null)
            {
                if (leftValue.HasValue)
                    throw new InvalidOperationException();
                if (Next != null)
                    return Next.Compute(value);
                return value;
            }
        }
        private class AddExpression
            : ExpressionBase
        {
            public AddExpression(bool negative)
            {
                Negate = negative;
            }
            protected override void Stringify(StringBuilder sb)
            {
                sb.Append(Negate ? '-' : '+');
            }
            public override int? Compute(int? leftValue = null)
            {
                if (!leftValue.HasValue)
                    return null;
                var x = Next?.Compute(null);
                if (x.HasValue)
                {
                    if (Negate)
                        return leftValue.Value - x.Value;
                    else
                        return leftValue.Value + x.Value;
                }
                return null;
            }
        }
        private class RegisterExpression
            : ExpressionBase
        {
            private int Index;
            public RegisterExpression(int index)
            {
                Index = index;
            }
            protected override void Stringify(StringBuilder sb)
            {
                sb.Append(CpuConstants.RegisterNames[Index]);
            }
            public override int? Compute(int? leftValue = null)
            {
                if (leftValue.HasValue)
                    throw new InvalidOperationException();
                if (Next != null)
                    return Next.Compute(Index);
                return Index;
            }
        }
        private class LabelExpression
            : ExpressionBase
        {
            private string Label;
            private int? Location;
            public LabelExpression(string label, int? loc)
            {
                Label = label;
                Location = loc;
            }

            protected override void Stringify(StringBuilder sb)
            {
                base.Stringify(sb);
                sb.Append(Label);
            }

            public override int? Compute(int? leftValue = null)
            {
                if (leftValue.HasValue)
                    throw new InvalidOperationException();
                if (Next != null && Location.HasValue)
                    return Next.Compute(Location.Value);
                return Location;
            }

            public void Fixup(int location)
            {
                Location = location;
            }
        }

        private class NumericExpression
            : ExpressionBase
        {
            private int value;
            private int nBase;
            public NumericExpression(int num, int nBase = 8)
            {
                value = num;
                this.nBase = nBase;
            }
            protected override void Stringify(StringBuilder sb)
            {
                base.Stringify(sb);
                sb.Append(Convert.ToString(value, nBase));
            }
            public override int? Compute(int? leftValue = null)
            {
                if (leftValue.HasValue)
                    throw new InvalidOperationException();
                if (Next != null)
                    return Next.Compute(value);
                return value;
            }
        }

        private class MakeRelativeAddressExpression
            : ExpressionBase
        {
            int BaseAddress;
            bool Is16Bit;
            public MakeRelativeAddressExpression(int baseAddress, bool is16Bit)
            {
                BaseAddress = baseAddress;
                Is16Bit = is16Bit;
            }
            public override int? Compute(int? leftValue = null)
            {
                if (leftValue.HasValue)
                    throw new InvalidOperationException();
                if (Next != null)
                {
                    var x = Next.Compute(null);
                    if (x.HasValue)
                    {
                        if (x < 0 || x > (Is16Bit ? 0xFFFF : 0x7FFF))
                            return null;
                        // x == absolute address to target...
                        // baseAddress == current address.
                        // step 1.: If the target address is within the first page, or wrap around page, we use "Base Page Addressing".
                        if (x <= 0x1FF)
                        {
                            // low end of base page...
                            return x & 0x1FF;   // base page bit clear, sign clear...
                        }
                        if (x >= (Is16Bit ? 0xFE00 : 0x7E00))
                        {
                            // high end of base page...
                            x = x.Value & 0x01FF;
                            x = x.Value | 0x0200; // sign bit.
                            return x;
                        }
                        // step 2.: if the taget address is within the current page, we use "current page" mode.
                        int curPage = BaseAddress & 0xFC00;
                        int targetPage = x.Value & 0xFC00;
                        if (curPage != targetPage)
                            throw new InvalidOperationException("The target address is outside of the valid range!");
                        int ofs = x.Value & 0x3FF;
                        ofs ^= 0x200;          // flip sign bit for some odd reason.
                        return ofs | 0x400; // set "C" bit
                        // fail with "out of bounds"
                    }
                }
                return null;
            }

        }

        private static readonly char[] ExpressionSeparators = { ',', '+', '-', ' ', '\t' };

        private static readonly char[] ListSeparators = { ' ', '\t', ',' };

        private static ExpressionBase ParseExpressionAsAddress(SourceLineRef from, ref string line, int mask, int baseAddress, LabelManager? manager, bool is16Bit, bool convertToInstructionAddress)
        {
            var exp = ParseExpressionAsInt(from, ref line, mask, baseAddress, manager, allowUnsignedFullRange: is16Bit);
            if (convertToInstructionAddress)
                exp.Insert(new MakeRelativeAddressExpression(baseAddress, is16Bit));
            return exp;
        }
        private static ExpressionBase ParseExpressionAsInt(SourceLineRef from, ref string line, int mask, int baseAddress, LabelManager? manager, int correction = 0, bool allowNegative = false, bool allowUnsignedFullRange = false)
        {
            if ((mask & 1) == 0)    // we don't shift; we expect this to be the rightmose bits...
                throw new NotImplementedException();

            // expressions are...
            // Registers, Labels, Integers, *, and a combination with of "+" and "-".
            // examples: A, *, *+1, -EXPR+4+OTHR

            ExpressionBase? exp = new LeadExpression() { Correction = correction, AllowNegative = allowNegative, Mask = mask };

            while (line.Length > 0)
            {
                bool negate = false;
                if (line.StartsWith('-'))
                {
                    negate = true;
                    line = line.Substring(1).Trim();
                }
                else
                {
                    if (line.StartsWith('+'))
                    {
                        line = line.Substring(1).Trim();    // trim leading "+"
                    }
                }

                int idx = line.IndexOfAny(ExpressionSeparators);
                string part;
                if (idx < 0)
                {
                    part = line;
                    line = string.Empty;
                }
                else
                {
                    part = line.Substring(0, idx);
                    line = line.Substring(idx).Trim();
                }
                if (part == "*")
                {
                    // current location...
                    exp.Append(new HereExpression(baseAddress));
                }
                else
                {
                    // register perhaps?
                    bool found = false;
                    for (idx = 0; idx < CpuConstants.RegisterNames.Length; idx++)
                    {
                        if (CpuConstants.RegisterNames[idx].Equals(part, StringComparison.InvariantCultureIgnoreCase))
                        {
                            exp.Append(new RegisterExpression(idx));
                            found = true;
                            if (negate)
                                throw from.Error(AssemblerErrorCodes.SyntaxError, "Cannot negate register: {0}", CpuConstants.RegisterNames[idx]);
                            break;
                        }
                    }
                    if (!found)
                    {
                        // must be a label or a numeric...
                        part = part.ToUpperInvariant();
                        if (Assembler.ValidLabel.IsMatch(part))
                        {
                            if (manager == null)
                                throw new InvalidOperationException("Cannot use labels in expressions without label manager!");
                            int? temp = manager.GetLocation(part);
                            var le = new LabelExpression(part, temp) { Negate = negate, };
                            // bypass relocation, use had value right now.
                            if (!temp.HasValue)
                                manager.RegisterRelocation(part, le.Fixup);
                            exp.Append(le);
                        }
                        else
                        {
                            // only option left is a number...
                            // spec from the processor manual, page 115
                            // two optins: decimal or octal (B suffix) - decimal is signed (sign should be handled above already!)
                            int temp;
                            int nBase = 10;
                            if (part.EndsWith('B')) // octal...
                            {
                                if (negate)
                                    throw from.Error(AssemblerErrorCodes.SyntaxError, "Octal numbers don't support signs: -{0}", part);
                                try
                                {
                                    temp = Convert.ToInt32(part.Substring(0, part.Length - 1), 8);
                                }
                                catch (Exception)
                                {
                                    throw from.Error(AssemblerErrorCodes.InvalidNumeral, "Invalid octal constant: {0}", part);
                                }
                                nBase = 8;
                            }
                            else    // decimal
                            {
                                try
                                {
                                    temp = Convert.ToInt32(part, 10);
                                }
                                catch (Exception)
                                {
                                    throw from.Error(AssemblerErrorCodes.InvalidNumeral, "Invalid decimal constant: {0}", part);
                                }
                            }
                            if (negate)
                            {
                                if (temp > 32768)
                                    throw from.Error(AssemblerErrorCodes.InvalidNumeral, "Integer out of range: -{0}", temp);
                            }
                            else
                            {
                                if (temp > (allowUnsignedFullRange ? 0xFFFF : 0x7FFF))
                                    throw from.Error(AssemblerErrorCodes.InvalidNumeral, "Integer out of range: {0}", temp);
                            }
                            exp.Append(new NumericExpression(temp, nBase) { Negate = negate });
                        }
                    }
                }
                if (line.StartsWith('+') || line.StartsWith('-'))
                {
                    // add/subtract operation.
                    exp.Append(new AddExpression(line.StartsWith('-')));
                    line = line.Substring(1).Trim();
                }
                else
                {
                    break;  // got all that was an expression...
                }
            }

            return exp;
        }
    }
}