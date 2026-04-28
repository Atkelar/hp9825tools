using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic;

namespace HP9825CPU
{
    /// <summary>
    /// Takes care of mapping different areas of memory to different uses: RAM or ROM mostly, but also "not installed" and "erorrs".
    /// </summary>
    public class MemoryManager
    {
        /// <summary>
        /// Create a new instance of the memory manager and also creates the actual memory buffer.
        /// </summary>
        /// <param name="use16Bit">True to use a 16bit addressing CPU; will give eihter 64k or 128k maximum address space. The 9825 had a 15 bit CPU!</param>
        /// <param name="workingArea">Can be used to pre-select a range as RAM. If not provided, call <see cref="SetRam(MemoryRange)"/> at a later time.</param>
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

        /// <summary>
        /// Declares a portion of memory to be RAM.
        /// </summary>
        /// <param name="range">The range to set aside as RAM.</param>
        /// <exception cref="ArgumentOutOfRangeException">Requested range not covered by the machine capabilities.</exception>
        /// <exception cref="InvalidOperationException">The memory range requested is already set aside for other use!</exception>
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

        /// <summary>
        /// Declares a portion of the memory to be ROM.
        /// </summary>
        /// <param name="range">The range of the address space to dedicate as ROM.</param>
        /// <exception cref="ArgumentOutOfRangeException">Requested range not covered by the machine capabilities.</exception>
        /// <exception cref="InvalidOperationException">The memory range requested is already set aside for other use!</exception>
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

        /// <summary>
        /// Gets the type of address for the provided address.
        /// </summary>
        /// <param name="address">The memory address to probe.</param>
        /// <returns>The type of memory at the poked location.</returns>
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

        /// <summary>
        /// Reads the system ROM image (the first 12k) normally located in the side drawer.
        /// </summary>
        /// <param name="words">The word input stream.</param>
        /// <param name="bigEndian">True to use big-endianess, the default.</param>
        public void LoadSystemRomImage(BinaryReader words, bool bigEndian = true)
        {
            BackingMemory.Load16Bit(words, 0, 12288, bigEndian);
            SetRom(new MemoryRange(0, 12288));
        }

        /// <summary>
        /// Reads the system ROM image (the first 12k) normally located in the side drawer.
        /// </summary>
        /// <param name="fLowBytes">The lower order bytes.</param>
        /// <param name="fHighBytes">The higher order bytes.</param>
        public void LoadSystemRomImage(BinaryReader fLowBytes, BinaryReader fHighBytes)
        {
            BackingMemory.LoadDual8Bit(fLowBytes, fHighBytes, 0, 12288);
            SetRom(new MemoryRange(0, 12288));
        }

        /// <summary>
        /// Translate the well known ROM pack option address space.
        /// </summary>
        /// <param name="wellknown">The ROM to translate.</param>
        /// <param name="address">Receives the target address, as "word" address.</param>
        /// <param name="length">Receives the legnth (in words).</param>
        /// <exception cref="ArgumentOutOfRangeException">Unknown option ROM requested.</exception>
        public static void TranslateRomOptions(OptionRom wellknown, out int address, out int length)
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

        /// <summary>
        /// Reads a ROM image into a predefined location and size.
        /// </summary>
        /// <param name="wellknown">The option ROM identifier.</param>
        /// <param name="fLowBytes">The lower order byte input stream.</param>
        /// <param name="fHighBytes">The higher order byte input stream.</param>
        public void LoadOptionPack(OptionRom wellknown, BinaryReader fLowBytes, BinaryReader fHighBytes)
        {
            TranslateRomOptions(wellknown, out int baseAddress, out int length);

            SetRom(new MemoryRange(baseAddress, baseAddress+length-1));
            BackingMemory.LoadDual8Bit(fLowBytes, fHighBytes, baseAddress, length);
        }

        /// <summary>
        /// Reads a ROM image into a predefined location and size.
        /// </summary>
        /// <param name="wellknown">The option ROM identifier.</param>
        /// <param name="fWords">The word-input stream.</param>
        /// <param name="bigEndian">True to use big-endianess, the default.</param>
        public void LoadOptionPack(OptionRom wellknown, BinaryReader fWords, bool bigEndian = true)
        {
            TranslateRomOptions(wellknown, out int baseAddress, out int length);

            SetRom(new MemoryRange(baseAddress, baseAddress+length-1));
            BackingMemory.Load16Bit(fWords, baseAddress, length, bigEndian);
        }

        /// <summary>
        /// Configure the RAM for a preset of the usual 8k-ranges.
        /// </summary>
        /// <param name="config">The selected RAM configuration.</param>
        /// <exception cref="NotImplementedException">A different (unknown) setting was provided.</exception>
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
            SetRam(new MemoryRange(baseAddress, 0x7FFF));
        }

        /// <summary>
        /// Access memory.
        /// </summary>
        /// <param name="address">The address to interact with.</param>
        /// <returns>The word located at the address;</returns>
        /// <exception cref="InvalidOperationException">The requested address is invalid; Addresses 0-31 are CPU internally handled. This helps catch a misguided CPU internal memory operation.</exception>
        /// <exception cref="NotImplementedException">The provided memory location type is not yet implmented.</exception>
        /// <remarks>
        /// <para>The write operation - if it hits a RAM part! - is always successful. Memmory faults, as defined via the <see cref="AddFault(int, int, int, MemoryFaultMode)"/> method, will only affect read access and create the requested bit fualts then. That way, we can simulate randomized errors.</para>
        /// </remarks>
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