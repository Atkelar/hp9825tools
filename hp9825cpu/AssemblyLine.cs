using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HP9825CPU
{
    /// <summary>
    /// Represents a single line in assembler code - can be comment only, a CPU instruction, data defintion, preprocessor directive...
    /// </summary>
    public abstract class AssemblyLine
    {
        /// <summary>
        /// The memory address for this instruction/data item if known.
        /// </summary>
        public int? Address { get; set; }

        /// <summary>
        /// The comment that goes with this line.
        /// </summary>
        public string? Comment { get; set; }

        /// <summary>
        /// The label that was defined in this line, thus associated with the <see cref="Address"/>.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// The original source file and line.
        /// </summary>
        public SourceLineRef Source { get; set; }

        /// <summary>
        /// True to indicate that this line can be repeated with a preceding "REP" instruction.
        /// </summary>
        public bool IsRepeatable { get; protected set; } = false;
        
        /// <summary>
        /// True to indicate that this line has been created from a macro (like "REP").
        /// </summary>
        public bool IsFromMacro { get; set; }

        /// <summary>
        /// Prints the source code line in a "normalized" way. Do NOT add newline at the end!
        /// </summary>
        /// <param name="target">The receiver of the line.</param>
        public abstract string Beautified();
        
        /// <summary>
        /// Writes the source code line in a "normalized" way.
        /// </summary>
        /// <param name="target"></param>
        public void PrintBeautified(System.IO.TextWriter target)
        {
            target.WriteLine(Beautified());
        }

        /// <summary>
        /// Gets the line in a normalized way.
        /// </summary>
        /// <returns>The source line to ready to show.</returns>
        public override string ToString()
        {
            return Beautified();
        }

        protected AssemblyLine(SourceLineRef from, int? address, string? comment, string? label)
        {
            Address = address;
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment;
            Label = label;
            Source = from;
        }

        public abstract void CreateOutput(ListingPrinter target);
        public abstract void ApplyTo(Memory target);
        public abstract int InstructionOutputSize();



        public static AssemblyLine FromComment(SourceLineRef from, int? address, string? comment)
        {
            return new AssemblyCommentLine(from, comment);
        }

        internal static AssemblyLine FromInstruction(SourceLineRef from, int address, int opCode, Assembler.ExpressionBase? fixupFrom, string mnemonic, string? operand, string? suffix, string? label = null, string? comment = null)
        {
            return new AssemblyInstructionLine(from, address, opCode, fixupFrom, label, mnemonic, operand, suffix, comment);
        }

        internal static AssemblyLine FromOrg(SourceLineRef from, int address, string operand, string? comment = null)
        {
            return new AssemblyOrgLine(from, address, false, operand, comment);
        }

        internal static AssemblyLine FromOrr(SourceLineRef from,int address, string? comment = null)
        {
            return new AssemblyOrgLine(from, address, true, null, comment);
        }

        internal static AssemblyLine FromRep(SourceLineRef from, int? baseAddress, int count, string expression, string? label, string? comment = null)
        {
            return new AssemblyRepLine(from, baseAddress, count, label, expression, comment);
        }

        internal static AssemblyLine FromEnd(SourceLineRef from, string? comment = null)
        {
            return new AssemblyEndLine(from, comment);
        }

        internal static AssemblyLine FromDef(SourceLineRef from, int? address, Assembler.ExpressionBase? fixupFrom, string expression, bool isIndirect, string? label, string? comment)
        {
            return new AssemblyDefLine(from, address, fixupFrom, expression, isIndirect, label, comment);
        }

        internal static AssemblyLine FromAbs(SourceLineRef from, int? address, Assembler.ExpressionBase? fixupFrom, string expression, string? label, string? comment)
        {
            return new AssemblyAbsLine(from, address, fixupFrom, expression, label, comment);
        }

        internal static AssemblyLine FromBSS(SourceLineRef from, int address, int count, string expression, string? label, string? comment)
        {
            return new AssemblyBssLine(from, address, count, expression, label, comment);
        }

        internal static AssemblyLine FromConstant(SourceLineRef from, int address, string mnemonic, int nBase, IEnumerable<int> values, string? label, string? comment)
        {
            return new AssemblyConstantLine(from, address, mnemonic, nBase, values, label, comment);
        }

        internal static AssemblyLine FromEqu(SourceLineRef from, string label, string expression, Assembler.ExpressionBase? fixupFrom, LabelManager? manager, string? comment)
        {
            return new AssemblyEquLine(from, label, expression, fixupFrom, manager, comment);
        }

        internal static AssemblyLine FromAscii(SourceLineRef from, int address, string ascii, string nExpressoin, string? label, string? comment)
        {
            return new AssemblyAsciiLine(from, address, ascii,nExpressoin, label, comment);
        }

        internal static AssemblyLine FromPreprocessor(SourceLineRef from, string mnemonic, string? comment)
        {
            return new AssemblyControlLine(from, mnemonic, comment);
        }

        internal static AssemblyLine FromHeader(SourceLineRef from, string header)
        {
            return new AssemblyHdrLine(from, header);
        }
        internal static AssemblyLine FromSpace(SourceLineRef from, int numLines, string expression, string? comment)
        {
            return new AssemblySpcLine(from, numLines, expression, comment);
        }

        #region Implementations

        private const string BeautifiedLineTemplate = "{0,-5} {1,-3} {2,-16}{3}";
        private const string BeautifiedCommentLineTempalate = "*{0}";

        private static string FormatCodeLine(string? label, string mnemonic, string? opts, string? suffix, string? comment)
        {
            return string.Format(BeautifiedLineTemplate, label, mnemonic, (opts ?? string.Empty) + (suffix != null ? ","+suffix : string.Empty) , comment).TrimEnd();
        }


        public class AssemblyHdrLine
                : AssemblyPreprocessorLine
        {
            internal AssemblyHdrLine(SourceLineRef from, string header)
                : base(from, null, null, null)
            {
                Header = header;
            }

            public string Header { get; private set; }

            public override void CreateOutput(ListingPrinter target)
            {
                // print first if we want it
                target.PrintSourceLine(Address, null, null, "HED", Header, null, true, IsFromMacro);
                target.SetHeadline(Header);
            }

            public override string Beautified()
            {
                return FormatCodeLine(null, "HED", Header, null, null);
            }

        }

        public class AssemblySpcLine
                : AssemblyPreprocessorLine
        {
            internal AssemblySpcLine(SourceLineRef from, int numLines, string expression, string? comment)
                : base(from, null, comment, null)
            {
                Count = numLines;
                Expression = expression;
            }

            public int Count { get; private set; }
            private string Expression;

            public override void CreateOutput(ListingPrinter target)
            {
                target.PrintSourceLine(Address, null, null, "SPC", Expression, Comment, true, IsFromMacro);
                target.PrintEmptyLines(Count);
            }
            public override string Beautified()
            {
                return FormatCodeLine(null, "SPC", Expression, null, Comment);
            }
        }


        public class AssemblyControlLine
                : AssemblyPreprocessorLine
        {
            internal AssemblyControlLine(SourceLineRef from, string mnemonic, string? comment)
                : base (from, null, comment, null)
            {
                Mnemonic = mnemonic;
            }
            public string Mnemonic { get; private set; }
            public override void CreateOutput(ListingPrinter target)
            {
                switch (Mnemonic)
                {
                    case "UNL":
                        // print command first, so we still see it...
                        target.PrintSourceLine(Address, null, Label, Mnemonic, null, Comment, false, IsFromMacro);
                        target.SuppressOutput = true;
                        return;
                    case "LST":
                        target.SuppressOutput = false;
                        break;
                    case "SUP":
                        target.SuppressExtraLines = true;
                        break;
                    case "UNS":
                        target.SuppressExtraLines = false;
                        break;
                    case "SKP":
                        target.PrintSourceLine(Address, null, Label, Mnemonic, null, Comment, true, IsFromMacro);
                        target.NewPageNow();
                        return;
                }
                target.PrintSourceLine(Address, null, Label, Mnemonic, null, Comment, false, IsFromMacro);
            }

            public override string Beautified()
            {
                return FormatCodeLine(Label, Mnemonic, null, null, Comment);
            }
        }

        private class AssemblyAsciiLine
                : AssemblyLine
        {
            public AssemblyAsciiLine(SourceLineRef from, int address, string ascii, string nExpressoin, string? label, string? comment)
                : base(from, address, comment, label)
            {
                Ascii = ascii;
                var enc = System.Text.Encoding.ASCII.GetBytes(ascii);
                if (enc.Length % 2 != 0)
                    throw new InvalidOperationException();
                List<int> temp = new List<int>();


                for (int i = 0; i < enc.Length; i += 2)
                {
                    temp.Add(
                        enc[i] << 8 | enc[i + 1]
                    );
                }

                Values = temp.ToArray();
                NExpression = nExpressoin;
                IsRepeatable = true;
            }
            private string NExpression;

            public override int InstructionOutputSize()
            {
                return Values.Length;
            }

            public override void ApplyTo(Memory target)
            {
                // don't change anything!
                for (int i = 0; i < Values.Length; i++)
                {
                    target[Address!.Value + i] = (Values[i] & 0xFFFF);
                }
            }

            private int[] Values;
            private string Ascii;

            public override void CreateOutput(ListingPrinter target)
            {
                target.PrintSourceLine(Address, Values[0], Label, "ASC", string.Concat(NExpression, ",", Ascii), Comment, false, IsFromMacro);

                for (int i = 1; i < Values.Length; i++)
                {
                    target.PrintSourceSubLine(Address + i, Values[i]);
                }
            }

            public override string Beautified()
            {
                return FormatCodeLine(Label, "ASC", string.Concat(NExpression, ",", Ascii), null, Comment);
            }

        }


        private class AssemblyEquLine
            : AssemblyLine
        {
            public AssemblyEquLine(SourceLineRef from, string label, string expression, Assembler.ExpressionBase? fixupFrom, LabelManager? manager, string? comment)
                : base(from, null, comment, label)
            {
                Value = fixupFrom?.Compute();
                if (Value.HasValue)
                    manager?.SetKnownLocation(label, Value.Value);
                else
                {
                    Manager = manager;
                    manager?.RegisterRelocationDependency(FixupNow);
                }
                Expression = expression;
                IsRepeatable = true;    // this will make sure that EQU following REP will throw up!
            }

            public override void ApplyTo(Memory target)
            {
            }

            public override int InstructionOutputSize()
            {
                return 0;
            }

            private void FixupNow()
            {
                if (Value.HasValue)
                    return;
                var x = FixupFrom?.Compute();
                if (x.HasValue)
                {
                    Value = x.Value;
                    Manager?.SetKnownLocation(Label, x.Value);
                }
            }

            int? Value;
            string Expression;

            Assembler.ExpressionBase? FixupFrom;
            LabelManager? Manager;

            public override void CreateOutput(ListingPrinter target)
            {
                target.PrintSourceLine(Address, Value, Label, "EQU", Expression, Comment, false, IsFromMacro);
            }

            public override string Beautified()
            {
                return FormatCodeLine(Label, "EQU", Expression, null, Comment);
            }
        }
        private class AssemblyConstantLine
                : AssemblyLine
        {
            public AssemblyConstantLine(SourceLineRef from, int address, string mnemonic, int nBase, IEnumerable<int> values, string? label, string? comment)
                : base(from, address, comment, label)
            {
                Values = values.ToArray();
                this.nBase = nBase;
                Mnemonic = mnemonic;
                IsRepeatable = true;
            }

            public override int InstructionOutputSize()
            {
                return Values.Length;
            }

            public override void ApplyTo(Memory target)
            {
                // don't change anything!
                for (int i = 0; i < Values.Length; i++)
                {
                    target[Address!.Value + i] = (Values[i] & 0xFFFF);
                }
            }

            private int[] Values;
            private int nBase;
            private string Mnemonic;

            public override void CreateOutput(ListingPrinter target)
            {
                target.PrintSourceLine(Address, Values[0], Label, Mnemonic, string.Join(',', Values.Select(x => Convert.ToString(x, nBase))), Comment, false, IsFromMacro);
                for (int i = 1; i < Values.Length; i++)
                {
                    target.PrintSourceSubLine(Address + i, Values[i]);
                }
            }

            public override string Beautified()
            {
                return FormatCodeLine(Label, Mnemonic, string.Join(',', Values.Select(x => Convert.ToString(x, nBase))), null, Comment);
            }
        }


        private class AssemblyBssLine
            : AssemblyLine
        {
            public AssemblyBssLine(SourceLineRef from, int address, int count, string expression, string? label, string? comment)
                : base(from, address, comment, label)
            {
                Size = count;
                Expression = expression;
                IsRepeatable = true;
            }

            public override int InstructionOutputSize()
            {
                return Size;
            }

            public override void ApplyTo(Memory target)
            {
                // don't change anything!
            }

            private int Size;
            private string Expression;
            public override void CreateOutput(ListingPrinter target)
            {
                target.PrintSourceLine(Address, null, Label, "BSS", Expression, Comment, false, IsFromMacro);
            }
            public override string Beautified()
            {
                return FormatCodeLine(Label, "BSS", Expression, null, Comment);
            }
        }

        private class AssemblyDefLine
                : AssemblyLine
        {
            public AssemblyDefLine(SourceLineRef from, int? address, Assembler.ExpressionBase? fixupFrom, string expression, bool isIndirect, string? label, string? comment)
                : base(from, address, comment, label)
            {
                Value = fixupFrom?.Compute();   // try here...
                if (!Value.HasValue)
                    FixupFrom = fixupFrom;  // try again later...
                IsIndirect = isIndirect;
                Expression = expression;
                IsRepeatable = true;
            }
            private string Expression;
            private bool IsIndirect;
            private Assembler.ExpressionBase? FixupFrom;
            private int? Value;

            public override int InstructionOutputSize()
            {
                return 1;
            }
            public override void ApplyTo(Memory target)
            {
                if (target.Contains(Address.Value))
                {
                    int value = Value ?? FixupFrom?.Compute() ?? throw new InvalidOperationException($"Expression for DEF didn't compute: {Expression}");
                    target[Address.Value] = value;
                }
            }
            public override void CreateOutput(ListingPrinter target)
            {
                target.PrintSourceLine(Address, Value, Label, "DEF", Expression + (IsIndirect ? ",I" : ""), Comment, false, IsFromMacro);
            }

            public override string Beautified()
            {
                return FormatCodeLine(Label, "DEF", Expression, IsIndirect ? "I" : null, Comment);
            }
        }

        private class AssemblyAbsLine
                : AssemblyLine
        {
            public AssemblyAbsLine(SourceLineRef from, int? address, Assembler.ExpressionBase? fixupFrom, string expression, string? label, string? comment)
                : base(from, address, comment, label)
            {
                Value = fixupFrom?.Compute();   // try here...
                if (!Value.HasValue)
                    FixupFrom = fixupFrom;  // try again later...
                Expression = expression;
                IsRepeatable = true;
            }
            private string Expression;
            private Assembler.ExpressionBase? FixupFrom;
            private int? Value;

            public override int InstructionOutputSize()
            {
                return 1;
            }
           
            public override void ApplyTo(Memory target)
            {
                if (target.Contains(Address.Value))
                {
                    int value = Value ?? FixupFrom?.Compute() ?? throw new InvalidOperationException($"Expression for ABS didn't compute: {Expression}");
                    target[Address.Value] = value;
                }
            }
            public override void CreateOutput(ListingPrinter target)
            {
                target.PrintSourceLine(Address, Value, Label, "ABS", Expression, Comment, false, IsFromMacro);
            }
            public override string Beautified()
            {
                return FormatCodeLine(Label, "ABS", Expression, null, Comment);
            }
        }


        private class AssemblyEndLine
            : AssemblyLine
        {
            public AssemblyEndLine(SourceLineRef from, string? comment)
                : base(from, null, comment, null)
            {
            }

            public override void ApplyTo(Memory target)
            {
            }
            public override int InstructionOutputSize()
            {
                return 0;
            }
            public override void CreateOutput(ListingPrinter target)
            {
                target.SuppressOutput = false;
                target.PrintSourceLine(null, null, null, "END", null, Comment, false, IsFromMacro);
            }
            public override string Beautified()
            {
                return FormatCodeLine(Label, "END", null, null, Comment);
            }
        }


        public abstract class AssemblyPreprocessorLine
                : AssemblyLine
        {
            protected AssemblyPreprocessorLine(SourceLineRef from, int? address, string? comment, string? label)
                : base(from, address, comment, label)
            {
            }

            public override void ApplyTo(Memory target)
            {
            }

            public override int InstructionOutputSize()
            {
                return 0;
            }
        }

        public class AssemblyRepLine
                : AssemblyPreprocessorLine
        {
            internal AssemblyRepLine(SourceLineRef from, int? address, int count, string? label, string expression, string? comment = null)
                : base(from, address, comment, label)
            {
                Count = count;
                Expression = expression;
            }

            public int Count { get; private set; }
            private string Expression;

            public override void CreateOutput(ListingPrinter target)
            {
                target.PrintSourceLine(Address, null, Label, "REP", Expression, Comment, false, IsFromMacro);
            }
            public override string Beautified()
            {
                return FormatCodeLine(Label, "REP", Expression, null, Comment);
            }
        }

        private class AssemblyOrgLine
            : AssemblyLine
        {
            public AssemblyOrgLine(SourceLineRef from, int address, bool isOrr, string? expression = null, string? comment = null)
                : base(from, address, comment, null)
            {
                IsOrr = isOrr;
                Expression = expression;
            }

            private bool IsOrr;
            private string? Expression;

            public override void ApplyTo(Memory target)
            {
            }

            public override int InstructionOutputSize()
            {
                return 0;
            }

            public override void CreateOutput(ListingPrinter target)
            {
                target.PrintSourceLine(Address, null, null, IsOrr ? "ORR" : "ORG", Expression, Comment, false, IsFromMacro);
            }
            public override string Beautified()
            {
                return FormatCodeLine(Label, IsOrr ? "ORR" : "ORG", Expression, null, Comment);
            }
        }

        private class AssemblyInstructionLine
            : AssemblyLine
        {
            public AssemblyInstructionLine(SourceLineRef from, int address, int opCode, Assembler.ExpressionBase? fixupFrom, string? label, string mnemonic, string? operand, string? suffix, string? comment = null)
                : base(from, address, comment, label)
            {
                OpCode = opCode;
                Mnemonic = mnemonic;
                Operand = operand;
                Suffix = suffix;
                FixupFrom = fixupFrom;
                IsRepeatable = true;
            }

            Assembler.ExpressionBase? FixupFrom;

            public override int InstructionOutputSize()
            {
                return 1;
            }

            public override void CreateOutput(ListingPrinter target)
            {
                string? op = null;
                if (!string.IsNullOrEmpty(Operand))
                    op = Operand;

                if (!string.IsNullOrWhiteSpace(Suffix))
                    op = (op ?? String.Empty) + "," + Suffix;

                target.PrintSourceLine(Address, OpCode, Label, Mnemonic, op, Comment, false, IsFromMacro);
            }

            public override void ApplyTo(Memory target)
            {
                if (target.Contains(Address.Value))
                {
                    if (FixupFrom != null)
                    {
                        int? value = FixupFrom.Compute();
                        if (!value.HasValue)
                            throw new InvalidOperationException($"Cannot determine value of expression / label: {Operand}");
                        OpCode |= value.Value;
                        FixupFrom = null;
                    }
                    target[Address.Value] = OpCode;
                }
            }

            public int OpCode { get; set; }
            public string Mnemonic { get; set; }
            public string? Operand { get; set; }
            public string? Suffix { get; set; }
            public override string Beautified()
            {
                return FormatCodeLine(Label, Mnemonic, Operand, Suffix, Comment);
            }
        }

        private class AssemblyCommentLine
            : AssemblyLine
        {
            public AssemblyCommentLine(SourceLineRef from, string? comment)
                : base(from, null, comment ?? "", null)
            {
            }

            public override void ApplyTo(Memory target)
            {
                // didn't apply ever...
            }
            public override int InstructionOutputSize()
            {
                return 0;
            }
            public override void CreateOutput(ListingPrinter target)
            {
                target.PrintCommentLine(Comment);
            }
            public override string Beautified()
            {
                return "*" + (Comment ?? "");
            }
        }
#endregion
    }
}