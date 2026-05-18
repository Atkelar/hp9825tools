using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLineUtils;
using HP9825CPU;

namespace HP9825Utils
{
    public class HplOptions
    {
        [Argument("o","OptionRom", HelpText = "Specify a file to include as an option ROM definiton for compile/decompile info.", Syntax = "file.hplcomp")]
        public IEnumerable<string>? OptionRomSource {get;set;}
        [Argument("s","SystemRom", HelpText = "Specify a file to include as a system mainframe ROM definiton for compile/decompile info.", Syntax = "system.hplcomp")]
        public string? SystemRomSource {get;set;}

        internal async Task<HPLCompilerContext?> LoadFiles()
        {
            if (SystemRomSource == null)
                return null;
            var result = new HPLCompilerContext();
            result.MainframeInfo = await HPLCompilerTables.LoadFrom(SystemRomSource); //"system.hplcomp");
            if (OptionRomSource != null)
            {
                foreach(var fn in OptionRomSource)
                {
                    var rom = await HPLCompilerTables.LoadFrom(fn);
                    switch (rom.RomId)
                    {
                        case 6: 
                            rom.ReverseCallback = KnwonHPLCallbacks.StringsReverseCallback;
                            break;
                        case 9:
                            rom.CompileCallback = KnwonHPLCallbacks.AdvancedProgrammingCompileCallback;
                            break;
                    }
                    result.SetPlugInContext(rom);
                }
            }
            return result;
            // context.SetPlugInContext(await HPLCompilerTables.LoadFrom("sysmatha.hplcomp"));
            // context.SetPlugInContext(await HPLCompilerTables.LoadFrom("sysmathb.hplcomp"));
            // context.SetPlugInContext(await HPLCompilerTables.LoadFrom("strings.hplcomp", null, KnwonHPLCallbacks.StringsReverseCallback));
            // context.SetPlugInContext(await HPLCompilerTables.LoadFrom("advpgm.hplcomp", KnwonHPLCallbacks.AdvancedProgrammingCompileCallback));
            // context.SetPlugInContext(await HPLCompilerTables.LoadFrom("genericio.hplcomp"));
            // context.SetPlugInContext(await HPLCompilerTables.LoadFrom("extendedio.hplcomp"));
            // context.SetPlugInContext(await HPLCompilerTables.LoadFrom("plot62.hplcomp"));
            // context.SetPlugInContext(await HPLCompilerTables.LoadFrom("sysprog.hplcomp"));
            // context.SetPlugInContext(await HPLCompilerTables.LoadFrom("matrix.hplcomp"));
        }
    }
}