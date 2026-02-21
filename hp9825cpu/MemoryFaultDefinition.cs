using System;
using System.Reflection;

namespace HP9825CPU
{
    internal class MemoryFaultDefinition
    {
        private int _BitMask;
        private MemoryFaultMode _Mode;


        public MemoryFaultDefinition(int startAddess, int endAddress, int bitMask, MemoryFaultMode mode)
        {
            this.FirstAddress = startAddess;
            this.LastAddress = endAddress;
            this._BitMask = bitMask;
            this._Mode = mode;
        }

        public int FirstAddress { get; internal set; }
        public int LastAddress { get; internal set; }

        private bool _Toggle;

        internal int ApplyToValue(int value)
        {
            switch (_Mode)
            {
                case MemoryFaultMode.StuckOn:
                    return value | _BitMask;
                case MemoryFaultMode.StuckOff:
                    return value & ~_BitMask;
                case MemoryFaultMode.Invert:
                    return value ^ _BitMask;
                case MemoryFaultMode.Random:
                    return (value & ~_BitMask) | (Random.Shared.Next() & _BitMask);
                case MemoryFaultMode.Toggle:
                    if(_Toggle = !_Toggle)
                        goto case MemoryFaultMode.StuckOn;
                    goto case MemoryFaultMode.StuckOff;
            }
            throw new NotImplementedException();
        }
    }
}