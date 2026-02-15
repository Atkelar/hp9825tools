using System;

namespace HP9825CPU
{
    public class Disassembler
    {

        public static int BasePattern(int opCode)
        {
            foreach (var m in CpuConstants.Commands)
            {
                if ((opCode & m.ValueMask) == m.Value)
                {
                    return m.Value;
                }
            }
            return -1;
        }

        public static AssemblyLine Disassemble(int opCode, int baseAddress, string? label = null,  string? comment=null, bool includeDefaults = false)
        {
            foreach (var m in CpuConstants.Commands)
            {
                if ((opCode & m.ValueMask) == m.Value)
                {
                    var str = m.Mnemonic;
                    string? suffix = null;
                    // found command pattern...
                    // fixup any mnemonic placeholders..
                    if (m.Updates != null)
                    {
                        foreach (var u in m.Updates)
                        {
                            if (u.ConditionMask == 0 || ((opCode & u.ConditionMask) != 0))
                            {
                                // yes.
                                bool isNonZero = (opCode & u.Mask) != 0;
                                char use = isNonZero ? u.What.NonZeroReplace : u.What.ZeroReplace;
                                if (use != '\u0000')    // it's a non-empty addition...
                                {
                                    if (u.IsSuffixUpdate)
                                    {
                                        if (includeDefaults)
                                            suffix = use.ToString();
                                        else
                                            if (!((u.DefaultsToSet && isNonZero) || (!u.DefaultsToSet && !isNonZero)))  // set only if we have a non-default.
                                                suffix = use.ToString();
                                    }
                                    else
                                        str = str.Replace(u.What.Placeholder, use);
                                }
                                else
                                {
                                    if (!u.IsSuffixUpdate)
                                        throw new InvalidOperationException("Cannot have empty update as part of mnemnoic text!");
                                }
                            }
                        }
                    }
                    string? operand = null;
                    int temp1;
                    bool isRelativeNotation = true;
                    switch (m.OperandType)
                    {
                        case OperandType.NValue:
                            temp1 = opCode & m.OperandMask;
                            operand = Convert.ToString(temp1 + 1, 8);
                            break;
                        case OperandType.RegIndex:
                            temp1 = opCode & m.OperandMask;
                            operand = CpuConstants.RegisterNames[temp1];
                            break;
                        case OperandType.SkipValueNonRelative:
                            isRelativeNotation = false;
                            goto case OperandType.SkipValue;

                        case OperandType.SkipValue:
                            temp1 = opCode & m.OperandMask;
                            if ((temp1 & 0b100_000) != 0)
                            {
                                // negative value, sign extend...
                                temp1 = ((~temp1) & 0b011_111) + 1;
                                operand = (isRelativeNotation ? "*-" : "-") + Convert.ToString(temp1, 8);   // check if negative works in octal?
                            }
                            else
                            {
                                if (temp1 == 0)
                                    operand = isRelativeNotation ? "*" : null;
                                else
                                    operand = (isRelativeNotation ? "*+" : "") + Convert.ToString(temp1, 8);   // check if negative works in octal?
                            }
                            break;
                        case OperandType.MemoryAddress:
                            temp1 = opCode & m.OperandMask;
                            // the mnemonic doesn't have a way of showing "base" or "current" page addresses. We need to "outsmart" it.
                            int memBase;
                            int target;
                            bool maybeReg = false;
                            if ((temp1 & 0b10_000_000_000) != 0)
                            {
                                // current page!
                                memBase = baseAddress & 0b0_111_110_000_000_000;    // get base address from current address...
                                target = memBase + ((temp1 ^ 0b1_000_000_000) & 0b1_111_111_111);   // for some ODD reason, bit 9 is complemented in absolute page addressing mode... don't ask...
                            }
                            else
                            {
                                // base page!
                                memBase = 0;
                                maybeReg = true;
                                if ((temp1 & 0b1_000_000_000) != 0) // negative offset!
                                {
                                    target = (memBase - (((~temp1) & 0b_111_111_111) + 1)) & 0x7FFF;    // we only have 64k in words...
                                }
                                else
                                {
                                    target = memBase + (temp1 & 0b_111_111_111);
                                }
                            }
                            if (maybeReg && target >= 0 && target < CpuConstants.RegisterNames.Length)
                                operand = CpuConstants.RegisterNames[target];
                            else
                                operand = Convert.ToString(target, 8);
                            break;
                    }

                    // if (operand != null)
                    //     str += " " + operand;
                    // if (suffix != null)
                    //     str += ", " + suffix;
                    return AssemblyLine.FromInstruction(SourceLineRef.Unknown, baseAddress, opCode, null, str, operand, suffix, label, comment);
                }
            }
            return AssemblyLine.FromComment(SourceLineRef.Unknown, baseAddress, $"Unknown opcode {opCode} at {label}@{baseAddress}. {comment}");
        }
    }
}