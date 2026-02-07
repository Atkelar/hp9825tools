using System.Threading.Tasks;
using CommandLineUtils;
using System.IO;
using System;
using HP9825CPU;
using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.VisualBasic;

namespace HP9825Assembler
{
    [Process("asm", "Assemble", HelpMessage = "Create assembly output from an assembly source code.")]
    public class AssemberCommand
        : ProcessBase
    {
        private ReturnCodeGroup<AssemblerExitCodes> Errors;

        public AssemberCommand()
        {
        }

        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            base.BuildReturnCodes(reg);
            Errors = reg.Register<AssemblerExitCodes>();
            return true;
        }

        protected override void BuildArguments(ParameterHandler builder)
        {
            ContextParameters = builder.AddOptions<AssemblerContextParameters>();
            Format = builder.AddOptions<ListingFormatOptions>("fmt");
            builder.AddOptionalDefault("app9825as.defaults");
        }

        AssemblerContextParameters ContextParameters {get;set;}
        ListingFormatOptions Format {get;set;}

        protected override async Task RunNow()
        {
            bool closeWriter = false;
            bool closeXRefWriter = false;
            TextWriter? sourceTo = null;
            TextWriter? crossRefTo = null;
            TextReader? input = null;
            try
            {
                if (!File.Exists(ContextParameters.InputFile))
                {
                    throw Errors.Happened(AssemblerExitCodes.FileNotFound, ContextParameters.InputFile);
                }
                input = System.IO.File.OpenText(ContextParameters.InputFile);

                if (!string.IsNullOrWhiteSpace(ContextParameters.ListingFile))
                {
                    if (ContextParameters.ListingFile != "-")
                    {
                        closeWriter = true;
                        sourceTo = System.IO.File.CreateText(ContextParameters.ListingFile);
                    }
                }
                else
                    sourceTo = Console.Out;

                if (!string.IsNullOrWhiteSpace(ContextParameters.CrossRefFile))
                {
                    if(ContextParameters.CrossRefFile !="-")
                    {
                        closeXRefWriter = true;
                        crossRefTo = System.IO.File.CreateText(ContextParameters.CrossRefFile);
                    }
                }
                else
                    crossRefTo = Console.Out;

                string? line;

                List<AssemblyLine> lines = new List<AssemblyLine>();

                int lineNumber = 0;
                LabelManager manager = new LabelManager();
                int? repCount = null;

                ListingPrinter printer = new ListingPrinter(Format.PageWidth, Format.PageHeight, Format.Options, ContextParameters.Use16Bit, sourceTo, crossRefTo);
                printer.Filename = System.IO.Path.GetFileName(ContextParameters.InputFile);

                char Condiational = ' ';    // no condition right now...

                bool firstRep = false;
                while ((line = input.ReadLine()) != null)
                {
                    lineNumber++;
                    // we ignore empty lines.
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var loc = new SourceLineRef(ContextParameters.InputFile, lineNumber);
                    do  // reapeat for any possibly found "repeatable" line...
                    {
                        bool ignoreLine = !(Condiational == ' ' || ContextParameters.Conditional.StartsWith(Condiational));
                        var cl = Assembler.Parse(loc, line, manager, null, ContextParameters.Use16Bit, false, ignoreLine);
                        if (cl == null) // go on to next line...
                            break;

                        if (repCount.HasValue && cl.IsRepeatable)
                        {
                            if (cl.Label != null)
                                throw loc.Error(AssemblerErrorCodes.RepMalformed, "Repeated instruction may not have a label! Found: {0}", cl.Label);
                            // found a candiadate!
                            if (!firstRep)
                                cl.IsFromMacro = true;
                            firstRep = false;
                            lines.Add(cl);
                            repCount = repCount.Value > 1 ? repCount - 1 : null;  // done wiht the next one...
                        }
                        else
                        {
                            switch (cl)
                            {
                                case AssemblyLine.AssemblyRepLine rep:
                                    if (repCount.HasValue)
                                        throw loc.Error(AssemblerErrorCodes.RepMalformed, "Cannot REP-REP! Previous REP still unfulfilled!");
                                    repCount = rep.Count;   // set up!
                                    firstRep = true;    // make sure we don't mark the first line as "from macro"...
                                    break;
                                case AssemblyLine.AssemblyControlLine ctrl when ctrl.Mnemonic == "IFN" || ctrl.Mnemonic == "IFZ":
                                    if (Condiational != ' ')
                                        throw loc.Error(AssemblerErrorCodes.InvalidConditionalNesting, "Found {0} inside condition {1}!", ctrl.Mnemonic, Condiational);
                                    Condiational = ctrl.Mnemonic[2];
                                    break;
                                case AssemblyLine.AssemblyControlLine ctrl when ctrl.Mnemonic == "XIF":
                                    if (Condiational == ' ')
                                        throw loc.Error(AssemblerErrorCodes.InvalidConditionalNesting, "Found XIF inside condition {0}!", Condiational);
                                    Condiational = ' ';
                                    break;
                            }
                            lines.Add(cl);
                            break;
                        }
                    } while (true);
                }

                var eofMark = new SourceLineRef(ContextParameters.InputFile, lineNumber);

                manager.DemandEnded(eofMark);
                if (repCount.HasValue)
                {
                    throw eofMark.Error(AssemblerErrorCodes.RepMalformed, "The last statement was a 'REP' but was missing a command to repeat...");
                }

                manager.Relocate();
                var mem = Memory.MakeMemory(ContextParameters.Use16Bit);

                foreach (var cl in lines)
                {
                    cl.ApplyTo(mem);
                }

                foreach (var cl in lines)
                {
                    cl.CreateOutput(printer);
                }

                manager.CreateCrossReference(printer, ContextParameters.CrossRefSort == "N");

                if (!string.IsNullOrWhiteSpace(ContextParameters.BeautyFile))
                {
                    using (var bf = System.IO.File.CreateText(ContextParameters.BeautyFile))
                    {
                        foreach (var cl in lines)
                        {
                            cl.PrintBeautified(bf);
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(ContextParameters.OutputFile) && ContextParameters.Length > 0)
                    {
                        if (ContextParameters.UseLowHighFiles)
                        {
                            string hFile = System.IO.Path.ChangeExtension(ContextParameters.OutputFile, ".high.bin");
                            string lFile = System.IO.Path.ChangeExtension(ContextParameters.OutputFile, ".low.bin");
                            using (var fHigh = System.IO.File.Create(hFile))
                            {
                                using (var fLow = System.IO.File.Create(lFile))
                                {
                                    using (var bHigh = new System.IO.BinaryWriter(fHigh))
                                    {
                                        using (var bLow = new System.IO.BinaryWriter(fLow))
                                        {
                                            mem.DumpDual8Bit(bLow, bHigh, ContextParameters.FromAddress, ContextParameters.Length);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            using (var f = System.IO.File.Create(ContextParameters.OutputFile))
                            {
                                using (var b = new System.IO.BinaryWriter(f))
                                {
                                    mem.Dump16Bit(b, ContextParameters.FromAddress, ContextParameters.Length);
                                }
                            }
                        }
                    }
            }
            catch (ParsingException ex)
            {
                throw Errors.Happened(AssemblerExitCodes.AssmeblyError, ex.Message);
            }
            catch (GenericException ex)
            {
                throw Errors.Happened(AssemblerExitCodes.AssmeblyError, ex.Message);
            }
            finally
            {
                if (closeWriter)
                    sourceTo?.Dispose();
                if (closeXRefWriter)
                    crossRefTo?.Dispose();
            }
        }
    }
}