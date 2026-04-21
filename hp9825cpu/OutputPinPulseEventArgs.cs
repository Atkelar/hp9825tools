using System;

namespace HP9825CPU
{
    public class OutputPinPulseEventArgs
        : EventArgs
    {
        public OutputPinPulseEventArgs(PinNumber which)
        {
            PulseOnPins = which;
        }

        public PinNumber PulseOnPins { get; private set; }
        
        public bool Any => PulseOnPins != PinNumber.None;
        public bool Pin1 => (PulseOnPins & PinNumber.Pin1) != 0;
        public bool Pin2 => (PulseOnPins & PinNumber.Pin2) != 0;
        public bool Pin3 => (PulseOnPins & PinNumber.Pin3) != 0;
        public bool Pin4 => (PulseOnPins & PinNumber.Pin4) != 0;
    }
}