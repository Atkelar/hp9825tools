using System;
using System.Collections.Generic;

namespace HP9825CPU
{
    public class HPLCompilerContext
    {
        public HPLCompilerTables MainframeInfo { get; set; }
        Dictionary<int, HPLCompilerTables> _RomTables = new Dictionary<int, HPLCompilerTables>();
        public void SetPlugInContext(HPLCompilerTables table, int? overrideRomId = null)
        {
            _RomTables[overrideRomId ?? table.RomId] = table;
        }
        public HPLCompilerTables? GetPlugInContext(int romId)
        {
            // TODO: validate romId
            if (_RomTables.TryGetValue(romId, out var x))
                return x;
            return null;
        }

        internal bool HasRom(int romId)
        {
            return _RomTables.ContainsKey(romId);
        }
    }
}