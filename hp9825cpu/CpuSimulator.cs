using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace HP9825CPU
{
    /// <summary>
    /// A simulator for the HP9825A CPU...
    /// </summary>
    /// <remarks>
    /// <para>Yes, I am *that* crazy...</para>
    /// <para>Seriously though: This simulates a machine running the NMOS-II processor, like the HP 9825A. How to use it:</para>
    /// <para>Initialize an instance, based on memory and IO devices, though the <see cref="MemoryManager"/> and <see cref="DeviceManager"/> objects.</para>
    /// <para>Call <see cref="CpuSimulator.Reset"/> - which simulates the reset signal (including IO devices!)</para>
    /// <para>Call <see cref="CpuSimulator.Tick"/> to simulate "a bunch of clocks"... </para>
    /// <para>Monitor the <see cref="CpuSimulator.State"/> to see if we still are running.</para>
    /// </remarks>
    public class CpuSimulator
    {
        /// <summary>
        /// Creates and initializes a new instance.
        /// </summary>
        /// <param name="memory">The memory configuration. RAM and ROM is defined here.</param>
        /// <param name="devices">The IO devices attached to the CPU.</param>
        /// <param name="useRelativeAddressing">True to use relative addressing mode. The HP9825 is hard wired for absolute mode.</param>
        public CpuSimulator(MemoryManager memory, DeviceManager? devices, bool useRelativeAddressing = false)
        {
            UseRelativeAddressing = useRelativeAddressing;
            Handlers = InitHandlers(memory.Use16Bit);
            Memory = memory;
            Devices = devices ?? new DeviceManager(); 
            Devices.HostCpu = this;
            regMasks = memory.Use16Bit ? CpuConstants.RegisterMasks16 : CpuConstants.RegisterMasks15;
            StateMessage = string.Empty;
        }

        public string StateMessage {get; private set;}

        /// <summary>
        /// Gets the current state of the simulator.
        /// </summary>
        public SimulatorState State { get; private set; } = SimulatorState.Created;

        /// <summary>
        /// Gets an approximation of clock cycles since the last reset. NOTE: actual number of clock cycles might differ from a real CPU due to many conditions.
        /// </summary>
        public long Ticks { get; private set; }     // based on page 164++ of the manual

        /// <summary>
        /// The clock frequency, in "Hz" Defaults to 6MHz
        /// </summary>
        public long ClockFrequency { get; set; }    = 6000000;

        /// <summary>
        /// Gets the "virtual" up-time of the CPU.
        /// </summary>
        public TimeSpan? UpTime => State != SimulatorState.Created ? TimeSpan.FromSeconds((double)Ticks / (double)ClockFrequency) : null;

        public MemoryManager Memory { get; private set; }
        public DeviceManager Devices { get; private set; }

        private void WriteRegister(CpuRegister register, int value)
        {
            // TODO: edge cases for floating point number handling: this (register array!) should be moved to the memory manager instead!
            int registerIndex = (int)register;
            if(registerIndex<0 || registerIndex >= registers.Length)
                throw new InvalidOperationException("Not a register index!");
            if (value <0 || value > 0xFFFF)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be 16-bit int!");

            if ((regMasks[registerIndex] & value) != value)
            {
                // invalid value... make sure we notify the caller, push an event...
            }
            if (registerIndex >= 4 && registerIndex <= 7)
            {
                // IO registers...
                Devices.WriteIORegister(registers[9], registerIndex-4, value);
            }
            else
                registers[registerIndex] = value;
        }

        private int ReadRegister(CpuRegister register)
        {
            // TODO: edge cases for floating point number handling: this (register array!) should be moved to the memory manager instead!
            int registerIndex = (int)register;
            if(registerIndex<0 || registerIndex >= registers.Length)
                throw new InvalidOperationException("Not a register index!");
            if (registerIndex >= 4 && registerIndex <= 7)
            {
                // IO registers...
                return Devices.ReadIORegister(registers[9], registerIndex-4);
            }
            else
                return registers[registerIndex];
        }

        // TODO: edge cases for floating point number handling: this (register array!) should be moved to the memory manager instead!
        private int[] registers = new int[CpuConstants.RegisterNames.Length];   // make sure we always align...
        private int[] regMasks;

        // true if the interrupt system is currently enabled.
        private bool InterruptsEnabled = false;

        /// <summary>
        /// The CPU reset signal; Resets the program counter and all internal state registers, sends the reset pulse out to the attached IO devices.
        /// </summary>
        public void Reset()
        {
            StateMessage = string.Empty;
            Ticks = 0;
            for(int i=0;i<registers.Length;i++)
                registers[i] = 0;
            InterruptsEnabled = false;
            DecimalCarry = false;
            OVRegister = false;
            ERegister = false;
            DmaDirection = DmaDirection.Unspecified;
            DmaMode = DmaMode.None;
            PC = 32; // start at octal 40 as per definition...  might set state to breakpoint hit, if defined there...
            Devices.Reset();
            ChangeState(SimulatorState.Reset);
            OnResetted();
        }

        /// <summary>
        /// Read a CPU register.
        /// </summary>
        /// <param name="index">The CPU register to query.</param>
        /// <returns>The value (0-0xFFFF) of the register.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The parameter was not a valid register.</exception>
        public int Register(CpuRegister index)
        {
            if ((int)index < 0 || (int)index > 31)
                throw new ArgumentOutOfRangeException(nameof(index), index, "CPU register undefined!");
            return ReadRegister(index);
        }

        /// <summary>
        /// Writes a register.
        /// </summary>
        /// <param name="index">The CPU register to query.</param>
        /// <param name="value">The value (0-0xFFFF) of the register.</param>
        /// <exception cref="ArgumentOutOfRangeException">The parameter was not a valid register, or the value was outside the 16bit range.</exception>
        public void Register(CpuRegister index, int value)
        {
            if ((int)index < 0 || (int)index > 31)
                throw new ArgumentOutOfRangeException(nameof(index), index, "CPU register undefined!");
            if (value < 0 || value > 0xFFFF)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Register value outside of 16 bit range!");
            WriteRegister(index, value);
        }

        private struct InstructionResult 
        {
            public static InstructionResult Ticks(long ticks)
            {
                return new InstructionResult() { TickCounter = ticks, MoveProgramCounterDelta = 1 };
            }
            public static InstructionResult TicksDelta(long ticks, int delta)
            {
                return new InstructionResult() { TickCounter = ticks, MoveProgramCounterDelta = delta };
            }
            public static InstructionResult TicksAbsolute(long ticks, int newLocation)
            {
                return new InstructionResult() { TickCounter = ticks, MoveProgramCounerAbsolute = newLocation };
            }

            public int MoveProgramCounterDelta;    // defaults: one step forward.
            public int? MoveProgramCounerAbsolute; // if we rather move to somewhere completely differnt.
            public long TickCounter;    // number of ticks required to run the instruction.
        }

        public int ReadMemoryCycles { get; set; } = 4;
        public int WriteMemoryCycles { get; set; } = 4;

        private bool ReadFromAbsoluteAddress(int address, out int value)
        {
            // 15-bit CPU allows for nested-indirection, take that into account here!
            if(address >=0 && address < 32)
            {
                value = ReadRegister((CpuRegister)address);
                return true;
            }

            if (_MemoryBreakpoints.TryGetValue(address, out var bp))
            {
                if (bp.IsEnabled && bp.OnRead)
                    _memBreakHit = bp;
            }
            value = Memory[address];
            return true;
        }
        private bool WriteToAbsoluteAddress(int address, int value)
        {
            // 15-bit CPU allows for nested-indirection, take that into account here!
            if(address>=0 && address < 32)
            {
                WriteRegister((CpuRegister)address, value);
                return true;
            }
            if (_MemoryBreakpoints.TryGetValue(address, out var bp))
            {
                if (bp.IsEnabled && bp.OnWrite)
                    _memBreakHit = bp;
            }
                // done. 
            Memory[address] = value;
            return true;
        }

        public bool UseRelativeAddressing { get; private set; }

        private bool AddressFromRef(int addressRef, bool isIndirect, out int value)
        {
            // first, decode the 11-bit value...
            // 0-31 always registers!
            if (addressRef>=0 && addressRef < 32)    // short hand for register access...
            {
                // adressing a register;
                if (isIndirect)
                {
                    value = ReadRegister((CpuRegister)addressRef);
                    // TODO: indirect addressing with reg->reg-> chain will fail move register storage to memory?
                    if (Memory.Use16Bit || (value & 0x8000) == 0 || value == 0xFFFF)
                        return true;
                    while (true)
                    {
                        value = value & 0x7FFF;
                        value = Memory[value];
                        if ((value & 0x8000) == 0 || value == 0xFFFF)
                            return true;
                    }
                }
                value = addressRef; // oops...
                return true;
            }
            if ((addressRef & 0x400)==0) // yes, base page!
            {
                value = (addressRef & 0x200) != 0 ? (Memory.Use16Bit ? 0xFE00 : 0x7E00) + (addressRef & 0x1FF)  : addressRef;
            }
            else
            {
                // depending on mode...
                if (UseRelativeAddressing)
                {
                    // address is +/- around PC.
                    value = ((addressRef & 0x200) != 0 ? (PC - 0x200) + (addressRef & 0x01FF) : PC + (addressRef & 0x01FF)) & (Memory.Use16Bit ? 0xFFFF : 0x7FFF);
                }
                else
                {
                    // address is absolute in page of PC
                    int page = PC & 0xFC00;
                    value = page | ((addressRef ^ 0x200) & 0x3FF);
                }
            }
            if (isIndirect)
            {
                while (true)
                {
                    if (value < 32)
                        throw new NotImplementedException();
                    value = Memory[value];
                    if (Memory.Use16Bit || (value & 0x8000) == 0 || value == 0xFFFF)
                        return true; 
                    value = value & 0x7FFF;
                }
            }
            return true;
        }



        private long _MeasureIndirections;
        private long _MeasureTicks;

        private void ResetTiming()
        {
            _MeasureIndirections = 0;
            _MeasureTicks = 0;
        }

        private InstructionResult HandleLDAB(int code)
        {
            if (AddressFromRef(code & 0x7FF, (code & 0x8000) != 0, out int value))
            {
                if (ReadFromAbsoluteAddress(value, out value))
                    WriteRegister((code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A, value);
                else
                    Fail($"LD instruction memory access error!");
            }
            else
                Fail($"LD instruction memory access error!");
            return InstructionResult.Ticks(ReadMemoryCycles * (_MeasureIndirections + 2) + _MeasureIndirections + _MeasureTicks);
        }

        private InstructionResult HandleCPAB(int code)
        {
            int delta = 1;
            if (AddressFromRef(code & 0x7FF, (code & 0x8000) != 0, out int value))
            {
                if (ReadFromAbsoluteAddress(value, out value))
                {
                    int cmpWith = ReadRegister((code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A);
                    if (cmpWith != value)
                        delta = 2;
                }
                else
                    Fail($"CP instruction memory access error!");
            }
            else
                Fail($"CP instruction memory access error!");
            return InstructionResult.TicksDelta(ReadMemoryCycles * (_MeasureIndirections + 2) + 4 + _MeasureTicks, delta);
        }

        private bool ERegister;
        private bool OVRegister;

        private InstructionResult HandleADAB(int code)
        {
            if (AddressFromRef(code & 0x7FF, (code & 0x8000) != 0, out int value))
            {
                if (ReadFromAbsoluteAddress(value, out value))
                {
                    var wr = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
                    int addValue = ReadRegister(wr);
                    // carry flags? Validated: It didn't work at all - was ignoring "rolling carry" situations...
                    bool ovr = ((addValue & 0x7FFF) + (value & 0x7FFF) > 0x7FFF); // check if we have a carry situation in bit 14...
                    addValue += value;
                    if (addValue > 0xFFFF)
                    {
                        addValue = addValue & 0xFFFF;
                        // carry...
                        ERegister = true;
                        if (!ovr)
                            OVRegister = true;
                    }
                    else
                    {
                        if (ovr)
                            OVRegister = true;
                    }
                    WriteRegister(wr, addValue);
                }
                else
                    Fail($"AD instruction memory access error!");
            }
            else
                Fail($"AD instruction memory access error!");
            return InstructionResult.Ticks(ReadMemoryCycles * (_MeasureIndirections + 2) + 4 + _MeasureTicks);
        }
        private InstructionResult HandleSTAB(int code)
        {
            int value = ReadRegister((code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A);
            if (AddressFromRef(code & 0x3FF, (code & 0x8000) != 0, out var address))
            {
                WriteToAbsoluteAddress(address, value);
            }
            else
                Fail($"ST instruction memory access error!");
            return InstructionResult.Ticks(ReadMemoryCycles * (_MeasureIndirections + 1) + WriteMemoryCycles + 1 + _MeasureTicks);
        }

        private InstructionResult HandleInterruptCall(int interruptServiceHandlerAddress)
        {
            // copy functionallity of "JSM" for the call
            int rValue = ReadRegister(CpuRegister.R);
            rValue++;
            if (rValue >(Memory.Use16Bit ? 0xFFFF : 0x7FFF))
                Fail($"JSM stack overflow during INT!");
            else
            {
                int handlerAddress = interruptServiceHandlerAddress;
                if(Memory.Use16Bit)
                {
                    if (!ReadFromAbsoluteAddress(interruptServiceHandlerAddress, out handlerAddress))
                    {
                        Fail("INT table malformed!");
                        handlerAddress = -1;
                    }
                }
                else
                {
                    while ((handlerAddress & 0x8000)!=0)
                    {
                        handlerAddress = handlerAddress & 0x7FFF;
                        if (!ReadFromAbsoluteAddress(handlerAddress, out handlerAddress))
                        {
                            Fail("INT indirection fail!");
                            handlerAddress = -1;
                            break;
                        }
                    }
                }
                if(handlerAddress>=0)
                {
                    if (_MemoryBreakpoints.TryGetValue(rValue, out var bp))
                    {
                        if (bp.IsEnabled && bp.OnWrite)
                            _memBreakHit = bp;
                    }
                    Memory[rValue] = PC;
                    WriteRegister(CpuRegister.R, rValue);
                    return InstructionResult.TicksAbsolute(ReadMemoryCycles * (_MeasureIndirections + 1) + WriteMemoryCycles + 5 + _MeasureTicks, handlerAddress);
                }
            }
            return InstructionResult.Ticks(ReadMemoryCycles * (_MeasureIndirections + 1) + WriteMemoryCycles + 5 + _MeasureTicks);
        }

        private InstructionResult HandleJSM(int code)
        {
            if (AddressFromRef(code & 0x7FF, (code & 0x8000) != 0, out int value))
            {
                // return stack: increment...
                int rValue = ReadRegister(CpuRegister.R);
                rValue++;
                if (rValue >(Memory.Use16Bit ? 0xFFFF : 0x7FFF))
                    Fail($"JSM stack overflow!");
                else
                {
                    if (_MemoryBreakpoints.TryGetValue(rValue, out var bp))
                    {
                        if (bp.IsEnabled && bp.OnWrite)
                            _memBreakHit = bp;
                    }
                    Memory[rValue] = PC;
                    WriteRegister(CpuRegister.R, rValue);
                    return InstructionResult.TicksAbsolute(ReadMemoryCycles * (_MeasureIndirections + 1) + WriteMemoryCycles + 5 + _MeasureTicks, value);
                }
            }
            Fail($"JSM instruction memory access error!");
            return InstructionResult.Ticks(ReadMemoryCycles * (_MeasureIndirections + 1) + WriteMemoryCycles + 5 + _MeasureTicks);
        }

        private InstructionResult HandleJMP(int code)
        {
            if (AddressFromRef(code & 0x7FF, (code & 0x8000) != 0, out int value))
            {
                return InstructionResult.TicksAbsolute(ReadMemoryCycles * (_MeasureIndirections + 1) + 2 + _MeasureTicks, value);
            }
            Fail($"JMP instruction memory access error!");
            return InstructionResult.Ticks(ReadMemoryCycles * (_MeasureIndirections + 1) + 2 + _MeasureTicks);
        }
        private InstructionResult HandleISZ(int code)
        {
            // increment skip if zero
            int delta = 1;
            if (AddressFromRef(code & 0x7FF, (code & 0x8000) != 0, out int address))
            {
                if (ReadFromAbsoluteAddress(address, out int value))
                {
                    value++;
                    value = value & 0xFFFF;
                    long temp1 = _MeasureIndirections;
                    long temp2 = _MeasureTicks;
                    ResetTiming();
                    if (WriteToAbsoluteAddress(address, value))
                    {
                        if (value == 0)
                            delta = 2;
                        _MeasureTicks += temp2; // ticks for register access...
                        _MeasureIndirections = temp1;
                    }
                    else
                        Fail($"ISZ instruction memory write access error!");
                }
            }
            else
                Fail($"ISZ instruction memory read access error!");
            return InstructionResult.TicksDelta(ReadMemoryCycles * (_MeasureIndirections + 2) + WriteMemoryCycles + 1 + _MeasureTicks, delta);
        }
        private InstructionResult HandleAND(int code)
        {
            if (AddressFromRef(code & 0x7FF, (code & 0x8000) != 0, out int value))
            {
                if (ReadFromAbsoluteAddress(value, out value))
                {
                    int rValue = ReadRegister(CpuRegister.A);
                    rValue = rValue & value;
                    WriteRegister(CpuRegister.A, rValue);
                }
                else
                    Fail($"AND instruction memory access error!");
            }
            else
                Fail($"AND instruction memory access error!");
            return InstructionResult.Ticks(ReadMemoryCycles * (_MeasureIndirections + 2) + 4 + _MeasureTicks);
        }
        private InstructionResult HandleIOR(int code)
        {
            if (AddressFromRef(code & 0x7FF, (code & 0x8000) != 0, out int value))
            {
                if (ReadFromAbsoluteAddress(value, out value))
                {
                    int rValue = ReadRegister(CpuRegister.A);
                    rValue = rValue | value;
                    WriteRegister(CpuRegister.A, rValue);
                }
                else
                    Fail($"IOR instruction memory access error!");
            }
            else
                Fail($"IOR instruction memory access error!");
            return InstructionResult.Ticks(ReadMemoryCycles * (_MeasureIndirections + 2) + 4 + _MeasureTicks);
        }

        private InstructionResult HandleRET(int code)
        {
            int n = code & 0x3F;
            bool pop =( code & 0x40 ) != 0;
            int rValue = ReadRegister(CpuRegister.R);
            if (_MemoryBreakpoints.TryGetValue(rValue, out var bp))
            {
                if (bp.IsEnabled && bp.OnRead)
                    _memBreakHit = bp;
            }
            int nextBase = Memory[rValue];
            rValue--;
            if (rValue < 0)
            {
                Fail("RET caused stack underflow!");
            }
            else
            {
                WriteRegister(CpuRegister.R, rValue);
                if ((n &0x20)!=0)
                    n = -32 + (n & 0x1F);    // make sure we get the negative part...
                if (pop)        // interrupt handling: TODO!
                {
                    if (this.IODeviceInterruptStack[0].InterruptedFrom != InterruptLevel.None)
                    {   // we have something to pop!
                        WriteRegister(CpuRegister.PA, IODeviceInterruptStack[0].SavedPA);
                        IODeviceInterruptStack[0]= IODeviceInterruptStack[1];
                        IODeviceInterruptStack[1].InterruptedFrom = InterruptLevel.None;
                    }
                }
            }
            return InstructionResult.TicksAbsolute(ReadMemoryCycles * 2 + 4, nextBase + n);
        }

        private InstructionResult HandleEXE(int code)
        {
            if (AddressFromRef(code & 0x1F, (code & 0x8000) != 0, out int value))
            {
                if (ReadFromAbsoluteAddress(value, out value))
                {
                    ExeOpcode = value;
                }
                else
                    Fail($"EXE instruction memory access error!");
            }
            else
                Fail($"EXE instruction memory access error!");
            return InstructionResult.TicksDelta(ReadMemoryCycles * (_MeasureIndirections + 1) + 2 + _MeasureTicks, 0);  // need to stay here until the exe'cd command drives us forward...?
        }
        private InstructionResult HandleDSZ(int code)
        {
            // decrement skip if zero
            int delta = 1;
            if (AddressFromRef(code & 0x7FF, (code & 0x8000) != 0, out int address))
            {
                if (ReadFromAbsoluteAddress(address, out int value))
                {
                    value--;
                    value = value & 0xFFFF;
                    long temp1 = _MeasureIndirections;
                    long temp2 = _MeasureTicks;
                    ResetTiming();
                    if (WriteToAbsoluteAddress(address, value))
                    {
                        if (value == 0)
                            delta = 2;
                        _MeasureTicks += temp2; // ticks for register access...
                        _MeasureIndirections = temp1;
                    }
                    else
                        Fail($"DSZ instruction memory write access error!");
                }
                else
                    Fail($"DSZ instruction memory write access error!");

            }
            else
                Fail($"DSZ instruction memory read access error!");
            return InstructionResult.TicksDelta(ReadMemoryCycles * (_MeasureIndirections + 2) + WriteMemoryCycles + 1 + _MeasureTicks, delta);
        }

        private static int SkipFromValue(int n)
        {
            n = n & 0x3F;
            if ((n & 0x20) != 0)
                return -32+ (n & 0x1F);
            return n;
        }

        private InstructionResult HandleRZAB(int code)
        {
            // Skip if A/B non zero.
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            int value = ReadRegister(r);
            int delta = 1;
            if (value != 0)
            {
                delta = SkipFromValue(code);
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8, delta);
        }
        private InstructionResult HandleRIAB(int code)
        {
            // Increment Skip if A/B non zero.
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            int value = ReadRegister(r);
            int delta = 1;
            if (value != 0)
            {
                delta = SkipFromValue(code);
            }
            WriteRegister(r, (value+1) & 0xFFFF);
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8, delta);
        }
        private InstructionResult HandleSZAB(int code)
        {
            // Skip if A/B zero.
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            int value = ReadRegister(r);
            int delta = 1;
            if (value == 0)
            {
                delta = SkipFromValue(code);
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8, delta);
        }
        private InstructionResult HandleSIAB(int code)
        {
            // Increment Skip if A/B zero.
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            int value = ReadRegister(r);
            int delta = 1;
            if (value == 0)
            {
                delta = SkipFromValue(code);
            }
            WriteRegister(r, (value+1) & 0xFFFF);
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8, delta);
        }
        private InstructionResult HandleSFSC(int code)
        {
            bool isSet = (code & 0x0100)==0;
            int n=1;
            if (FlagActive == isSet)
            {
                n = SkipFromValue(code);
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8,n);
        }
        private InstructionResult HandleSDSC(int code)
        {
            bool isSet = (code & 0x0100) == 0;
            int n=1;
            if (DecimalCarry == isSet)
            {
                n = SkipFromValue(code);
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8,n);
        }
        private InstructionResult HandleSSSC(int code)
        {
            bool isSet = (code & 0x0100)==0;
            int n=1;
            if (StatusActive == isSet)
            {
                n = SkipFromValue(code);
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8,n);
        }
        private InstructionResult HandleSHSC(int code)
        {
            bool isSet = (code & 0x0100)==0;
            int n=1;
            if (HaltActive == isSet)
            {
                n = SkipFromValue(code);
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8,n);
        }
        private InstructionResult HandleSLAB(int code)
        {
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            bool isSet = (code & 0x040)!=0;
            bool isNonHold = (code & 0x080)!=0;
            int n=1;
            var value = ReadRegister(r);
            if ((value & 1)==0)
            {
                n = SkipFromValue(code);
            }
            if (isNonHold)
            {
                if (isSet)
                    value = value | 1;
                else
                    value = value & 0xFFFE;
                WriteRegister(r, value);
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8,n);
        }
        private InstructionResult HandleRLAB(int code)
        {
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            bool isSet = (code & 0x040)!=0;
            bool isNonHold = (code & 0x080)!=0;
            int n=1;
            var value = ReadRegister(r);
            if ((value & 1)!=0)
            {
                n = SkipFromValue(code);
            }
            if (isNonHold)
            {
                if (isSet)
                    value = value | 1;
                else
                    value = value & 0xFFFE;
                WriteRegister(r, value);
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8,n);
        }

        private InstructionResult HandleSABP(int code)
        {
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            bool isSet = (code & 0x040)!=0;
            bool isNonHold = (code & 0x080)!=0;
            int n=1;
            var value = ReadRegister(r);
            if ((value & 0x8000)==0)
            {
                n = SkipFromValue(code);
            }
            if (isNonHold)
            {
                if (isSet)
                    value = value | 0x8000;
                else
                    value = value & 0x7FFF;
                WriteRegister(r, value);
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8, n);
        }
        private InstructionResult HandleSABM(int code)
        {
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            bool isSet = (code & 0x040)!=0;
            bool isNonHold = (code & 0x080)!=0;
            int n=1;
            var value = ReadRegister(r);
            if ((value & 0x8000)!=0)
            {
                n = SkipFromValue(code);
            }
            if (isNonHold)
            {
                if (isSet)
                    value = value | 0x8000;
                else
                    value = value & 0x7FFF;
                WriteRegister(r, value);
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8,n);
        }
        private InstructionResult HandleSOSC(int code)
        {
            bool checkSet = (code & 0x0100)!=0;
            bool isSet = (code & 0x040)!=0;
            bool isNonHold = (code & 0x080)!=0;
            int n=1;
            if (checkSet == OVRegister)
            {
                n = SkipFromValue(code);
            }
            if (isNonHold)
            {
                if (isSet)
                    OVRegister = true;
                else
                    OVRegister = false;
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8,n);
        }
        private InstructionResult HandleSESC(int code)
        {
            bool checkSet = (code & 0x0100)!=0;
            bool isSet = (code & 0x040)!=0;
            bool isNonHold = (code & 0x080)!=0;
            int n=1;
            if (checkSet == ERegister)
            {
                n = SkipFromValue(code);
            }
            if (isNonHold)
            {
                if (isSet)
                    ERegister = true;
                else
                    ERegister = false;
            }
            return InstructionResult.TicksDelta(ReadMemoryCycles + 8,n);
        }
        private InstructionResult HandleTCAB(int code)
        {
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            var value = ReadRegister(r);
            value =(-value) & 0xFFFF;
            WriteRegister(r, value);
            return InstructionResult.Ticks(ReadMemoryCycles + 3);
        }
        private InstructionResult HandleCMAB(int code)
        {
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            var value = ReadRegister(r);
            value =(~value) & 0xFFFF;
            WriteRegister(r, value);
            return InstructionResult.Ticks(ReadMemoryCycles + 3);
        }
        private InstructionResult HandleAABR(int code)
        {
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            var value = ReadRegister(r);
            int n = (code & 0xF) + 1;
            int se = (value & 0x8000) != 0 ? ~0xFFFF : 0;
            value = ((value | se) >> n) & 0xFFFF;
            WriteRegister(r, value);
            return InstructionResult.Ticks(ReadMemoryCycles + 8 + n);
        }
        private InstructionResult HandleSABR(int code)
        {
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            var value = ReadRegister(r);
            int n = (code & 0xF) + 1;
            value = (value >> n) & 0xFFFF;
            WriteRegister(r, value);
            return InstructionResult.Ticks(ReadMemoryCycles + 8 + n);
        }
        private InstructionResult HandleSABL(int code)
        {
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            var value = ReadRegister(r);
            int n = (code & 0xF) + 1;
            value = (value << n) & 0xFFFF;
            WriteRegister(r, value);
            return InstructionResult.Ticks(ReadMemoryCycles + 8 + n);
        }
        private InstructionResult HandleRABR(int code)
        {
            CpuRegister r = (code & 0x800)!=0 ? CpuRegister.B : CpuRegister.A;
            var value = ReadRegister(r);
            int n = (code & 0xF) + 1;
            for(int i = 0;i < n;i++)
            {
                value = (value >> 1) | ((value & 1)!=0 ? 0x8000 : 0);
            }
            WriteRegister(r, value);
            return InstructionResult.Ticks(ReadMemoryCycles + 8 + n);
        }
        private InstructionResult HandleEIR(int code)
        {
            InterruptsEnabled = true;
            return InstructionResult.Ticks(ReadMemoryCycles + 6);
        }
        private InstructionResult HandleDIR(int code)
        {
            InterruptsEnabled = false;
            return InstructionResult.Ticks(ReadMemoryCycles + 6);
        }

        public DmaMode DmaMode {get; set;}
        private DmaDirection _DmaDirection = DmaDirection.Unspecified;
        public DmaDirection DmaDirection { get => _DmaDirection; set { if (Memory.Use16Bit) _DmaDirection = value; else _DmaDirection = DmaDirection.Unspecified; }}

        private InstructionResult HandleDMA(int code)
        {
            DmaMode = DmaMode.Dma;
            return InstructionResult.Ticks(ReadMemoryCycles + 6);
        }
        private InstructionResult HandlePCM(int code)
        {
            DmaMode = DmaMode.PulseCount;
            return InstructionResult.Ticks(ReadMemoryCycles + 6);
        }
        private InstructionResult HandleDDR(int code)
        {
            // TODO: warn on 15bit
            DmaMode= DmaMode.None;
            return InstructionResult.Ticks(ReadMemoryCycles + 6);
        }
        private InstructionResult HandlePlace(int code)
        {
            // PLACE: increment counter BEFORE place...
            CpuRegister counterReg = (code & 8) == 0 ? CpuRegister.C : CpuRegister.D;
            CpuRegister reg = (CpuRegister) (code & 7);
            bool byteOperation = (code & 0x800) != 0;
            bool decrement = (code & 0x80) != 0;

            int address = ReadRegister(counterReg);

            int value = ReadRegister(reg);

            // here, the fun begins... addressing in 15/16 bit mode is quite different...
            if (byteOperation)
            {
                bool lowByte;
                if (Memory.Use16Bit)
                {
                    int extReg = (int)CpuRegister.DMAPA;
                    lowByte = (address & 1) != 0;
                    if ((this.registers[extReg] & (counterReg == CpuRegister.C ? 0x8000 : 0x4000))!=0)
                        address |= 0x10000; // "17th" bit...
                    address += (decrement ? -1 : +1);
                    int realAddress = address >> 1;
                    // left byte is bit 0
                    if (realAddress < 32 || realAddress >= Memory.BackingMemory.Length)
                        Fail($"Invalid address for stack operation. Must not be CPU or outside of memory.");
                    else
                    {
                        if (_MemoryBreakpoints.TryGetValue(realAddress, out var bp))
                        {
                            if (bp.IsEnabled && bp.OnWrite)
                                _memBreakHit = bp;
                        }

                        // put...
                        if (lowByte)
                            Memory[realAddress] = (Memory[realAddress] & 0xFF00) | (value & 0xFF);
                        else
                            Memory[realAddress] = (Memory[realAddress] & 0x00FF) | ((value << 8) & 0xFF00);
                        // update address parts...
                        this.registers[extReg] = (this.registers[extReg] & (counterReg == CpuRegister.C ? 0x7FFF : 0xBFFF)) | ((address & 0x10000) != 0 ? (counterReg == CpuRegister.C ? 0x8000 : 0x4000) : 0);
                        WriteRegister(counterReg, address & 0xFFFF);
                    }
                }
                else
                {
                    // 15bit mode: MSB of address = byte. BUT 0 = right , 1 = left!
                    // increment for put is BEFORE access, so toggle high bit...
                    address = address ^ 0x8000;
                    lowByte = (address & 0x8000) == 0;
                    address = address & 0x7FFF;
                    if (decrement)
                    {
                        if (lowByte)   // one to zero transition in the bit...
                            address--;
                    }
                    else
                    {
                        if (!lowByte)   // zero to one transition in the bit...
                            address++;
                    }
                    if (address < 32 || address >= Memory.BackingMemory.Length)
                        Fail($"Invalid address for stack operation. Must not be CPU or outside of memory.");
                    else
                    {
                        if (_MemoryBreakpoints.TryGetValue(address, out var bp))
                        {
                            if (bp.IsEnabled && bp.OnWrite)
                                _memBreakHit = bp;
                        }

                        if (lowByte)
                            Memory[address] = (Memory[address] & 0xFF00) | (value & 0xFF);
                        else
                            Memory[address] = (Memory[address] & 0x00FF) | ((value << 8) & 0xFF00);
                        // strange thing... but pate 99 says so...
                        address |= lowByte ? 0 : 0x8000;
                        WriteRegister(counterReg, address);
                    }
                }
            }
            else
            {
                if (Memory.Use16Bit)
                {
                    int extReg = (int)CpuRegister.DMAPA;
                    if ((this.registers[extReg] & (counterReg == CpuRegister.C ? 0x8000 : 0x4000))!=0)
                        address |= 0x10000; // "17th" bit...
                    address += (decrement ? -2 : +2);
                    int realAddress = address >> 1;
                    // left byte is bit 0
                    if (realAddress < 32 || realAddress >= Memory.BackingMemory.Length)
                        Fail($"Invalid address for stack operation. Must not be CPU or outside of memory.");
                    else
                    {
                        // put...
                        if (_MemoryBreakpoints.TryGetValue(realAddress, out var bp))
                        {
                            if (bp.IsEnabled && bp.OnWrite)
                                _memBreakHit = bp;
                        }
                        Memory[realAddress] = value;
                        // update address parts...
                        this.registers[extReg] = (this.registers[extReg] & (counterReg == CpuRegister.C ? 0x7FFF : 0xBFFF)) | ((address & 0x10000) != 0 ? (counterReg == CpuRegister.C ? 0x8000 : 0x4000) : 0);
                        WriteRegister(counterReg, address & 0xFFFF);
                    }
                }
                else
                {
                    address += decrement ? -1 : 1;
                    // if we get an address with a leading one here, ingore it!
                    address &=0x7FFF;
                    if (address < 32 || address >= Memory.BackingMemory.Length)
                        Fail($"Invalid address for stack operation. Must not be CPU or outside of memory.");
                    else
                    {
                        if (_MemoryBreakpoints.TryGetValue(address, out var bp))
                        {
                            if (bp.IsEnabled && bp.OnWrite)
                                _memBreakHit = bp;
                        }
                        Memory[address] = value;
                        WriteRegister(counterReg, address);
                    }
                }
            }
            return InstructionResult.Ticks(ReadMemoryCycles + WriteMemoryCycles + 11);
        }
        private InstructionResult HandleWithdraw(int code)
        {
            // withdraw... increment AFTER withdraw operation.
            CpuRegister counterReg = (code & 8) == 0 ? CpuRegister.C : CpuRegister.D;
            CpuRegister reg = (CpuRegister) (code & 7);
            bool byteOperation = (code & 0x800) != 0;
            bool decrement = (code & 0x80) != 0;

            int address = ReadRegister(counterReg);

            // TODO: Check if the read of a byte sets the upper targget bits to zero? Currently assuming "set null".
            //int value = ReadRegister(reg);
            int value = 0;

            // here, the fun begins... addressing in 15/16 bit mode is quite different...
            if (byteOperation)
            {
                bool lowByte;
                if (Memory.Use16Bit)
                {
                    int extReg = (int)CpuRegister.DMAPA;
                    lowByte = (address & 1) != 0;
                    if ((this.registers[extReg] & (counterReg == CpuRegister.C ? 0x8000 : 0x4000))!=0)
                        address |= 0x10000; // "17th" bit...
                    int realAddress = address >> 1;
                    // left byte is bit 0
                    if (realAddress < 32 || realAddress >= Memory.BackingMemory.Length)
                        Fail($"Invalid address for stack operation. Must not be CPU or outside of memory.");
                    else
                    {
                        if (_MemoryBreakpoints.TryGetValue(realAddress, out var bp))
                        {
                            if (bp.IsEnabled && bp.OnRead)
                                _memBreakHit = bp;
                        }

                        // put...
                        if (lowByte)
                            value = (value & 0xFF00) | (Memory[realAddress] & 0xFF);
                        else
                            value = (value & 0xFF00) | ((Memory[realAddress] >> 8) & 0xFF);
                        address += (decrement ? -1 : +1);
                        // update address parts...
                        this.registers[extReg] = (this.registers[extReg] & (counterReg == CpuRegister.C ? 0x7FFF : 0xBFFF)) | ((address & 0x10000) != 0 ? (counterReg == CpuRegister.C ? 0x8000 : 0x4000) : 0);
                        WriteRegister(counterReg, address & 0xFFFF);
                    }
                }
                else
                {
                    // 15bit mode: MSB of address = byte. BUT 0 = right , 1 = left!
                    lowByte = (address & 0x8000) == 0;
                    address = address & 0x7FFF;
                    if (address < 32 || address >= Memory.BackingMemory.Length)
                        Fail($"Invalid address for stack operation. Must not be CPU or outside of memory.");
                    else
                    {
                        if (_MemoryBreakpoints.TryGetValue(address, out var bp))
                        {
                            if (bp.IsEnabled && bp.OnRead)
                                _memBreakHit = bp;
                        }
                        if (lowByte)
                            value = (value & 0xFF00) | (Memory[address] & 0xFF);
                        else
                            value = (value & 0xFF00) | ((Memory[address] >> 8) & 0xFF);
                        // strange thing... but pate 99 says so...
                        lowByte = !lowByte;
                        if (decrement)
                        {
                            if (lowByte)   // one to zero transition in the bit...
                                address--;
                        }
                        else
                        {
                            if (!lowByte)   // zero to one transition in the bit...
                                address++;
                        }
                        address |= lowByte ? 0 : 0x8000;
                        WriteRegister(counterReg, address);
                    }
                }
            }
            else
            {
                if (Memory.Use16Bit)
                {
                    int extReg = (int)CpuRegister.DMAPA;
                    if ((this.registers[extReg] & (counterReg == CpuRegister.C ? 0x8000 : 0x4000))!=0)
                        address |= 0x10000; // "17th" bit...
                    int realAddress = address >> 1;
                    // left byte is bit 0
                    if (realAddress < 32 || realAddress >= Memory.BackingMemory.Length)
                        Fail($"Invalid address for stack operation. Must not be CPU or outside of memory.");
                    else
                    {
                        // put...
                        if (_MemoryBreakpoints.TryGetValue(realAddress, out var bp))
                        {
                            if (bp.IsEnabled && bp.OnRead)
                                _memBreakHit = bp;
                        }
                        value = Memory[realAddress];
                        address += (decrement ? -2 : +2);
                        // update address parts...
                        this.registers[extReg] = (this.registers[extReg] & (counterReg == CpuRegister.C ? 0x7FFF : 0xBFFF)) | ((address & 0x10000) != 0 ? (counterReg == CpuRegister.C ? 0x8000 : 0x4000) : 0);
                        WriteRegister(counterReg, address & 0xFFFF);
                    }
                }
                else
                {
                    if (address < 32 || address >= Memory.BackingMemory.Length)
                        Fail($"Invalid address for stack operation. Must not be CPU or outside of memory.");
                    else
                    {
                        if (_MemoryBreakpoints.TryGetValue(address, out var bp))
                        {
                            if (bp.IsEnabled && bp.OnRead)
                                _memBreakHit = bp;
                        }
                        value = Memory[address];
                        address += decrement ? -1 : 1;
                        WriteRegister(counterReg, address);
                    }
                }
            }

            WriteRegister(reg, value);

            return InstructionResult.Ticks(ReadMemoryCycles + WriteMemoryCycles + 11);
        }

        private InstructionResult HandleCRL(int code)
        {
            int n = (code & 0xF) + 1;
            int address = ReadRegister(CpuRegister.A);
            Debug.WriteLine("CRL - {0} - {1}*", Convert.ToString(address, 8), n);
            for(int i = 0;i<n;i++)
            {
                if (_MemoryBreakpoints.TryGetValue(address, out var bp))
                {
                    if (bp.IsEnabled && bp.OnWrite)
                        _memBreakHit = bp;
                }
                if (address<32)
                    registers[address] = 0;
                else
                    Memory[address] = 0;
                address++;
            }
            return InstructionResult.Ticks(ReadMemoryCycles + WriteMemoryCycles * n + 10);
        }
        private InstructionResult HandleXFR(int code)
        {
            int n = (code & 0xF) + 1;
            int address1 = ReadRegister(CpuRegister.A);
            int address2 = ReadRegister(CpuRegister.B);
            Debug.WriteLine("XFR - {0} -> {1} - {2}*", Convert.ToString(address1, 8), Convert.ToString(address2, 8), n);
            for(int i = 0;i<n;i++)
            {
                if (_MemoryBreakpoints.TryGetValue(address1, out var bp))
                {
                    if (bp.IsEnabled && bp.OnRead)
                        _memBreakHit = bp;
                }
                if (_MemoryBreakpoints.TryGetValue(address2, out bp))
                {
                    if (bp.IsEnabled && bp.OnWrite)
                        _memBreakHit = bp;
                }
                var val = address1 < 32 ? registers[address1] : Memory[address1];
                if (address2 < 32)
                    registers[address2] = val;
                else
                    Memory[address2] = val;
                address1++;
                address2++;
            }
            return InstructionResult.Ticks(ReadMemoryCycles * (n + 1) + n * WriteMemoryCycles + 15);
        }
        private InstructionResult HandleMRX(int code)
        {
            // mantissa right shifr of AR1...
            // n-count is B & 0xF, rotate A &0xF through first/last digits...
            int aReg = ReadRegister(CpuRegister.A);
            int shiftCount = ReadRegister(CpuRegister.B) & 0xF; // note: 0-15 is valid and will be counted.
            FloatingPointNumber ar1 = ReadAR1();
            Debug.Write(string.Format("MRX - {0} - {1}* - ", ar1, shiftCount));
            if (shiftCount > 0)
            {
                int stuffValue = aReg & 0xF;
                if (stuffValue > 9)
                {
                    Fail("MRX tried to stuff more than 9 into BCD number!");
                }
                else
                {
                    int[] digits = ar1.GetMantissa();
                    int lastOut=0;
                    for(int i=0;i<shiftCount;i++)
                    {
                        lastOut = digits[11];
                        for(int digit = 12; digit > 1; digit--)
                        {
                            digits[digit] = digits[digit-1];
                        }
                        digits[0] = stuffValue;
                        stuffValue = 0;
                    }
                    WriteRegister(CpuRegister.A, lastOut);
                    WriteRegister(CpuRegister.SE, lastOut);
                    ar1.PutMantissa(digits);
                    WriteAR1(ar1);
                    Debug.WriteLine(ar1);
                }
            }
            else
            {
                WriteRegister(CpuRegister.SE, aReg & 0xF);
            }
            DecimalCarry = false;

            return InstructionResult.Ticks(shiftCount== 0 ? ReadMemoryCycles + 20 : 4*ReadMemoryCycles + 3*WriteMemoryCycles + 4*shiftCount + 20);
        }

        public int Ar1Address => Memory.Use16Bit ? 0xFFF8 : 0x7FF8;

        public FloatingPointNumber ReadAR1()
        {
            return FloatingPointNumber.FromMemory(Memory, Ar1Address);
        }
        public void WriteAR1(FloatingPointNumber num)
        {
            num.WriteTo(Memory, Ar1Address);
        }

        public FloatingPointNumber ReadAR2()
        {
            int baseAddress = (int)CpuRegister.AR2;
            return FloatingPointNumber.FromParts(this.registers[baseAddress], this.registers[baseAddress+1], this.registers[baseAddress+2], this.registers[baseAddress+3]);
        }
        public void WriteAR2(FloatingPointNumber num)
        {
            int baseAddress = (int)CpuRegister.AR2;
            this.registers[baseAddress] = num.M;
            this.registers[baseAddress+1] = num.M1;
            this.registers[baseAddress+2] = num.M2;
            this.registers[baseAddress+3] = num.M3;
        }


        private InstructionResult HandleDRS(int code)
        {
            // mantissa right shift of AR1...
            // n-count is 1 in 0 through first/last digits...
            FloatingPointNumber ar1 = ReadAR1();
            Debug.Write(string.Format("DRS - {0} - ", ar1));
            int[] digits = ar1.GetMantissa();
            int lastOut=digits[11];
            for(int digit = 11; digit > 0 ; digit--)
            {
                digits[digit] = digits[digit-1];
            }
            digits[0] = 0;
            WriteRegister(CpuRegister.A, lastOut);
            WriteRegister(CpuRegister.SE, lastOut);
            ar1.PutMantissa(digits);
            WriteAR1(ar1);
            Debug.WriteLine(ar1);
            DecimalCarry = false;
            return InstructionResult.Ticks(4*ReadMemoryCycles + 3*WriteMemoryCycles + 14);        
        }
        private InstructionResult HandleMLY(int code)
        {
            // mantissa left shifr of AR2...
            // n-count is 1 rotate A &0xF through first/last digits...
            int aReg = ReadRegister(CpuRegister.A);
            FloatingPointNumber ar2 = ReadAR2();
            Debug.Write(string.Format("MLY - {0} - ", ar2));
            int stuffValue = aReg & 0xF;
            if (stuffValue > 9)
            {
                Fail("MLY tried to stuff more than 9 into BCD number!");
            }
            else
            {
                int[] digits = ar2.GetMantissa();
                int lastOut=digits[0];
                for(int digit = 0; digit < 11 ; digit++)
                {
                    digits[digit] = digits[digit+1];
                }
                digits[11] = stuffValue;
                WriteRegister(CpuRegister.A, lastOut);
                WriteRegister(CpuRegister.SE, lastOut);
                ar2.PutMantissa(digits);
                WriteAR2(ar2);
                Debug.WriteLine(ar2);
                DecimalCarry = false;
            }

            return InstructionResult.Ticks(ReadMemoryCycles + 26);
        }
        private InstructionResult HandleMRY(int code)
        {
            // mantissa right shifr of AR2...
            // n-count is B & 0xF, rotate A &0xF through first/last digits...
            int aReg = ReadRegister(CpuRegister.A);
            int shiftCount = ReadRegister(CpuRegister.B) & 0xF; // note: 0-15 is valid and will be counted.
            FloatingPointNumber ar2 = ReadAR2();
            Debug.Write(string.Format("MRY - {0} - {1}* - ", ar2, shiftCount));
            if (shiftCount > 0)
            {
                int stuffValue = aReg & 0xF;
                if (stuffValue > 9)
                {
                    Fail("MRY tried to stuff more than 9 into BCD number!");
                }
                else
                {
                    int[] digits = ar2.GetMantissa();
                    int lastOut=0;
                    for(int i=0;i<shiftCount;i++)
                    {
                        lastOut = digits[11];
                        for(int digit = 11; digit > 0; digit--)
                        {
                            digits[digit] = digits[digit-1];
                        }
                        digits[0] = stuffValue;
                        stuffValue = 0;
                    }
                    WriteRegister(CpuRegister.A, lastOut);
                    WriteRegister(CpuRegister.SE, lastOut);
                    ar2.PutMantissa(digits);
                    WriteAR2(ar2);
                    Debug.WriteLine(ar2);
                    DecimalCarry = false;
                }
            }
            else    // TODO: validate. The manual is not precise enough; also MRX!
            {
                WriteRegister(CpuRegister.SE, aReg & 0xF);
            }
            DecimalCarry = false;

            return InstructionResult.Ticks(shiftCount == 0 ? ReadMemoryCycles+20 : ReadMemoryCycles + 4*shiftCount + 27);
        }
        private InstructionResult HandleNRM(int code)
        {
            // normalize AR2...
            FloatingPointNumber ar2 = ReadAR2();
            Debug.Write(string.Format("NRM - {0} - ", ar2));
            int numShifts = 0;
            int[] digits = ar2.GetMantissa();
            while (digits[0]==0 && numShifts<12)
            {
                numShifts++;
                for(int digit = 0; digit < 11 ; digit++)
                {
                    digits[digit] = digits[digit+1];
                }
                digits[11]=0;
            }

            WriteRegister(CpuRegister.B, numShifts);
            ar2.PutMantissa(digits);
            WriteAR2(ar2);
            Debug.WriteLine("{0} - {1}*", ar2, numShifts);
            DecimalCarry = numShifts > 11;

            return InstructionResult.Ticks(numShifts < 12 ? ReadMemoryCycles + numShifts + 17 : ReadMemoryCycles + 63);
        }
        private InstructionResult HandleFXA(int code)
        {
            // Mantissa addition: AR1+AR2, but mantissa only! Set DC for overflow.
            var ar2 = ReadAR2();
            var ar1 = ReadAR1();
            var m1 = ar1.GetMantissa();
            var m2 = ar2.GetMantissa();
            // page 105
            int carry = DecimalCarry ? 1 : 0;
            Debug.Write(string.Format("FXA - {0} - {1} - {2} - ", ar1, ar2, carry));

            for(int i = 11; i >=0;i--)
            {
                int sum = carry + m1[i] + m2[i];
                if (sum > 9)
                {
                    carry = 1;
                    sum -= 10;
                }
                else
                    carry = 0;
                m2[i] = sum;
            }

            DecimalCarry = carry > 0;
            ar2.PutMantissa(m2);
            WriteAR2(ar2);
            Debug.WriteLine("{0} - {1}", ar2, carry);
            return InstructionResult.Ticks(4*ReadMemoryCycles + 16);
        }
        private InstructionResult HandleMWA(int code)
        {
            // Mantissa word addition: B+AR2, but mantissa only! Set DC for overflow.
            var ar2 = ReadAR2();
            var b = ReadRegister(CpuRegister.B);

            var m1 = new int[12];
            m1[11] = b & 0xF;
            m1[10] = (b & 0xF0)>>4;
            m1[9] = (b & 0xF00)>>8;
            m1[8] = (b & 0xF000)>>12;
            var m2 = ar2.GetMantissa();
            // page 105
            int carry = DecimalCarry ? 1 : 0;
            Debug.Write(string.Format("MWA - {0} - {1:x4} {2}{3}{4}{5} - {6} - ", ar2, b, m1[8],m1[9],m1[10],m1[11] , carry));

            for(int i = 11; i >=0;i--)
            {
                int sum = carry + m1[i] + m2[i];
                if (sum > 9)
                {
                    carry = 1;
                    sum -= 10;
                }
                else
                    carry = 0;
                m2[i] = sum;
            }

            DecimalCarry = carry > 0;
            ar2.PutMantissa(m2);
            WriteAR2(ar2);
            Debug.WriteLine(" - {0} - {1}", ar2, carry);
            return InstructionResult.Ticks(ReadMemoryCycles + 22);
        }
        private InstructionResult HandleCMX(int code)
        {
            // 10's complement of AR1 - DC = 0
            var ar1 = ReadAR1();
            var m = ar1.GetMantissa();
            // page 69
            int bNum = 10;
            Debug.Write(string.Format("CMX - {0} - ", ar1));
            for(int i=11;i>=0;i--)
            {
                if (bNum < 10 || m[i]!=0)
                {
                    m[i] = bNum - m[i];
                    bNum=9;
                }
            }
            DecimalCarry = false;
            ar1.PutMantissa(m);
            WriteAR1(ar1);
            Debug.WriteLine(ar1);
            return InstructionResult.Ticks(4*ReadMemoryCycles + 4*WriteMemoryCycles + 17);
        }
        private InstructionResult HandleCMY(int code)
        {
            // 10's complement of AR2 - DC = 0
            var ar2 = ReadAR2();
            var m = ar2.GetMantissa();
            // page 69
            int bNum = 10;
            Debug.Write(string.Format("CMY - {0} - ", ar2));
            for(int i=11;i>=0;i--)
            {
                if (bNum < 10 || m[i]!=0)
                {
                    m[i] = bNum - m[i];
                    bNum=9;
                }
            }
            DecimalCarry = false;
            ar2.PutMantissa(m);
            WriteAR2(ar2);
            Debug.WriteLine(ar2);
            return InstructionResult.Ticks(ReadMemoryCycles+17);
        }
        private InstructionResult HandleFMP(int code)
        {
            // Fast Multiply Mantissa: AR2+AR1*B, but mantissa only! Set DC for overflow.
            int count = ReadRegister(CpuRegister.B) & 0xF;
            var ar2 = ReadAR2();
            var ar1 = ReadAR1();
            var m1 = ar1.GetMantissa();
            var m2 = ar2.GetMantissa();
            // page 105
            int carry = DecimalCarry ? 1 : 0;
            Debug.Write(string.Format("FMP - {0} - {1} - {2} - ", ar1, ar2, carry));

            int overflows = 0;
            for(int cnt = 0; cnt<count;cnt++)
            {
                for(int i = 11; i >=0;i--)
                {
                    int sum = carry + m1[i] + m2[i];
                    if (sum > 9)
                    {
                        carry = 1;
                        sum -= 10;
                    }
                    else
                        carry = 0;
                    m2[i] = sum;
                }
                if (carry > 0)
                {
                    overflows++;
                }
                carry = 0;  // we only use the DC for the first addition...
            }
            DecimalCarry=false;

            DecimalCarry = carry > 0;
            ar2.PutMantissa(m2);
            WriteAR2(ar2);
            WriteRegister(CpuRegister.A, overflows);
            Debug.WriteLine(" {0} - {1} - {2}", ar2, carry, overflows);

            return InstructionResult.Ticks(count == 0 ? ReadMemoryCycles + 28 : 4 * ReadMemoryCycles + 13 * count + 18);
        }
        private InstructionResult HandleFDV(int code)
        {
            // Fast Divide Mantissa: , but mantissa only! Set DC for overflow.
            var ar2 = ReadAR2();
            var ar1 = ReadAR1();
            var m1 = ar1.GetMantissa();
            var m2 = ar2.GetMantissa();
            int count = 0;
            int carry;
            carry = DecimalCarry ? 1 : 0;
            Debug.Write(string.Format("FDV {0} - {1} - {2} - ", ar1, ar2, carry));
            // page 105
            if (ar1.IsZero)
                Fail("FDV had a divide by zero condition. AR1 is zero.");
            else
            {
                // TODO: validate: spec on page 106 is a bit unclear. Will DC be reset after each round or not?
                do
                {
                    for(int i = 11; i >=0;i--)
                    {
                        int sum = carry + m1[i] + m2[i];
                        if (sum > 9)
                        {
                            carry = 1;
                            sum -= 10;
                        }
                        else
                            carry = 0;
                        m2[i] = sum;
                    }
                    if (carry == 0)
                    {
                        count++;
                    }
                } while (carry == 0);

                DecimalCarry=false;
                ar2.PutMantissa(m2);
                WriteAR2(ar2);
                Debug.WriteLine("{0}, {1}", ar2, count);
                WriteRegister(CpuRegister.B, count);
            }

            return InstructionResult.Ticks(4*ReadMemoryCycles + 13 * count + 13);
        }
        private InstructionResult HandleMPY(int code)
        {
            // "Booth's Algorithm" - take A*B and build a 32bit BA combo..
            // I'm reasonably sure, that this is just a simple signed 32 bit integer multiply....
            // TODO: check if flags are affected, no mention on page 106!

            int a = ReadRegister(CpuRegister.A);
            int b = ReadRegister(CpuRegister.B);
            // need to "sign extend" a and b...
            if ((a & 0x8000) != 0)
            {
                a = -32768 + (a & 0x7FFF);
            }
            if ((b & 0x8000) != 0)
            {
                b = -32768 + (b & 0x7FFF);
            }
            int result = a * b;
            WriteRegister(CpuRegister.A, result & 0xFFFF);
            WriteRegister(CpuRegister.B, (result >> 16) & 0xFFFF);

            // TODO: timing is complex, "T" factor unknown... assuming 4, so 8 as a result...
            return InstructionResult.Ticks(ReadMemoryCycles + 8 +59);
        }
        private InstructionResult HandleCDC(int code)
        {
            DecimalCarry = false;
            return InstructionResult.Ticks(ReadMemoryCycles + 5);
        }

        private InstructionResult HandleSDO(int code)
        {
            DmaDirection = DmaDirection.MemoryToIO;
            return InstructionResult.Ticks(ReadMemoryCycles +  5);
        }
        private InstructionResult HandleSDI(int code)
        {
            DmaDirection = DmaDirection.IOToMemory;
            return InstructionResult.Ticks(ReadMemoryCycles + 5);
        }
        private InstructionResult HandleDBL(int code)
        {
            int extReg = (int)CpuRegister.DMAPA;
            this.registers[extReg] &= 0xBFFF;
            return InstructionResult.Ticks(ReadMemoryCycles + 6);
        }
        private InstructionResult HandleCBL(int code)
        {
            int extReg = (int)CpuRegister.DMAPA;
            this.registers[extReg] &= 0x7FFF;
            return InstructionResult.Ticks(ReadMemoryCycles + 6);
        }
        private InstructionResult HandleDBU(int code)
        {
            int extReg = (int)CpuRegister.DMAPA;
            this.registers[extReg] |= 0x4000;
            return InstructionResult.Ticks(ReadMemoryCycles + 6);
        }
        private InstructionResult HandleCBU(int code)
        {
            int extReg = (int)CpuRegister.DMAPA;
            this.registers[extReg] |= 0x8000;
            return InstructionResult.Ticks(ReadMemoryCycles + 6);
        }

        private Dictionary<int, Func<int, InstructionResult>> InitHandlers(bool is16bit)
        {
            var handlers = new Dictionary<int, Func<int, InstructionResult>>()
            {
                // memory access group
                { 0x0000, HandleLDAB }, // LDA, LDB 
                { 0x1000, HandleCPAB }, // CPA, CPB
                { 0x2000, HandleADAB }, // ADA, ADB
                { 0x3000, HandleSTAB }, // STA, STB
                { 0x4000, HandleJSM }, // JSM
                { 0x4800, HandleISZ }, // ISZ
                { 0x5000, HandleAND }, // AND
                { 0x5800, HandleDSZ }, // DSZ
                { 0x6000, HandleIOR }, // IOR
                { 0x6800, HandleJMP }, // JMP
                { 0x7000, HandleEXE }, // EXE
                { 0xF080, HandleRET }, // RET
                { 0x7400, HandleRZAB }, // RZA, RZB
                { 0x7440, HandleRIAB }, // RIA, RIB
                { 0x7500, HandleSZAB }, // SZA, SZB
                { 0x7540, HandleSIAB }, // SIA, SIB
                { 0x7480, HandleSFSC }, // SFS, SFC
                { 0x74C0, HandleSDSC }, // SDS, SDC
                { 0x7C80, HandleSSSC }, // SSS, SSC
                { 0x7CC0, HandleSHSC }, // SHS, SHC
                { 0x7600, HandleSLAB }, // SLA, SLB
                { 0x7700, HandleRLAB }, // RLA, RLB
                { 0xF400, HandleSABP }, // SAP, SBP
                { 0xF500, HandleSABM }, // SAM, SBM
                { 0xF600, HandleSOSC }, // SOC, SOS
                { 0xFE00, HandleSESC }, // SEC, SES
                { 0xF020, HandleTCAB }, // TCA, TCB
                { 0xF060, HandleCMAB }, // SMA, SMB
                { 0xF100, HandleAABR }, // AAR, ABR
                { 0xF140, HandleSABR }, // SAR, SBR
                { 0xF14F, HandleSABR }, // CLA
                { 0xF94F, HandleSABR }, // CLB
                { 0xF180, HandleSABL }, // SAL, SBL
                { 0xF1C0, HandleRABR }, // RAR, RBR
                { 0x7110, HandleEIR },  // I/O Group
                { 0x7118, HandleDIR },
                { 0x7120, HandleDMA },
                { 0x7128, HandlePCM },
                { 0x7138, HandleDDR },
                { 0x7160, HandlePlace },
                { 0x7170, HandleWithdraw },
                { 0x7380, HandleCRL },
                { 0x7300, HandleXFR },
                { 0x7B00, HandleMRX },
                { 0x7B21, HandleDRS },
                { 0x7B61, HandleMLY },
                { 0x7B40, HandleMRY },
                { 0x7340, HandleNRM },
                { 0x7280, HandleFXA },
                { 0x7200, HandleMWA },
                { 0x7260, HandleCMX },
                { 0x7220, HandleCMY },
                { 0x7A00, HandleFMP },
                { 0x7A21, HandleFDV },
                { 0x7B8F, HandleMPY },
                { 0x73C0, HandleCDC }
            };
            if (is16bit)
            {
                // add 16bit only instructions...
                handlers.Add(0x7100, HandleSDO);
                handlers.Add(0x7108, HandleSDI);
                handlers.Add(0x7140, HandleDBL);
                handlers.Add(0x7148, HandleCBL);
                handlers.Add(0x7150, HandleDBU);
                handlers.Add(0x7158, HandleCBU);
            }
            return handlers;
        }
        private Dictionary<int, Func<int, InstructionResult>> Handlers;


        /// <summary>
        /// Gets a disassembly of the "current" instruction or one from another memory location, if provided.
        /// </summary>
        /// <param name="label">The label to include, null for none.</param>
        /// <param name="location">The memory location to disassemble. Can be null to indicate either current <see cref="PC"/> or the pending opcode for the running <see cref="IsInExecute"/> instruction.</param>
        /// <param name="comment">The comment to include, null for none.</param>
        /// <returns>The disassembled source instruction.</returns>
        public string Disasssemble(string? label= null, int? location= null, string? comment = null)
        {
            var opCode = location.HasValue ? Memory[location.Value] : ExeOpcode.GetValueOrDefault(Memory[PC]);
            location ??= PC;
            var line = Disassembler.Disassemble(opCode, location.Value, label, includeDefaults: true);
            return line.Beautified();
        }

        private int? ExeOpcode;

        /// <summary>
        /// True, if the next pending instruction is the content of an "EXE" instruction.
        /// </summary>
        public bool IsInExecute => ExeOpcode.HasValue;

        internal bool DecimalCarry {get;set;}

        /// <summary>
        /// True to indicate an active (grounded!) "Flag" input.
        /// </summary>
        public bool FlagActive {get;set;}

        /// <summary>
        /// True to indicate an active (grounded!) "Halt" input.
        /// </summary>
        public bool HaltActive {get;set;}

        /// <summary>
        /// True to indicate an active (grounded!) "Status" input.
        /// </summary>
        public bool StatusActive {get;set;}


        /// <summary>
        /// Gets/sets the current program counter position. This is the NEXT instruction that will be handled by the <see cref="Tick"/> call.
        /// </summary>
        public int PC
        {
            get => ReadRegister(CpuRegister.P);
            set
            {
                if (value < 0 || (value > (Memory.Use16Bit ? 0xFFFF : 0x07FFF)))
                    Fail($"Code ended outside the valid memory range at {value} coming from {PC}...");
                else
                    WriteRegister(CpuRegister.P, value);
            }
        }

        private Dictionary<int, MemoryBreakpointDefinition> _MemoryBreakpoints = new Dictionary<int, MemoryBreakpointDefinition>();

        private MemoryBreakpointDefinition? _memBreakHit = null;

        public void ClearMemoryBreakpoint(int address)
        {
            _MemoryBreakpoints.Remove(address);
        }

        public MemoryBreakpointDefinition SetMemoryBreakpoint(int address, bool read = true, bool write = true)
        {
            // TODO validate address..
            if (_MemoryBreakpoints.TryGetValue(address, out var bp))
            {
                bp.OnRead = read;
                bp.OnWrite = write;
                return bp;
            }
            bp = new MemoryBreakpointDefinition();
            bp.OnRead = read;
            bp.OnWrite = write;
            bp.IsEnabled = true;
            _MemoryBreakpoints.Add(address, bp);
            return bp;
        }

        public int ReturnAddress(int nStackElement = 0)
        {
            int r = ReadRegister(CpuRegister.R);
            r-=nStackElement;
            if (!ReadFromAbsoluteAddress(r, out var value))
                return -1;
            return value;
        }

        /// <summary>
        /// Continues to handle the next instruction.
        /// </summary>
        public void Tick()
        {
            switch(State)
            {
                case SimulatorState.Created:
                    throw new InvalidOperationException("CPU has not yet been reset!");
                case SimulatorState.FailedState:
                    throw new InvalidOperationException("CPU is in failed state!");
            }
            _memBreakHit = null;
            ChangeState(SimulatorState.Running); // set here, so any subsequent code can update to other states.
            HandleDeviceTick();
            if(State == SimulatorState.FailedState)
                return;

            InstructionResult result;

            if(CallInterruptServiceHandler.HasValue)
            {
                ResetTiming();
                result = HandleInterruptCall(CallInterruptServiceHandler.Value);
                CallInterruptServiceHandler = null;
            }
            else
            {
                var opCode = ExeOpcode.GetValueOrDefault(Memory[PC]);   // "EXE" result first... if again, we'll loop...
                ExeOpcode = null;
                var bp = Disassembler.BasePattern(opCode);
                if (!Handlers.TryGetValue(bp, out var thisHandler))
                {
                    Fail($"OpCode {opCode} from {PC} is not recognized!");
                    result = new InstructionResult();
                }
                else
                {
                    ResetTiming();
                    result = thisHandler(opCode);
                }
            }
            if (State != SimulatorState.FailedState)    // only tick over if we are still in a valid state...
            {
                var old = Ticks;
                Ticks += result.TickCounter;
                if (result.MoveProgramCounerAbsolute.HasValue)
                    PC = result.MoveProgramCounerAbsolute.Value;
                else
                    PC+= result.MoveProgramCounterDelta;
                OnTicked(old);
                // check for breakepoint hit!
                if (_Breakpoints.TryGetValue(PC, out var bpDef))
                {
                    if (bpDef.IsEnabled)    // todo: conditional? counter? 
                    {
                        ChangeState(SimulatorState.BreakPointHit);
                    }
                }
                if(_memBreakHit != null)
                {
                    ChangeState(SimulatorState.BreakPointHit);
                }
            }
        }

        // two levels...
        private (int SavedPA, InterruptLevel InterruptedFrom)[] IODeviceInterruptStack = new (int SavedPA, InterruptLevel InterruptedFrom)[2];

        private int? CallInterruptServiceHandler = null;

        private void HandleDeviceTick()
        {
            // first... did the last tick yield an IRQ?
            var pil = Devices.PendingInterruptLevel;
            if (pil != InterruptLevel.None && InterruptsEnabled && !CallInterruptServiceHandler.HasValue) // Avoid race condition during JSM IV operation.
            {
                // could be...
                if(IODeviceInterruptStack[0].InterruptedFrom == InterruptLevel.None || pil == InterruptLevel.High && IODeviceInterruptStack[0].InterruptedFrom == InterruptLevel.Low)
                {
                    int? deviceId = Devices.GetSelectCodeForInterruptAndConfirm(pil);
                    if(deviceId.HasValue)
                    {
                        // we only accept the interrupt if we are "first" or "higher priority";     this.State = SimulatorState.BreakPointHit
                        IODeviceInterruptStack[1] = IODeviceInterruptStack[0];
                        IODeviceInterruptStack[0].InterruptedFrom = pil;
                        IODeviceInterruptStack[0].SavedPA = ReadRegister(CpuRegister.PA);
                        WriteRegister(CpuRegister.PA, deviceId.Value);
                        
                        CallInterruptServiceHandler = (ReadRegister(CpuRegister.IV) & 0xFFF0) | deviceId.Value;
                    }
                }
            }
            Devices.Tick();
        }

        public void ClearBreakPoint(int address)
        {
            _Breakpoints.Remove(address);
        }

        public BreakpointDefinition SetBreakPoint(int address)
        {
            if (_Breakpoints.TryGetValue(address, out var bp))
            {
                return bp;
            }
            bp = new BreakpointDefinition() { IsEnabled = true };
            _Breakpoints.Add(address, bp);
            return bp;
        }

        private Dictionary<int, BreakpointDefinition> _Breakpoints = new Dictionary<int, BreakpointDefinition>();

        private void ChangeState(SimulatorState newState)
        {
            if(State != newState)
            {
                State = newState;
                OnStateChanged();
            }
        }

        private void Fail(string message)
        {
            StateMessage = message;
            ChangeState(SimulatorState.FailedState);
        }

#region Events
        /// <summary>
        /// Triggered on every CPU "tick".
        /// </summary>
        public event EventHandler<TickedEventArgs> Ticked;
        /// <summary>
        /// Triggered whenever the CPU is reset.
        /// </summary>
        public event EventHandler Resetted;
        /// <summary>
        /// Triggered whenever the CPU run state changes.
        /// </summary>
        public event EventHandler StateChanged;

        protected virtual void OnStateChanged()
        {
            if (StateChanged != null)
                StateChanged.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnResetted()
        {
            if (Resetted != null)
                Resetted.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnTicked(long oldTick)
        {
            if (Ticked != null)
                Ticked.Invoke(this, new TickedEventArgs(oldTick, Ticks));
        }

        public bool IsFreeRunning { get; private set; }

        public async Task Run(bool realTime = false, long? tickLimit = null)
        {
            if (IsFreeRunning)
                throw new InvalidOperationException("Cannot run while already running!");
            if(State == SimulatorState.FailedState || State ==  SimulatorState.Created)
                throw new InvalidOperationException("Simulator state is invalid for free running mode. Needs to be properly reset first!");
            IsFreeRunning = true;
            DateTime startedRunning = DateTime.UtcNow;
            TimeSpan startedVirtual = UpTime.GetValueOrDefault();
            ChangeState(SimulatorState.Running);
            var startedAt = Ticks;
            if (tickLimit.HasValue)
                tickLimit = startedAt + tickLimit.Value;
            while (State == SimulatorState.Running)
            {
                Tick();
                // check if "real time" is more than a ms behind simulated time, delay if so...
                if (realTime)
                {
                    DateTime now = DateTime.UtcNow;
                    // TODO: wait if we run fast... tell the outside if we run slow...
                }
                if(tickLimit.HasValue && tickLimit.Value < Ticks)
                    break;
            }
            IsFreeRunning = false;
            OnStateChanged();   // report new state changed
        }
        #endregion
    }
}