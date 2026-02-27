using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic;

namespace HP9825CPU
{
    public class MemoryManager
    {
        public MemoryManager(bool use16Bit = false, MemoryRange? workingArea = null)
        {
            Use16Bit = use16Bit;
            BackingMemory = Memory.MakeMemory(use16Bit);
            _Mapping = new MemoryType[BackingMemory.Length];
            for(int i = 0;i<_Mapping.Length;i++)
                _Mapping[i] = MemoryType.Missing;
            if (workingArea != null)
                SetRam(workingArea);
        }

        public void SetRam(MemoryRange range)
        {
            if (range.End >= BackingMemory.Length)
                throw new ArgumentOutOfRangeException(nameof(range), range.End, "Momory range is too large for the backing memory!");
            for(int ofs = range.Start; ofs <= range.End;ofs++)
                if (_Mapping[ofs] != MemoryType.Missing) // detect overlapping same type too!
                    throw new InvalidOperationException(string.Format("Cannot set memory at {0}-{1} to be RAM, is alread {2} at {3}.", range.Start, range.End, _Mapping[ofs], ofs));
            for(int ofs = range.Start; ofs <= range.End;ofs++)
                _Mapping[ofs] = MemoryType.Ram;
        }

        public void SetRom(MemoryRange range)
        {
            if (range.End >= BackingMemory.Length)
                throw new ArgumentOutOfRangeException(nameof(range), range.End, "Momory range is too large for the backing memory!");
            for(int ofs = range.Start; ofs <= range.End;ofs++)
                if (_Mapping[ofs] != MemoryType.Missing)    // detect overlapping same type too!
                    throw new InvalidOperationException(string.Format("Cannot set memory at {0}-{1} to be ROM, is alread {2} at {3}.", range.Start, range.End, _Mapping[ofs], ofs));
            for(int ofs = range.Start; ofs <= range.End;ofs++)
                _Mapping[ofs] = MemoryType.Rom;
        }

        public MemoryType GetTypeFor(int address)
        {
            return _Mapping[address];
        }

        /// <summary>
        /// Adds a simulated memory error to the system. Note: multiple specas are cumulative!
        /// </summary>
        /// <param name="startAddess">First affected word.</param>
        /// <param name="endAddress">Last affected word.</param>
        /// <param name="bitMask">The affected bits (1 = faulty, 0 = working)</param>
        /// <param name="mode">The type of fault to simulate.</param>
        public void AddFault(int startAddess, int endAddress, int bitMask, MemoryFaultMode mode)
        {
            if ((bitMask & 0xFFFF)!=0)
            {
                _Faults ??= new List<MemoryFaultDefinition>();
                _Faults.Add(new MemoryFaultDefinition(startAddess, endAddress, bitMask, mode));
            }
        }

        public void LoadSystemRomImage(BinaryReader words, bool bigEndian = true)
        {
            BackingMemory.Load16Bit(words, 0, 12288, bigEndian);
            SetRom(new MemoryRange(0, 12288));
        }

        public void LoadSystemRomImage(BinaryReader fLowBytes, BinaryReader fHighBytes)
        {
            BackingMemory.LoadDual8Bit(fLowBytes, fHighBytes, 0, 12288);
            SetRom(new MemoryRange(0, 12288));
        }

        private void TranslateRomOptions(OptionRom wellknown, out int address, out int length)
        {
            length = 1024;  // most roms are...
            switch(wellknown)
            {
                case OptionRom.ExtendedIO:
                    address = 0x4400;
                    length = 2048;
                    break;
                case OptionRom.Strings:
                    address = 0x4C00;
                    break;
                case OptionRom.AdvancedProgramming:
                    address = 0x4000;
                    break;
                case OptionRom.Matrix:
                case OptionRom.SystemProgramming:
                    address = 0x3C00;
                    break;
                case OptionRom.Plotter:
                    address = 0x3800;
                    break;
                case OptionRom.GeneralIO:
                    address = 0x3400;
                    break;
                case OptionRom.MassMemory:
                    address = 0x3000;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(wellknown), wellknown, "The provided well-known ROM value is unknown!");
            }
        }

        public void LoadOptionPack(OptionRom wellknown, BinaryReader fLowBytes, BinaryReader fHighBytes)
        {
            TranslateRomOptions(wellknown, out int baseAddress, out int length);

            SetRom(new MemoryRange(baseAddress, baseAddress+length-1));
            BackingMemory.LoadDual8Bit(fLowBytes, fHighBytes, baseAddress, length);
        }

        public void LoadOptionPack(OptionRom wellknown, BinaryReader fWords, bool bigEndian = true)
        {
            TranslateRomOptions(wellknown, out int baseAddress, out int length);

            SetRom(new MemoryRange(baseAddress, baseAddress+length-1));
            BackingMemory.Load16Bit(fWords, baseAddress, length, bigEndian);
        }

        public void SetRamConfiguration(RamConfiguration config)
        {
            int baseAddress;
            switch(config)
            {
                case RamConfiguration.Ram8k:
                    baseAddress = 0x7000;
                    break;
                case RamConfiguration.Ram16k:
                    baseAddress = 0x6000;
                    break;
                case RamConfiguration.Ram24k:
                    baseAddress = 0x5000;
                    break;
                case RamConfiguration.Ram32k:
                    baseAddress = 0x4000;
                    break;
                default:
                    throw new NotImplementedException();
            }
            SetRam(new MemoryRange(0x5000, 0x7FFF));
        }

        public int this[int address]
        {
            get 
            {
                if (address < 32)
                    throw new InvalidOperationException();
                switch(GetTypeFor(address))
                {
                    case MemoryType.Missing:
                        return 0xFFFF;
                    case MemoryType.Rom:
                    case MemoryType.Ram:
                        MemoryFaultDefinition ? def;
                        var value = BackingMemory[address];
                        if (_Faults != null)
                        {
                            for(int i = 0; i< _Faults.Count;i++)
                            {
                                def = _Faults[i];
                                if (def.FirstAddress <= address && def.LastAddress >= address)
                                {
                                    value = def.ApplyToValue(value);
                                }
                            }
                        }
                        return value;
                }
                throw new NotImplementedException();
            }
            set 
            {
                if (address < 32)
                    throw new InvalidOperationException();
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

        private MemoryType[] _Mapping;
        
        private List<MemoryFaultDefinition>? _Faults = null;

        public Memory BackingMemory { get; private set; }
        public bool Use16Bit { get; internal set; }
    }
}