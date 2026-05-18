using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using System.Xml;

namespace HP9825CPU
{

    /// <summary>
    /// Simulates an HP RTC plug in - HP 98035
    /// </summary>
    public class RTCDevice
        : DeviceBase
    {
        private readonly bool _EuropeanDateFormat;
        private readonly bool _ExtendedFormat;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceName">The name of this device for the simulator output...</param>
        /// <param name="enableExtension">True to enable the "extended" (rebuilt) version. Supports years and checks/validates leap years.</param>
        /// <param name="configureEuropeanMode">True to use the "european" date format. Usually set with a solder jumper inside the device.</param>
        /// <param name="hasCable">True to indicate "option 100" (i.e. the cable) - if true, initializes the input pins to false, waiting for external stimulus using <see cref="SetInputPin(PinNumber, bool)"/>. False will initialize them to "true" and block calls to the set method.</param>
        public RTCDevice(bool configureEuropeanMode = false, bool enableExtension = false, bool hasCable = false, string? deviceName = null)
            : base(9, "HP98035A", deviceName)   // service manuel page 12
        {
            _EuropeanDateFormat = configureEuropeanMode;
            _ExtendedFormat = enableExtension;
            Status = true;  // always set (decoding logic is done by the device manager for convenience.)
            for(int i=0;i<4;i++)
            {
                _Units[i]._Parent = this;
                if (!hasCable)
                {
                    _Inputs[i] = true;
                }
            }
            _HasCable = hasCable;
        }

        /// <summary>
        /// If set, assumes *this* date/time every time the unit is reset, and uses "simulation relative" timing.
        /// </summary>
        public DateTime? ForcedTimeAtReset { get; set; }

        /// <summary>
        /// True to indicate that the reported time should be relative to the simulation time (based on reset time).
        /// </summary>
        public bool SimulationRelativeTiming { get; set; }

        private bool _RunRelative;

        private DateTime _ResetTime;

        /// <summary>
        /// Resets the RTC module.
        /// </summary>
        protected internal override void Reset()
        {
            if (System == null)
                throw new InvalidOperationException();
            base.Reset();
            _InterruptFlag = false;
            _ErrorCode = 0;  // we start up clean... but we run through the self test code!
            // s_ScheduledHardwareErorr; 
            if (ForcedTimeAtReset.HasValue)
            {
                _RunRelative = true;
                _ResetTime = ForcedTimeAtReset.Value.ToUniversalTime();
            }
            else
            {
                if (SimulationRelativeTiming)
                {
                    _ResetTime = DateTime.UtcNow;
                    _RunRelative = true;
                }
                else
                    _RunRelative = false;
            }
            _StartupAt = System.RunTime;
            SetDefaults();
        }

        protected override void SaveCurrentState(XmlElement target)
        {
            target.SetAttribute("lastTick", _LastTimingTick);
            if (_CommandInputBuffer.Length>0)
                target.SetAttribute("cmdBuf", _CommandInputBuffer.ToString());
            target.SetAttribute("baseTicks", _BaseTicksForEvents);
            target.SetAttribute("errorCode", _ErrorCode);
            target.SetAttribute("euroDate", _EuropeanDateFormat);
            target.SetAttribute("extended", _ExtendedFormat);
            target.SetAttribute("hasCable", _HasCable);
            target.SetAttribute("hasInput", _HasInputData);
            target.SetAttribute("hasIO", _HasIOPending);
            for(int i=0;i<_Inputs.Length;i++)
                target.SetAttribute("in" + i.ToString(), _Inputs[i]);
            target.SetAttribute("irqFlag", _InterruptFlag);
            target.SetAttribute("irqState", _InterruptStatus);
            target.SetAttribute("latchIn", _LatchInput);
            target.SetAttribute("latchOut", _LatchOutput);
            target.SetAttribute("nextCommand", _NextCommand);
            target.SetAttribute("offsetRT", _OffsetToRealTime);
            target.SetAttribute("outIndex", _OutIndex);
            if (_OutputBuffer != null)
            {
                target.SetAttribute("outBufferLen", _OutputBuffer.Length);
                target.SetAttribute("outBuffer", Convert.ToBase64String(_OutputBuffer));
            }
            target.SetAttribute("outLength", _OutputLength);
            target.SetAttribute("resetTime", _ResetTime);
            target.SetAttribute("runRelative", _RunRelative);
            target.SetAttribute("hwError", _ScheduledHardwareErorr);
            target.SetAttribute("startTime", _StartupAt);
            for(int i=0;i<4;i++)
            {
                var eUnit = target.OwnerDocument.CreateElement("unit", CpuSimulator.StateSaveNamespace);
                target.AppendChild(eUnit);
                eUnit.SetAttribute("idx", i);
                eUnit.SetAttribute("port", _Units[i].ExternalPort);
                eUnit.SetAttribute("delay", _Units[i].Delay );
                eUnit.SetAttribute("period", _Units[i].Period );
                eUnit.SetAttribute("running", _Units[i].Running );
                eUnit.SetAttribute("value", _Units[i].Value );
                eUnit.SetAttribute("mDay", _Units[i].MatchDay);
                eUnit.SetAttribute("mHour", _Units[i].MatchHour );
                eUnit.SetAttribute("mMin", _Units[i].MatchMinute );
                eUnit.SetAttribute("mSec", _Units[i].MatchSecond );
            }
            target.SetAttribute("simulatedReset", this.ForcedTimeAtReset);
            target.SetAttribute("waitingLineMask", this.WaitingForLineMask);
            target.SetAttribute("unservicedIrqs", this.UnservicedInterrupts);
            target.SetAttribute("triggerCode", this.TriggerCode);
            target.SetAttribute("testPointOn", this.TestPointEnabled);
            target.SetAttribute("simulationRelative", this.SimulationRelativeTiming);
            target.SetAttribute("simulateRTCError", this.RTCError);
            // TODO: validate!
        }

        /// <summary>
        /// Gets the "simulated time" based on settings. 
        /// </summary>
        private TimeSpan RelativeTime
        {
            get
            {
                if (_RunRelative && System != null)
                {
                    return System.RunTime.Subtract(_StartupAt);
                }
                else
                {
                    return DateTime.UtcNow.Subtract(_ResetTime);
                }
            }
        }

        long _BaseTicksForEvents = 0;

        private void SetDefaults()
        {
            _InterruptFlag = false;
            _InterruptStatus = false;
            for(int i =0;i<4;i++)
            {
                _Units[i].Reset();
            }
            _Units[0].Mode = TiminigUnitMode.Output;
            _Units[0].ExternalPort = 0;
            _Units[1].Mode = TiminigUnitMode.Input;
            _Units[1].ExternalPort = 1;
            _BaseTicksForEvents = RelativeTime.Ticks;
            _LastTimingTick = 0;
            OutputLineMask = 0;
            WaitingForLineMask = false;
        }

        public override bool Flag { get => !_HasIOPending; protected set {} }

        // TODO: provide properties for simulation of "absolute real time" or "reset to current simulation time" only.

        private const string DateFormatUS = "MM:dd:HH:mm:ss";
        private const string DateFormatEuro = "dd:MM:HH:mm:ss";
        private const string DateFormatUSExtended = "YYYY:MM:dd:HH:mm:ss";
        private const string DateFormatEuroExtended = "YYYY:dd:MM:HH:mm:ss";

        private bool _HasError => _ErrorCode != 0;

        /// <summary>
        /// when an interrupt has been missed. This could happen when two or more tim-
        /// ing/ counting units request interrupts at intervals to narrow to be serviced by the
        /// computer. This would also happen if a second interrupt occurred and the interface
        /// was not re-enabled for interrupts after the first occurrence. To determine which
        /// interrupting unit was missed, see "Request Unserviced Interrupt" .
        /// </summary>
        private void SetErrorMissedInterrupt()
        {
            _ErrorCode |= 1;
        }


        /// <summary>
        /// when the instruction sent to the clock is inconsistent with the current assignment of
        /// units and ports. Some examples are: sending an interrupt instruction to a unit which
        /// is assigned to an input port, attempting to assign two units to the same port, or
        /// requesting the value of an unassigned unit.
        /// </summary>

        private void SetErrorPortAssignment()
        {
            _ErrorCode |= 4;
        }

        /// <summary>
        /// when attempting to execute a Set Real Time instruction when there is unit defined
        /// to an output port in the active state. This is also caused by trying to change the
        /// match, delay, or periodic specifications on an active unit, or by trying to reassign an
        /// active unit.
        /// </summary>
        private void SetErrorPortActive()
        {
            _ErrorCode |= 8;
        }

        /// <summary>
        /// when incorrect instructions or out-of-range data are sent to the Real Time Clock. It
        /// is set by incorrect usage of instructions, or when entered data (real time, match
        /// time, ... ) is invalid.
        /// </summary>
        private void SetErrorBadInstruction()
        {
            _ErrorCode |= 2;
        }

        /// <summary>
        /// Simulates the requested hardware faults with the next reset.
        /// </summary>
        /// <param name="lostTime">True if the "time has been lost" - i.e. the clock time is invalid. Battery drained or last write back operation interrupted.</param>
        /// <param name="hardwareDefect">True to simulate a detected hardware problem with the RTC chip.</param>
        /// <param name="faultyBits03">True to simulate a RAM error in bits 0-3.</param>
        /// <param name="faultyBits47">True to simulate a RAM error in bits 4-7.</param>
        public void SimulateHardwareFault(bool lostTime = true, bool hardwareDefect = false, bool faultyBits03 = false, bool faultyBits47 = false)
        {
            _ScheduledHardwareErorr = 0;
            _ScheduledHardwareErorr |= lostTime ? 0x10 : 0;
            _ScheduledHardwareErorr |= hardwareDefect ? 0x20 : 0;
            _ScheduledHardwareErorr |= faultyBits03 ? 0x40 : 0;
            _ScheduledHardwareErorr |= faultyBits47 ? 0x80 : 0;
        }

        private int _ScheduledHardwareErorr = 0;

        protected internal override int ReadIORegister(int regIndex)
        {
            int result = 0;
            // status register: R5 (1)
            //  read: bit 0 = error bit.

            Debug.WriteLine("RTC READ {0}", regIndex);

            switch(regIndex)
            {
                case 1:
                    result |= _HasError ? 0x1 : 0;
                    result |= _InterruptFlag  ? 0x2: 0;
                    // TODO: interrupt bit?
                    result |= 0x20; // fixed bit for interface ID...
                    // DMA and invert flags always zero.
                    result |= _InterruptStatus ? 0x80 : 0;
                    break;
                case 0:
                    result = _LatchOutput;
                    break;
            }

            Debug.WriteLine("RTC READ {0} -> {1:x2} ({1})", regIndex, result);
            return result;
        }

        private StringBuilder _CommandInputBuffer = new StringBuilder();

        protected internal override void Tick()
        {
            if(_NextCommand != null)
            {
                HandleCommand(_NextCommand);
                _NextCommand = null;
            }
            bool commandDone = false;
            if (_HasIOPending)
            {
                if (_HasInputData)
                {
                    if (WaitingForLineMask)
                    {
                        // whatever bits now, it's the mask...
                        OutputLineMask = _LatchInput & 0xF;
                        WaitingForLineMask = false;
                    }
                    else
                    {
                        if (_LatchInput == 10 || _LatchInput == 47)
                            commandDone = true;
                        else
                        {
                            char cInput = (char)_LatchInput;
                            if ((cInput >= '0' && cInput <='9') || (cInput >= 'A' && cInput <= 'Z') || cInput == '=')   // ignore any non-relevand characters, as per definition.
                                _CommandInputBuffer.Append(cInput);
                        }
                    }
                    _HasIOPending = false;
                    _HasInputData = false;
                }
                else
                {
                    if(_OutputBuffer == null)   
                        _LatchOutput = 10;
                    else
                    {
                        if (_OutIndex < _OutputBuffer.Length && _OutIndex < _OutputLength)
                        {
                            _LatchOutput = ((int)_OutputBuffer[_OutIndex]) & 0xFF;
                            _OutIndex++;
                        }
                        else
                        {
                            _LatchOutput = 10;
                        }
                    }
                    _HasIOPending = false;
                }
            }
            if (commandDone)
            {
                _NextCommand = _CommandInputBuffer.ToString();
                _CommandInputBuffer.Clear();
            }

            var delta = (this.RelativeTime.Ticks - this._BaseTicksForEvents) / TicksPerMilliseconds;
            if (delta > _LastTimingTick)
            {
                int triggerWord = 0;
                for(int i =0;i<4;i++)
                {
                    if  (_Units[i].Tick(delta))
                    {
                        triggerWord |= 1 << _Units[i].ExternalPort;
                    }
                }
                _LastTimingTick = delta;
                if (_HasCable)  // cable "pulses" the sync line every millisecond...
                    OnOutputPulse((PinNumber)triggerWord);
                if (triggerWord!=0)
                {
                    // TODO: request interrupt if set!
                    Debug.WriteLine("Got a trigger at {1}ms for {0}...", (PinNumber)triggerWord, _LastTimingTick);
                }
            }
        }

        /// <summary>
        /// Uses the set-error-flag methods to indicate errors...
        /// </summary>
        /// <param name="command">The command to handle...</param>
        private void HandleCommand(string command)
        {
            if (command.Length==0)
                return;    // empty commands are ignored...
            if (command.Length == 1)
            {
                switch(command[0])
                {
                    case 'R':
                        HandleRequestTime();
                        break;
                    case 'B':
                        FastPOST();
                        break;
                    case 'N':
                        FetchInputsAndPulseOutputs();
                        break;
                    case 'L':
                        WaitingForLineMask = true;
                        break;
                    case 'Q':
                        TestPointEnabled = true;
                        break;
                    case 'X':
                        TestPointEnabled = false;
                        break;
                    case 'E':
                        SetOutputBuffer(_ErrorCode & 0xF);
                        _ErrorCode = _ErrorCode & 0xF0;   // clear out software errors after read...
                        break;
                    case 'T':
                        SetOutputBuffer(TriggerCode & 0xF);
                        TriggerCode = 0;
                        break;
                    case 'W':
                        SetOutputBuffer(this.UnservicedInterrupts);
                        break;
                    case 'A':
                        HandleHaltAll();
                        break;
                    case 'F':
                        HandleActivateAll();
                        break;
                    default:
                        SetErrorBadInstruction();
                        break;
                }
                return;
            }

            switch(command[0])
            {
                case 'S':
                    HandleSetTime(command.Substring(1));
                    break;
                case 'U':
                    HandleUnitCommand(command.Substring(1));
                    break;
                default:
                    SetErrorBadInstruction();
                    break;
            }
        }

        private int UnservicedInterrupts = 0;

        /// <summary>
        /// "B" command handler... TODO: find out more details! It's a bit woo-woo in the docs.
        /// </summary>
        private void FastPOST()
        {
            this.SetDefaults();
        }

        private void FetchInputsAndPulseOutputs()
        {
            // pulse any manually triggered outputs...
            int input = 0;
            for (int i=0;i<4;i++)
            {
                if (_Inputs[i])
                    input |= 1 << i;
            }
            OnOutputPulse((PinNumber)OutputLineMask);
            SetOutputBuffer(input);
        }

        private void HandleActivateAll()
        {
            for(int i=0;i<4;i++)
            {
                if (_Units[i].Mode != TiminigUnitMode.Unassigned)
                    ActivateUnit(i);
            }
        }

        private void ActivateUnit(int index)
        {
            // TODO: set init params...
            _Units[index].Start();
        }

        private void HandleHaltAll()
        {
            for(int i=0;i<4;i++)
            {
                if (_Units[i].Mode != TiminigUnitMode.Unassigned)
                    _Units[i].Stop();
            }
            _InterruptFlag = false;
        }

        private enum TiminigUnitMode
        {
            Unassigned = 0,
            Input = 1,
            Output = 2
        }

        private static long TicksPerMilliseconds = TimeSpan.FromMilliseconds(1).Ticks;

        private struct TimingUnit
        {
            public TimingUnit()
            {
                
            }
            public RTCDevice _Parent;
            public TiminigUnitMode Mode;
            public bool Running {get; private set;}
            public int ExternalPort = -1;

            private long _TicksStarted = 0;

            public void Start()
            {
                if (Mode == TiminigUnitMode.Unassigned)
                {
                    _Parent.SetErrorPortAssignment();
                    return;
                }
                ResetCount();
                if (Running)
                {
                    return;
                }
                if (Mode == TiminigUnitMode.Input)
                {
                    if (_Parent.SimulationRelativeTiming)
                    {
                        _TicksStarted = _Parent.System?.RunTime.Ticks ?? 0;
                    }
                    else
                    {
                        _TicksStarted = DateTime.UtcNow.Ticks;
                    }
                }
                Running = true;
            }

            public void Stop()
            {
                if (Mode == TiminigUnitMode.Unassigned)
                {
                    _Parent.SetErrorPortAssignment();
                    return;
                }
                if (!Running)
                    return;
                if (Mode == TiminigUnitMode.Input)
                {
                    if (_Parent.SimulationRelativeTiming)
                    {
                        _CumulatedRunningTicks += ((_Parent.System?.RunTime.Ticks) ?? 0) - _TicksStarted;
                    }
                    else
                    {
                        _CumulatedRunningTicks += DateTime.UtcNow.Ticks - _TicksStarted;
                    }
                }
                _TicksStarted = 0;
                Running = false;
            }

            private long _CumulatedRunningTicks = 0;

            public void ResetCount()
            {
                if (Mode == TiminigUnitMode.Input && Running)
                {
                    if (_Parent.SimulationRelativeTiming)
                    {
                        _TicksStarted = _Parent.System?.RunTime.Ticks ?? 0;
                    }
                    else
                    {
                        _TicksStarted = DateTime.UtcNow.Ticks;
                    }
                }
                else
                    _TicksStarted = 0;
                _CumulatedRunningTicks = 0;
            }

            public long Value { get; internal set; }
            public int? MatchSecond { get; internal set; }
            public int? MatchHour { get; internal set; }
            public int? MatchMinute { get; internal set; }
            public int? MatchDay { get; internal set; }
            public long Delay { get; internal set; }
            public long Period { get; internal set; }


            internal long GetTimerValue()
            {
                if (Mode == TiminigUnitMode.Input)
                {
                    if (Running)
                    {
                        if (_Parent.SimulationRelativeTiming)
                        {
                            return (_CumulatedRunningTicks + (((_Parent.System?.RunTime.Ticks) ?? 0) - _TicksStarted)) / TicksPerMilliseconds;
                        }
                        else
                        {
                            return (_CumulatedRunningTicks + (DateTime.UtcNow.Ticks - _TicksStarted)) / TicksPerMilliseconds;
                        }
                    }
                    return this._CumulatedRunningTicks / TicksPerMilliseconds;
                }
                return 0;
            }

            internal void Reset()
            {
                this.Mode = TiminigUnitMode.Unassigned;
                this.ExternalPort = -1;
                this._CumulatedRunningTicks = 0;
                this._TicksStarted = 0;
                this.Running = false;
                this.Value = 0;
                this.MatchDay=null;
                this.MatchHour=null;
                this.MatchSecond=null;
                this.MatchMinute=null;
                this.Delay = 0;
                this.Period = 0;
                this._WasMatch = false;
                this._MatchTriggered = false;
                this._NextPeriod = null;
                this._DelayExpires = null;
            }

            private bool HasMatchSet => MatchSecond.HasValue;
            private bool _WasMatch = false;
            private bool _MatchTriggered = false;
            private long? _DelayExpires = null;
            private long? _NextPeriod = null;

            internal bool Tick(long currentTick)
            {
                bool needsPulse = false;
                if (Running)
                {
                    // we got a tick!
                    // TODO: handle input/output accordingly...
                    switch (Mode)
                    {
                        case TiminigUnitMode.Input:
                        // count if input pin is "true"...
                            if (_Parent._Inputs[this.ExternalPort])
                                Value++;
                            break;
                        case TiminigUnitMode.Output:
                            // three stacked conditions: match, delay, pulse...
                            if (HasMatchSet)    // match is cyclic, i.e. needs to reset once it no longer matches...
                            {
                                if (!_MatchTriggered)   // still waiting...
                                {
                                    if (IsMatch(_Parent.GetSimulatedNow()))
                                    {
                                        if(!_WasMatch)
                                        {
                                            _MatchTriggered = true;
                                            _WasMatch = true;
                                            _DelayExpires = currentTick + Delay;
                                        }
                                        // else ignore...
                                    }
                                    else
                                    {
                                        _WasMatch = false;
                                    }
                                }
                            }
                            else
                            {
                                _MatchTriggered = true;  // no match requested, always triggered.
                                _DelayExpires = currentTick + Delay;
                            }
                            // if we have a delay, and the 
                            if(_DelayExpires.HasValue && _DelayExpires <= currentTick && (!_NextPeriod.HasValue || _NextPeriod.Value <= currentTick))
                            {
                                // delay is done (0 or greater...)
                                needsPulse = true;
                                if (Period > 0)
                                {
                                    _NextPeriod = (_NextPeriod ?? currentTick) + Period;
                                }
                                else
                                {
                                    _DelayExpires = null;   // one off... get ready for reapeat...
                                    _MatchTriggered = false;
                                }
                            }
                            break;
                    }
                    // publish interrupt request to parent if so... this is done indirectly to support "multiple same time interrupts" as per spec.
                }
                return needsPulse;
            }

            private bool IsMatch(DateTime dateTime)
            {
                return (!MatchSecond.HasValue || dateTime.Second == MatchSecond.Value)
                    && (!MatchMinute.HasValue || dateTime.Minute == MatchMinute.Value)
                    && (!MatchHour.HasValue || dateTime.Hour == MatchHour.Value)
                    && (!MatchDay.HasValue || dateTime.Day == MatchDay.Value);
            }
        }

        private bool[] _Inputs = new bool[4];

        public void SetInputPin(PinNumber which, bool on)
        {
            if(!_HasCable)
                throw new InvalidOperationException("The device isn't equipped with a cable!");

            if ((which & PinNumber.Pin1)!=0)
                _Inputs[0] = on;
            if ((which & PinNumber.Pin2)!=0)
                _Inputs[1] = on;
            if ((which & PinNumber.Pin3)!=0)
                _Inputs[2] = on;
            if ((which & PinNumber.Pin4)!=0)
                _Inputs[3] = on;
        }

        public event EventHandler<OutputPinPulseEventArgs> OutputPulse;

        protected void OnOutputPulse(PinNumber which)
        {
            if (_HasCable && OutputPulse != null)
            {
                OutputPulse(this, new OutputPinPulseEventArgs(which));
            }
        }

        private TimingUnit[] _Units = new TimingUnit[4];

        private static int? UnitIndexFromString(string value, int index = 0)
        {
            switch(value[index])
            {
                case '1':
                case '2':
                case '3':
                case '4':
                    return ((int)value[index]) - (int)'1';
                default:
                    return null;
            }
        }

        private void HandleUnitCommand(string command)
        {
            if (command.Length==0)
            {
                SetErrorBadInstruction();
                return;
            }
            var unitIndex = UnitIndexFromString(command);
            if(!unitIndex.HasValue)
            {
                SetErrorBadInstruction();
                return;
            }

            command = SkipNumbers(command);

            if (command.Length==0)
            {
                SetErrorBadInstruction();
                return;
            }
            
            switch (command[0])
            {
                case '=':
                    HandleUnitAssignment(unitIndex.Value, command.Substring(1));
                    break;
                case 'H':
                    HandleHaltUnit(unitIndex.Value);
                    break;
                case 'G':
                    HandleActivateUnit(unitIndex.Value);
                    break;
                case 'C':
                    HandleClearCounterInUnit(unitIndex.Value);
                    break;
                case 'V':
                    HandleRequestCounterInUnit(unitIndex.Value);
                    break;
                case 'M':
                    HandleUnitMatchCommand(unitIndex.Value, command.Substring(1));
                    break;
                case 'D':
                    HandleDelayCommand(unitIndex.Value, command.Substring(1));
                    break;
                case 'P':
                    HandlePeriodCommand(unitIndex.Value, command.Substring(1));
                    break;
                default:
                    SetErrorBadInstruction();
                    break;
            }
        }

        private void HandlePeriodCommand(int unitIndex, string command)
        {
            // period has two versions:
            // P alone: clear delay.
            // P### : n ms period - n = 0 (off) to 99999999 ms.
            if (_Units[unitIndex].Running)
            {
                SetErrorPortActive();
                return;
            }
            if (_Units[unitIndex].Mode != TiminigUnitMode.Output)
            {
                SetErrorPortAssignment();
                return;
            }
            long period = 0;
            if (command.Length>0)
            {
                if (!long.TryParse(command, out period))
                {
                    SetErrorBadInstruction();
                    return;
                }
            }
            if (period < 0 || period > 99999999)
            {
                SetErrorBadInstruction();
                return;
            }
            _Units[unitIndex].Period = period;
        }

        private void HandleDelayCommand(int unitIndex, string command)
        {
            // delay has two versions:
            // D alone: clear delay.
            // D### : n ms delay - n = 0 (off) to 99999999 ms.
            if (_Units[unitIndex].Running)
            {
                SetErrorPortActive();
                return;
            }
            if (_Units[unitIndex].Mode != TiminigUnitMode.Output)
            {
                SetErrorPortAssignment();
                return;
            }
            long delay = 0;
            if (command.Length>0)
            {
                if (!long.TryParse(command, out delay))
                {
                    SetErrorBadInstruction();
                    return;
                }
            }
            if (delay < 0 || delay > 99999999)
            {
                SetErrorBadInstruction();
                return;
            }
            _Units[unitIndex].Delay = delay;
        }

        private void HandleUnitMatchCommand(int unitIndex, string command)
        {
            // match command can have two versions:
            // M alone: cancel match pattern.
            // M ## : set match to specific "pattern".
            if (_Units[unitIndex].Running)
            {
                SetErrorPortActive();
                return;
            }
            if (_Units[unitIndex].Mode != TiminigUnitMode.Output)
            {
                SetErrorPortAssignment();
                return;
            }
            if (command.Length==0)
            {
                _Units[unitIndex].MatchSecond = null;
                _Units[unitIndex].MatchHour = null;
                _Units[unitIndex].MatchMinute = null;
                _Units[unitIndex].MatchDay = null;
            }
            else
            {
                // assume digits for day, hour, 
                if (!TryParseTimeByFormat(command, out var year, out var month, out var day, out var hour, out var minute, out var second) || !second.HasValue)
                {
                    SetErrorBadInstruction();
                    return;
                }
                // we only want seconds upwards..
                _Units[unitIndex].MatchSecond = second;
                _Units[unitIndex].MatchMinute = minute;
                _Units[unitIndex].MatchHour = hour;
                _Units[unitIndex].MatchDay = day;
                // ignoring month and year...
            }
        }

        private void HandleRequestCounterInUnit(int unitIndex)
        {
            if (_Units[unitIndex].Mode != TiminigUnitMode.Input)
                SetErrorPortAssignment();
            else
                SetOutputBuffer(_Units[unitIndex].GetTimerValue().ToString());
        }

        private void HandleClearCounterInUnit(int unitIndex)
        {
            if (_Units[unitIndex].Mode != TiminigUnitMode.Input)
                SetErrorPortAssignment();
            else
                _Units[unitIndex].Value = 0;
        }

        private void HandleActivateUnit(int unitIndex)
        {
            if(_Units[unitIndex].Mode == TiminigUnitMode.Unassigned)
                SetErrorPortAssignment();
            else
                _Units[unitIndex].Start();
        }

        private void HandleHaltUnit(int unitIndex)
        {
            if(_Units[unitIndex].Mode == TiminigUnitMode.Unassigned)
                SetErrorPortAssignment();
            else
                _Units[unitIndex].Stop();
        }

        private void HandleUnitAssignment(int unitIndex, string command)
        {
            if (_Units[unitIndex].Running)   // not allowed.
            {
                SetErrorPortActive();
                return;
            }
            if (command.Length==0)  // got a "U#=" command...
            {
                _Units[unitIndex].Mode = TiminigUnitMode.Unassigned;
                _Units[unitIndex].ExternalPort = -1;
                return;
            }
            if(command.Length<2)
            {
                SetErrorBadInstruction();
                return;
            }
            TiminigUnitMode newMode;
            switch(command[0])
            {
                case 'O':
                    newMode = TiminigUnitMode.Output;
                    break;

                case 'I':
                    newMode = TiminigUnitMode.Input;
                    break;

                default:
                    SetErrorBadInstruction();
                    return;
            }

            var portNumber = UnitIndexFromString(command, 1);
            if (!portNumber.HasValue)
            {
                SetErrorBadInstruction();
                return;
            }

            _Units[unitIndex].Mode = newMode;
            _Units[unitIndex].ExternalPort = portNumber.Value;
        }

        private string SkipNumbers(string command)
        {
            int i = 0;
            while (i < command.Length && char.IsAsciiDigit(command[i]))
                i++;
            if (i >= command.Length)
                return string.Empty;
            return command.Substring(i);
        }

        private void HandleRequestTime()
        {
            if(RTCError)
                SetOutputBuffer(ErrorTime);
            else
            {
                if(_ExtendedFormat)
                {
                    SetOutputBuffer(GetSimulatedNow().ToString(_EuropeanDateFormat ? DateFormatEuroExtended : DateFormatUSExtended));
                }
                else
                {
                    SetOutputBuffer(GetSimulatedNow().ToString(_EuropeanDateFormat ? DateFormatEuro : DateFormatUS));
                }
            }
        }

        private void SetOutputBuffer(int value)
        {
            _OutIndex = 0;
            if(_OutputBuffer == null)
                _OutputBuffer = new byte[32];
            _OutputBuffer[0] = (byte)(value & 0xFF);
            _OutputLength = 1;
        }
        private void SetOutputBuffer(string value)
        {
            _OutIndex = 0;
            if (_OutputBuffer == null)
                _OutputBuffer = new byte[Math.Max(value.Length * 2, 32)];
            else
            {
                if (_OutputBuffer.Length < value.Length)
                    _OutputBuffer = new byte[Math.Max(value.Length * 2, 32)];
            }
            _OutputLength = Encoding.ASCII.GetBytes(value, 0, value.Length, _OutputBuffer, 0);
        }

        private bool TryParseTimeByFormat(string timeString, out int? year, out int? month, out int? day, out int? hour, out int? minute, out int? second)
        {
            year = month=day=hour=minute=second= null;
            // from right to left... update:
            // seconds, minutes, hours, month, day (the latter two base don EU or US format)
            if (_ExtendedFormat)
            {
                if (timeString.Length > 14)
                {
                    return false;
                }
            }
            else
            {
                if (timeString.Length > 10)
                {
                    return false;
                }
            }
            int[] values = new int[timeString.Length / 2];
            for(int i=0;i<timeString.Length / 2;i++)
            {
                values[values.Length - 1 - i] = int.Parse(timeString.Substring(i*2,2));
            }
            second=values[0];
            if (values.Length>1)
                minute = values[1];
            if (values.Length>2)
                hour = values[2];
            if (_EuropeanDateFormat)
            {
                if (values.Length>3)
                    month = values[3];
                if (values.Length>4)
                    day = values[4];
            }
            else
            {
                if (values.Length>3)
                    day = values[3];
                if (values.Length>4)
                    month = values[4];
            }
            if (_ExtendedFormat)
            {
                if (values.Length > 5)
                {
                    // handle [yy]yy in last items...
                    if (values.Length>6)
                    {
                        year = values[6] * 100 + values[5];
                    }
                    else
                    {
                        year = ((DateTime.Today.Year / 100) * 100) + values[5];
                    }
                }
            }
            return true;
        }

        private void HandleSetTime(string timeString)
        {
            if (!TryParseTimeByFormat(timeString, out var year, out var month, out var day, out var hour, out var minute, out var second) || !second.HasValue)
            {
                SetErrorBadInstruction();
                return;
            }

            if (timeString.Length == 0 || (timeString.Length % 2) !=0)
            {
                SetErrorBadInstruction();
                return;
            }
            var now = GetSimulatedNow();

            month ??= now.Month;
            day ??= now.Day;
            hour ??= now.Hour;
            minute ??= now.Minute;
            if (!year.HasValue)
            {
                if(month == 2 && day == 29)
                    year = 2000;    // y2k was a leap year, so it should work as pseudo-year.
                else
                    year = 2001;    // for all other dates, assume year 2001, non-leap year so the auto-rollover for 28 days works.
            }
            try
            {
                var newTime = new DateTime(year.Value,month.Value, day.Value, hour.Value, minute.Value, second.Value, 10);  // add 10 milliseconds as per doc.
                SetSimulatedTime(newTime);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
                SetErrorBadInstruction();
            }
        }

        /// <summary>
        /// set to true to simulate an RTC error condition - returns 88:88... as per page 32 of the manual.
        /// </summary>
        public bool RTCError {get;set;}

        public bool TestPointEnabled { get; private set; }

        private TimeSpan _OffsetToRealTime = TimeSpan.Zero;

        private void SetSimulatedTime(DateTime newTime)
        {
            if (_RunRelative && System != null)
            {
                _OffsetToRealTime = newTime.Subtract(this._ResetTime.Add(System.RunTime));
            }
            else
                _OffsetToRealTime = newTime.Subtract(DateTime.Now);
            RTCError = false;
        }

        private DateTime GetSimulatedNow()
        {
            if (_RunRelative && System != null)
            {
                return this._ResetTime.Add(System.RunTime).Add(_OffsetToRealTime);
            }
            else
                return DateTime.Now.Add(_OffsetToRealTime);
        }

        private bool _InterruptStatus = false;
        private bool _InterruptFlag = false;

        private int _LatchInput;
        private bool _HasIOPending;   // basically: the INVERSE output of U12A - mapped to "flag" line and reset with R7 writes...
        private bool _HasInputData; // set via R4 write, reset via internal read...
        private int _LatchOutput;
        private string? _NextCommand = null;
        private int _OutIndex = 0;
        //private string? _OutputBuffer;
        private byte[]? _OutputBuffer = null;
        private int OutputLineMask;
        private bool WaitingForLineMask;
        private int _OutputLength;
        private int TriggerCode;
        private int _ErrorCode;
        private TimeSpan _StartupAt;
        private bool _HasCable;
        private long _LastTimingTick;
        private const string ErrorTime = "88:88:88:88:88";

        protected internal override void WriteIORegister(int regIndex, int value)
        {
            // status register: R5 (1)
            //  write: bit 7 is stored and reproduced upon reading...

            value &= 0xFF;  // the interface only has 8 bit IO. upper 8 bits will be ignored.

            Debug.WriteLine("RTC WRITE {0}:{1:x4} ({1})", regIndex,value);
            switch(regIndex)
            {
                case 0: // R4 
                    _LatchInput = value;    // latch data for reading...
                    _HasInputData = true;
                    break;
                case 3: // R7 
                    _HasIOPending = true;   // trigger internal handling now; inverse of U12A (negative logic!)
                    break;
                case 1: // R5
                    _InterruptStatus = (value & 0x80) != 0;
                    break;
            }
        }
    }
}