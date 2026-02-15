using System;
using System.Collections.Generic;
using System.Linq;

namespace HP9825CPU
{
    public class MemoryManager
    {
        public MemoryManager(bool use16Bit = false, MemoryRange? workingArea = null)
        {
            Use16Bit = use16Bit;
            BackingMemory = Memory.MakeMemory(use16Bit);
            if (workingArea != null)
                _RamRanges.Add(workingArea);
        }

        public void SetRam(MemoryRange range)
        {
            if (range.End >= BackingMemory.Length)
                throw new ArgumentOutOfRangeException(nameof(range), range.End, "Momory range is too large for the backing memory!");
            foreach(var r in _RomRanges)
                if (r.Overlaps(range))
                    throw new InvalidOperationException(string.Format("Cannot set memory to be RAM, is alread ROM. {0}-{1} conflicts with {2}-{3}", range.Start, range.End, r.Start, r.End));
            // ram *can* legally overlap...
            _RamRanges.Add(range);
        }

        public void SetRom(MemoryRange range)
        {
            if (range.End >= BackingMemory.Length)
                throw new ArgumentOutOfRangeException(nameof(range), range.End, "Momory range is too large for the backing memory!");
            foreach(var r in _RamRanges)
                if (r.Overlaps(range))
                    throw new InvalidOperationException(string.Format("Cannot set memory to be RAM, is alread ROM. {0}-{1} conflicts with {2}-{3}", range.Start, range.End, r.Start, r.End));
            // rom *can* legally overlap...
            _RomRanges.Add(range);
        }

        public MemoryType GetTypeFor(int address)
        {
            if (_RamRanges.Any(x=> address >= x.Start && address <= x.End))
                return MemoryType.Ram;
            if (_RomRanges.Any(x=> address >= x.Start && address <= x.End))
                return MemoryType.Rom;
            return MemoryType.Missing;
        }

        public int this[int address]
        {
            get 
            {
                switch(GetTypeFor(address))
                {
                    case MemoryType.Missing:
                        return 0xFFFF;
                    case MemoryType.Rom:
                    case MemoryType.Ram:
                        return BackingMemory[address];
                }
                throw new NotImplementedException();
            }
            set 
            {
                switch(GetTypeFor(address))
                {
                    case MemoryType.Missing:
                    case MemoryType.Rom:
                        return;
                    case MemoryType.Ram:
                        BackingMemory[address] = value;
                        return;
                }
                throw new NotImplementedException();
            }
        }
        
        private List<MemoryRange> _RamRanges = new List<MemoryRange>();
        private List<MemoryRange> _RomRanges = new List<MemoryRange>();
       
        public Memory BackingMemory { get; private set; }
        public bool Use16Bit { get; internal set; }
    }
}