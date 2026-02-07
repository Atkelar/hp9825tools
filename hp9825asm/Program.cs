using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using CommandLineUtils;
using HP9825CPU;

namespace HP9825Assembler
{

    class Program
    {
        static async System.Threading.Tasks.Task<int> Main(string[] args)
        {
            Console.WriteLine("9825 CPU Assembler, {0} (c) 2026 by Atkelar", typeof(Program).Assembly.GetName().Version);

            // the original parameters were defined via the "ASMB," pseudo instructions; we have command line parameters instead...

            ReturnCodeHandler ret = new ReturnCodeHandler();
            var errors = ret.Register<AssemblerExitCodes>();

            ParameterHandler parser = new ParameterHandler("hp9825asm", true);
            var opts = parser.AddOptions<AssemblerContextParameters>();
            var fmt = parser.AddOptions<ListingFormatOptions>("fmt");
            return await ReturnCodeHandler.Run(async () =>
            {
                try
                {
                    if (await parser.LoadFrom("ap9825asm.defaults"))
                        Console.WriteLine("loaded defaults...");
                    await parser.ParseFrom(args);
                    if (!parser.ParsedOk)
                    {
                        Console.WriteLine(parser.GetHelpText(Console.WindowWidth));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(parser.GetHelpText(Console.WindowWidth));
                    throw ReturnCode.UhandledError.Happened(ex.Message);
                }
                if (parser.HelpRequested)
                {
                    Console.WriteLine(parser.GetHelpText(Console.WindowWidth));
                    return;
                }

                bool closeWriter = false;
                TextWriter? sourceTo = null;
                TextReader? input = null;
                try
                {
                    if (!System.IO.File.Exists(opts.InputFile))
                    {
                        throw new InvalidOperationException($"Input file {opts.InputFile} not found!");
                    }
                    input = System.IO.File.OpenText(opts.InputFile);

                    if (!string.IsNullOrWhiteSpace(opts.ListingFile))
                    {
                        if (opts.ListingFile != "-")
                        {
                            closeWriter = true;
                            sourceTo = System.IO.File.CreateText(opts.ListingFile);
                        }
                    }
                    else
                        sourceTo = Console.Out;

                    string? line;

                    List<HP9825CPU.AssemblyLine> lines = new List<HP9825CPU.AssemblyLine>();

                    int lineNumber = 0;
                    HP9825CPU.LabelManager manager = new HP9825CPU.LabelManager();
                    int? repCount = null;

                    ListingPrinter printer = new ListingPrinter(fmt.PageWidth, fmt.PageHeight, fmt.Options, opts.Use16Bit, sourceTo);
                    printer.Filename = System.IO.Path.GetFileName(opts.InputFile);

                    char Condiational = ' ';    // no condition right now...

                    bool firstRep = false;
                    while ((line = input.ReadLine()) != null)
                    {
                        lineNumber++;
                        // we ignore empty lines.
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                        var loc = new SourceLineRef(opts.InputFile, lineNumber);
                        do  // reapeat for any possibly found "repeatable" line...
                        {
                            bool ignoreLine = !(Condiational == ' ' || opts.Conditional.StartsWith(Condiational));
                            var cl = Assembler.Parse(loc, line, manager, null, opts.Use16Bit, false, ignoreLine);
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

                    var eofMark = new SourceLineRef(opts.InputFile, lineNumber);

                    manager.DemandEnded(eofMark);
                    if (repCount.HasValue)
                    {
                        throw eofMark.Error(AssemblerErrorCodes.RepMalformed, "The last statement was a 'REP' but was missing a command to repeat...");
                    }

                    manager.Relocate();
                    var mem = Memory.MakeMemory(opts.Use16Bit);

                    foreach (var cl in lines)
                    {
                        cl.ApplyTo(mem);
                    }

                    foreach (var cl in lines)
                    {
                        cl.CreateOutput(printer);
                    }

                    if (!string.IsNullOrWhiteSpace(opts.BeautyFile))
                    {
                        using (var bf = System.IO.File.CreateText(opts.BeautyFile))
                        {
                            foreach (var cl in lines)
                            {
                                cl.PrintBeautified(bf);
                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(opts.OutputFile) && opts.Length > 0)
                        {
                            if (opts.UseLowHighFiles)
                            {
                                string hFile = System.IO.Path.ChangeExtension(opts.OutputFile, ".high.bin");
                                string lFile = System.IO.Path.ChangeExtension(opts.OutputFile, ".low.bin");
                                using (var fHigh = System.IO.File.Create(hFile))
                                {
                                    using (var fLow = System.IO.File.Create(lFile))
                                    {
                                        using (var bHigh = new System.IO.BinaryWriter(fHigh))
                                        {
                                            using (var bLow = new System.IO.BinaryWriter(fLow))
                                            {
                                                mem.DumpDual8Bit(bLow, bHigh, opts.FromAddress, opts.Length);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                using (var f = System.IO.File.Create(opts.OutputFile))
                                {
                                    using (var b = new System.IO.BinaryWriter(f))
                                    {
                                        mem.Dump16Bit(b, opts.FromAddress, opts.Length);
                                    }
                                }
                            }
                        }
                }
                catch (ParsingException ex)
                {
                    throw errors.Happened(AssemblerExitCodes.AssmeblyError, ex.Message);
                }
                catch (GenericException ex)
                {
                    throw errors.Happened(AssemblerExitCodes.AssmeblyError, ex.Message);
                }
                catch (Exception ex)
                {
    #if DEBUG
                    Console.WriteLine(ex.ToString());
    #else
                    Console.WriteLine(ex.Message);
    #endif
                    throw ReturnCode.UhandledError.Happened(ex.Message);
                }
                finally
                {
                    if (closeWriter)
                        sourceTo?.Dispose();
                }

            });
        }
    }
}