using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;

namespace HP9825CPU
{
    /// <summary>
    /// Provides an implementation of the built in tape drive, device select code 1. 
    /// The ROM code does provide switching the select code, so there seems to be a
    /// way of adding "external" tape drives with the same code/controller.
    /// </summary>
    public partial class TapeDrive
        : DeviceBase
    {
        /// <summary>
        /// Creates an instance of the tape drive.
        /// </summary>
        /// <param name="deviceName">A label for the device. Defaults to "TAPE".</param>
        public TapeDrive(string? deviceName = null)
            : base(1, "TAPE", deviceName)
        {
            
        }

        /// <summary>
        /// Triggered whenever the cartridge changes. Can include transitions from and to empty, as well as direct transitions from one to another.
        /// </summary>
        public event EventHandler CartridgeChanged;

        /// <summary>
        /// Triggered whenever the "activity" LED changes state. See the <see cref="ActivityLight"/> property for the current state.
        /// </summary>
        public event EventHandler ActivityChanged;
        /// <summary>
        /// True if the amber activity LED is currently on. This happens at about 10% of the tape speed.
        /// </summary>
        public bool ActivityLight { get => Moving ;}

        protected override void InitializeTracePoints(ITracePointBuilder builder)
        {
            base.InitializeTracePoints(builder);
            _TPSpeedChange = builder.Create("SPDCHG", "Speed changed", TraceCategory.Normal)
                .Describe("Triggers whenever the requested speed for the tape changes.")
                .MessageTemplate("Requested speed changed: [0:0.000] to [1:0.000]ips; Currently [2:0.000]ips @ [3:0.0000]\"")
                .ParameterDefaults(0d, 0d, 0d)
                .Make();
            _TPSpeedupDone = builder.Create("SPDDN", "Speed Done", TraceCategory.Normal)
                .Describe("Triggers when the tape reached the target speed after the acceleration period.")
                .MessageTemplate("Target speed reached: [0:0.000]ips @ [1:0.0000]\"")
                .ParameterDefaults(0d, 0d)
                .Make();
            _TPCommandStateChanged = builder.Create("CMDCHG", "Command Changed", TraceCategory.Normal)
                .Describe("Shows updated command state for the tape drive.")
                .MessageTemplate("Command update: [0] @ [1:0.0000]\" from [*-1]")
                .ParameterDefaults(CommandFlags.None, 0d)
                .Make();
            _TPHole = builder.Create("HOLE", "Hole Detected", TraceCategory.Normal)
                .Describe("Shows that the simulated drive has detected an index hole in the tape.")
                .MessageTemplate("HOLE detected between [0:0.0000]\" and [1:0.0000]\" at speed [2:0.0000]ips.")
                .ParameterDefaults(0d, 0d, 0d)
                .Make();
            _TPHoleReset = builder.Create("HOLERESET", "BET Reset", TraceCategory.Diagnostics)
                .Describe("Shows that the simulated drive had the BET marker (index hole) reset.")
                .MessageTemplate("HOLE reset at [0:0.0000]\" at speed [1:0.0000]ips - from [*-1]")
                .ParameterDefaults(0d, 0d)
                .Make();
            _TPModeSwitch = builder.Create("TPMOD", "Tape mode switched", TraceCategory.Diagnostics)
                .Describe("Shows that the 'working mode' of the tape mechanism has changed.")
                .MessageTemplate(" TAPE MODE SWITCH: [0] -> [1]")
                .ParameterDefaults(TapeMode.Idle, TapeMode.Idle)
                .Make();

            _TPGapStart = builder.Create("GAPNOW", "Gap found", TraceCategory.Diagnostics)
                .Describe("Shows that the tape just enetered a GAP section.")
                .MessageTemplate("GAP found @ [0:0.0000]\" [1]")
                .ParameterDefaults(0d, string.Empty)
                .Make();
            _TPGapEnd = builder.Create("GAPNOT", "Gap endet", TraceCategory.Diagnostics)
                .Describe("Shows that the tape has left a gap region.")
                .MessageTemplate("Moved out of gap @ [0:0.0000]")
                .ParameterDefaults(0d)
                .Make();

            _TPWordWriteDone = builder.Create("WRWDD", "Write word done", TraceCategory.Diagnostics)
                .Describe("Logs completely received words during writes.")
                .MessageTemplate("Received: [0] ([1] in block) @ [2:0.0000]\"")
                .ParameterDefaults(0,0,0d)
                .Make();
            _TPWordReadDone = builder.Create("RDWDDN", "Read word done", TraceCategory.Diagnostics)
                .Describe("A complete word (=16 bits) have been read and set to the output data register.")
                .MessageTemplate("Sent: [0] ([1] in block) @ [2:0.0000]\"")
                .ParameterDefaults(0, 0, 0d)
                .Make();


            _TPBlockTransitionBeforeEnd = builder.Create("BLKTRNINV", "Block transition before end", TraceCategory.Warning)
                .Describe("The current block on tape was left before all the data on it was read and transferred!")
                .MessageTemplate("Block transitioned, left over bits after reading [0] bytes/ [1] bits!! @ [2:0.0000]\"")
                .ParameterDefaults(0,0,0d)
                .Make();
                
            _TPBlockTransitioned = builder.Create("BLKTRN", "Block transitioned", TraceCategory.Diagnostics)
                .Describe("The current block in the read operation changed.")
                .MessageTemplate("Block transitioned @ [0:0.0000]\"")
                .ParameterDefaults(0d)
                .Make();

            _TPInvalidDummyBit = builder.Create("INVFMT", "Invalid formatting", TraceCategory.Warning)
                .Describe("The received low level structure between words and blocks doesn't add up.")
                .MessageTemplate("Tape structure invalid for block @ [0:0.0000] - [1]")
                .Make();


            builder.Create("SLOW", "Simulation Timing Issue", TraceCategory.Performace)
                .Describe("Simulation is running too slow for the tape drive to keep up sending the right event pattern!")
                .MessageTemplate("Simulation might be wonky... [0:0.0000]\" in this tick requested, but [1:0.0000]\" moved.")
                .ParameterDefaults(0d, 0d);
        }

        private bool _Moving;
        private bool Moving 
        {
            get
            { 
                return _Moving;
            }
            set
            {
                if (_Moving != value)
                {
                    _Moving = value;
                    ActivityChanged?.Invoke(this, EventArgs.Empty);
                }
                if (!_Moving)
                    _StatusRegister |= StatusFlags.InterRecordGap;  // a non-moving tape WILL pretend to see a gap... i.e. no-data.
            }
        }

        /// <summary>
        /// State of the write protection in the currently inserted tape. Buffer variable to decouple from the tape's property.
        /// </summary>
        private bool _WriteProtected;

        /// <summary>
        /// Current speed in "ips" (inch per second)
        /// </summary>
        private double _CurrentSpeed;
        /// <summary> 
        /// Target speed in "ips" (inch per second)
        /// </summary>
        private double _TargetSpeed;


        [Flags]
        private enum StatusFlags
        {
            WriteProtect = 0x80,
            Reverse = 0x40,
            // "10% of speed" bit - according to listing line 02693000
            Moving = 0x20,
            InterRecordGap = 0x10,
            //POP = 0x8,    // POP = grounded, inverted, inverted again. So: always zero in real binary...
            CartridgeOut = 0x4,
            ServoFail = 0x2,
            BeginOrEndTape = 0x1,
            None = 0
        }

        [Flags]
        private enum CommandFlags
        {
            None = 0,
            TrackB = 1,
            Search = 2,
            ThresholdHiOrGap = 4,
            Tach = 8,
            Reverse = 0x10,
            Fast = 0x20,
            Write = 0x40,
            Run = 0x80
        }

        private StatusFlags _StatusRegister = StatusFlags.None;
        private CommandFlags _CommandRegister = CommandFlags.None;

        protected internal override void Reset()
        {
            base.Reset();
            _StatusRegister = StatusFlags.None;
            _CommandRegister = CommandFlags.None;
            _BitWriteValueLatch = false;
            _WriteLatched = false;
            if (System!= null)
            {
                _LastTick = System.RunTime;
            }
            _CartridgeOut = Cartridge == null;
            _ServoFailed = false;
            _CurrentSpeed = 0;
            _TargetSpeed = 0;
            _LastStreak = null;
            _LastMode = TapeMode.Idle;
            _SignalInterRecordGap = true;   // assume gap...
            _SignalGap = true;
            _LastTacSignalPosition = double.PositiveInfinity;
            Moving = false;
        }

        protected override void SaveCurrentState(XmlElement target)
        {
            target.SetAttribute("status", _StatusRegister);
            target.SetAttribute("command", _CommandRegister);
            target.SetAttribute("writeLatched", _WriteLatched);
            target.SetAttribute("writeValue", _BitWriteValueLatch);
            target.SetAttribute("lastTick", _LastTick);
            target.SetAttribute("servoFail", _ServoFailed);
            target.SetAttribute("speed", _CurrentSpeed);
            target.SetAttribute("targetSpeed", _TargetSpeed);
            target.SetAttribute("lastMode", _LastMode);
            target.SetAttribute("signalIRG", _SignalInterRecordGap);
            target.SetAttribute("lastTacPos", _LastTacSignalPosition);
            target.SetAttribute("betFlag", _BETFlag);
            target.SetAttribute("cartOut", _CartridgeOut);
            target.SetAttribute("cartridge", Cartridge?.Label);
            target.SetAttribute("cartridgePos", Cartridge?.Position);
            target.SetAttribute("dmarLatch", _DMARLatch);
            target.SetAttribute("readTick", _LastReadTick);
            target.SetAttribute("moving", _Moving);
            target.SetAttribute("rwPulse", _NextRWPulse);
            target.SetAttribute("readBitCount", _ReadBitCounter);
            target.SetAttribute("readBitResult", _ReadBitResult);
            target.SetAttribute("readWordOffset", _ReadWordOffset);
            target.SetAttribute("searchDone", _SearchCompleted);
            target.SetAttribute("head", _SelectedHead);
            target.SetAttribute("gap", _SignalGap);
            target.SetAttribute("wasGap", _WasGap);
            target.SetAttribute("writeBitCount", _WriteBitCount);
            target.SetAttribute("writeProt", _WriteProtected);
            target.SetAttribute("writeStarted", _WriteStarted);
            target.SetAttribute("writeWord", _WriteWord);
            if (_LastStreak != null)
            {
                // we seem to be caught in a write; make sure we recall that...
                var tStreak = target.OwnerDocument.CreateElement("streak", CpuSimulator.StateSaveNamespace);
                target.AppendChild(tStreak);
                tStreak.SetAttribute("start", _LastStreak.Start);
                tStreak.SetAttribute("end", _LastStreak.End);
                tStreak.SetAttribute("count", _LastStreak.Data.Count);
                byte[] buf = new byte[_LastStreak.Data.Count * 2];
                for(int i=0;i<_LastStreak.Data.Count;i++)
                {
                    var w = _LastStreak.Data[i];
                    buf[i*2] = (byte)(w & 0xFF);
                    buf[i*2+1] = (byte)((w >> 8) & 0xFF);
                }
                tStreak.InnerText = Convert.ToBase64String(buf);
            }
        }

        /// <summary>
        /// True if the motor is currenty spinning. Takes into account all the "halt" conditions inside the controller.
        /// </summary>
        private bool MotorOn => (_CommandRegister & CommandFlags.Run)  != 0 && !_CartridgeOut && !_ServoFailed && !_BETFlag && !_SearchCompleted;

        /// <summary>
        /// "SFL" flag - servo failed. This is triggered when an over voltage or over current is detected.
        /// </summary>
        private bool _ServoFailed;
        /// <summary>
        /// True if the cartridge is remvoed from the drive.
        /// </summary>
        private bool _CartridgeOut;
        /// <summary>
        /// Time keeper. The tape mechanism is very timing dependent, so we need to keep track of "simulation time" Between ticks
        /// to push the correct status info, i.e. "bit read ready" or similar.
        /// </summary>
        private TimeSpan _LastTick;

        protected internal override int ReadIORegister(int regIndex)
        {
            switch(regIndex)
            {
                case 0: // R4 => data bit on IOB 0
                    _FlagLatch = false;
                    return _ReadBitResult ? 1 : 0;  // we can now read bits!!!
                case 1: // R5 => Status
                    //Debug.WriteLine("TAPE: read status = {0}", _StatusRegister);
                    return ((int)_StatusRegister) & 0xFF;
                case 2: // R6 => 
                    if(_BETFlag)
                        _TPHoleReset.Invoke(Cartridge?.Position, _CurrentSpeed);
//                        Debug.WriteLine("** Clear Hole indicator");
                    _BETFlag = false;
                    _SearchCompleted = false;
                    return 0;
                default:
                    ReportHardwareAccessError("Reading from TAPE: {0}", regIndex);
                    break;
            }
            return 0;
        }

        /// <summary>
        /// The last bit written to the data port bit 0 - the "tape write bit" value.
        /// </summary>
        private bool _BitWriteValueLatch;    // input (=> IO to tape) bit for next write operation.
        /// <summary>
        /// True if the write bit has been latched since this value has been reset.
        /// </summary>
        private bool _WriteLatched;

        protected internal override void WriteIORegister(int regIndex, int value)
        {
            //Debug.WriteLine("Writing to TAPE: {0} = {1:x2} ({1})", regIndex, value);
            switch  (regIndex)
            {
                case 0: // R4 => data...?
                    _FlagLatch = false;
                    _DMARLatch = false;
                    _BitWriteValueLatch = (value & 1) != 0;
                    _WriteLatched = true;
                    break;
                case 1: // R5 => Status
                    bool oldSearch = ((_CommandRegister & CommandFlags.Search)!=0);
                    _CommandRegister = (CommandFlags)(~value & 0xFF);
                    _StatusRegister &= ~StatusFlags.Reverse; 
                    if ((_CommandRegister & CommandFlags.Reverse) != 0) // reverse... is reversed in the schematic...
                        _StatusRegister |= StatusFlags.Reverse;
                    if (oldSearch != ((_CommandRegister & CommandFlags.Search)!=0))
                    {
                        // serach changed, if "oldsearch" was true, clear "search complete" flag.
                        if (oldSearch)
                            _SearchCompleted = false;
                    }
                    //     throw new NotImplementedException("Got a search request! DMA time!");
                    _TPCommandStateChanged.Invoke(_CommandRegister, Cartridge?.Position);
                    break;
                case 2: // R6 => DMA finished...
                    _DMARLatch = false;
                    _SearchCompleted = true;
                    break;
                case 3: // R7 => clear special status flags...
                    _ServoFailed = false;
                    _CartridgeOut = Cartridge == null;
                    break;
            }
        }

        private bool _FlagLatch, _DMARLatch;

        /// <summary>
        /// Removes the current cartridge from the tape drive.
        /// </summary>
        public void Eject()
        {
            if (Cartridge != null)
            {
                Cartridge.InDrive = null;
                Cartridge.LastUsed = DateTime.UtcNow;
                Cartridge = null;
                OnCartridgeChanged();
            }
            _CartridgeOut = true;
        }

        private void OnCartridgeChanged()
        {
            CartridgeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the currently inserted tape cartridge object. Null if the tape drive is empty.
        /// </summary>
        public TapeCartridge? Cartridge { get; private set; }

        /// <summary>
        /// Inserts a cartridge into the drive.
        /// </summary>
        /// <param name="whichOne">The cartridge object.</param>
        /// <exception cref="InvalidOperationException">Tried to insert the same cartridge object into multiple drives?!</exception>
        public void InsertCartridge(TapeCartridge whichOne)
        {
            if (whichOne == Cartridge)  // meh, don't bother...
                return;

            if (whichOne.InDrive != null)
                throw new InvalidOperationException("Cannot insert a cartridge in multiple drives!");

            if (Cartridge != null)
                Eject();
            
            Cartridge = whichOne;
            Cartridge.LastUsed = DateTime.UtcNow;
            this._WriteProtected = Cartridge.Readonly;
            InitHoles();
            _LastTacSignalPosition = Cartridge.Position;
            whichOne.InDrive = this;
            OnCartridgeChanged();
            // the "cartridge" out should be detected by the next "tick". this gives us time to detect a "fast change" too.
        }

        /// <summary>
        /// Initialize the "hole positions" for the provided cartridge.
        /// </summary>
        /// <exception cref="InvalidOperationException">The length of the tape is implausible.</exception>
        private void InitHoles()
        {
            double len = (Cartridge?.Length).GetValueOrDefault();
            if  (len < 100)
                throw new InvalidOperationException("Cannot have a cartridge with less than 100 inches of tape!");
            // TODO: maybe find a faster way of checking these; at least the locations are now precomputed.
            //first assumption: [29.13 o 0.25 o 15.75 o 0.25 o 15.75 o 0.25 o 23.75 o][content][o 23.75 o 0.25 o 0.25 o]
            // NOTE: The schematic depiction of the three end-of-tape markers is wrong; the service manual
            // on page 17 has a more detailed descriptoin!
            //  610mm -> 24", 305 -> 12"
            //[29.13 o 0.25 o 11.75 o 0.25 o 11.75 o 0.25 o 24 o][content][o 24 o 12 o 12 o]

            this._TapeHolePositions = new double[11];
            _TapeHolePositions[6] = -1;    // assume "first start of tape" hole at -1"
            _TapeHolePositions[5] = _TapeHolePositions[6] - 24;
            _TapeHolePositions[4] = _TapeHolePositions[5] - 0.25;
            _TapeHolePositions[3] = _TapeHolePositions[4] - 11.75;
            _TapeHolePositions[2] = _TapeHolePositions[3] - 0.25;
            _TapeHolePositions[1] = _TapeHolePositions[2] - 11.75;
            _TapeHolePositions[0] = _TapeHolePositions[1] - 0.25;
            _TapeHolePositions[7] = _TapeHolePositions[6] + len;
            _TapeHolePositions[8] = _TapeHolePositions[7] + 24;
            _TapeHolePositions[9] = _TapeHolePositions[8] + 12;
            _TapeHolePositions[10] = _TapeHolePositions[9] + 12;
        }

        internal const double SpeedFastIps = 90;
        internal const double SpeedNormalIps = 22;
        internal const double AccelerationIpsps = 1200;

        // we simulate tape moving speed according to patent page 297
        // speeds are 22 or 90 inches per second.
        // with acceleration of 1200 inches per second².
        // the "servo failed" detection is a current/voltage sense
        // to trip at overcurrent situations.
        // "MOVING signals" MFD, MRV and MFG are created if the servo   
        // is running at > 2ips.
 
        protected internal override void Tick()
        {
            if (System == null)
                return;
            
            var tacFlag = false;
            var rwFlag = false;

            var delta = System.RunTime.Subtract(_LastTick).TotalSeconds;
            var newMode = _LastMode; // keep current mode if we haven't ticked...
            var posNow = Cartridge?.Position ?? 0;
            var preTickGap = _SignalGap;
            double dirFlag = 0;
            if (delta > 0)
            {
                newMode =  TapeMode.Idle;   // assume idle until proven otherwise...
                _LastTick = System.RunTime;
                // simulated forward time... accelerate tape if needed, move cartridge, send pulses...
                // step #1: update target speed...
                double newSpeed;
                if (this.MotorOn)
                {
                    if ((this._CommandRegister & CommandFlags.Fast)!=0)
                        newSpeed = SpeedFastIps;
                    else
                        newSpeed = SpeedNormalIps;
                    if ((this._CommandRegister & CommandFlags.Reverse)!=0)
                        newSpeed *= -1; // reverse!
                }
                else
                {
                    newSpeed = 0;
                }
                var speedDelta = AccelerationIpsps * delta;
                if (_TargetSpeed != newSpeed)
                {
                    //TracePoint("name", newSpeed);
                    _TPSpeedChange.Invoke(_TargetSpeed, newSpeed, _CurrentSpeed, Cartridge?.Position);
                    // Debug.WriteLine("Tape speed change. Current target = {0:0.000}ips, new target = {1:0.000}ips @ {2:0.000}\"", _TargetSpeed, newSpeed, Cartridge?.Position);
                    // Debug.WriteLine("  Accelerating tape: {0:0.000}ips to {1:0.000}ips, delta = {2:0.000}ips", _CurrentSpeed, newSpeed, speedDelta);
                    _TargetSpeed = newSpeed;
                }

                if (_TargetSpeed != _CurrentSpeed)
                {
                    // accelerate!
                    if (_TargetSpeed < _CurrentSpeed)
                    {
                        // slow down or reverse...
                        _CurrentSpeed -= speedDelta;
                        if(_CurrentSpeed < _TargetSpeed)
                            _CurrentSpeed = _TargetSpeed; 
                    }
                    else
                    {
                        // speed up or forward...
                        _CurrentSpeed += speedDelta;
                        if(_CurrentSpeed > _TargetSpeed)
                            _CurrentSpeed = _TargetSpeed; 
                    }
                    if (_TargetSpeed == _CurrentSpeed)
                        _TPSpeedupDone.Invoke(_CurrentSpeed, Cartridge?.Position);
                }
                if(_CurrentSpeed != 0 && Cartridge != null)
                {
                    // NOTE 1: Tac signal is 23kHz for 22ips - thus we have 1045 pulses per inch (hardware)
                    // NOTE 2: Tac is doubled internally in the drive but halved again when fed to the FLG status input, which halves it again...
                    // NOTE 3: We thus set the FLG every 483th of an inch of movement; the exact value is from the Driver code. And THAT means...
                    // NOTE 4: if the simulated time since last tick is MORE than 483th of a second, we need to "slow down" the tape and move less...
                    
                    // NOTE 5: GAP detection: no flux transition (bits) for 125µs = GAP signal...
                    // NOTE 6: IRG detection: no flux transition (bits) for 2.5ms = IRG signal...
                    // NOTE 7: GAP(and IRG) are only reset after minimum of 4 flux transitions (bits)
                    // NOTE 8: first 12 bits after a GAP are always "0" bits for speed alignment.

                    // NOTE 9: Bits zero = short, bits one = long

                    var posDelta = _CurrentSpeed * delta;
                    if (delta > MinTickTime)
                    {
                        posDelta = MinTickTime * _CurrentSpeed;
                        TracePoint("SLOW", _CurrentSpeed * delta, posDelta);
                    }

                    // new mode detection; current mode is in "_LastMode".
                    if ((_CommandRegister & CommandFlags.Write)!=0)
                    {
                        newMode = (_CommandRegister & CommandFlags.ThresholdHiOrGap) != 0 ? TapeMode.WriteGap : TapeMode.WriteData;
                        if (_CurrentSpeed < 0)
                        {
                            ReportHardwareAccessError("Trying to write in reverse! Simulating servo fail to stop!");
                            _ServoFailed = true;
                        }
                    }
                    else
                        newMode = TapeMode.ReadingData; // (_CommandRegister & CommandFlags.ThresholdHiOrGap) != 0 ? TapeMode.ReadingGaps : TapeMode.ReadingData;

                    var posOld = Cartridge.Position;
                    posNow += posDelta;
                    
                    // handle low level "holes" signalling here!

                    bool inHole = false;
                    
                    for(int i=0;i<_TapeHolePositions.Length;i++)
                    {
                        double a,b;
                        if (posDelta < 0)
                        {
                            a= posNow;
                            b=posOld;
                        }
                        else
                        {
                            b= posNow;
                            a=posOld;
                        }
                        var x = _TapeHolePositions[i];
                        if (a <= x && b>=x)
                        {
                            inHole = true;
                            break;
                        }
                    }
                    if (inHole)
                    {
                        _TPHole.Invoke(posOld, posNow, _CurrentSpeed);
                        _BETFlag = true;
                    }

                    dirFlag = _CurrentSpeed > 0 ? 1 : -1;
                    if(_LastTacSignalPosition == double.PositiveInfinity)
                    {
                        _LastTacSignalPosition = posNow;
                        tacFlag = true;
                    }
                    else
                    {
                        if (Math.Abs(posNow - _LastTacSignalPosition) > FlagTacEveryInch)
                        {
                            // we moved past the flag clock... send out a tac pulse...
                            tacFlag = true;
                            _LastTacSignalPosition += dirFlag * FlagTacEveryInch; // keep tac signal in sync...
                        }
                    }

                    // NOTE! bit clock/inch is depending on "0" or "1" bits. If the last read bit was 0, it is short, if 1 it is long. 
                    // It seems that the tape capacity is "worst case", i.e. all 1s. So bit clock per inch is derived from that...
                    // According to the service manual, the "1" bit case is about 1.75 longer than the "0" distance. Page 71. 
                    //  That wording is loaded... is it "1.75*len" or is it "1.75*len+len"? The patent doesn't go into numeric details here...
                    // ... so let's assume the shorter case: 1Len = 1.75*0Len; so that makes 0Len = 1Len/1.75...

                    // the write process is a bit convoluted... the following conditions are defined:

                    // Write = enabled, Mode = enabled -> write GAP. No Flux transitions are recorde.
                    // Write = enabled, Mode = disabled -> start write when this mode becomes active:
                    //                                     "a short while" after arriving here, a Flux 
                    //                                      transition is recorded and the next (first) 
                    //                                      bit is loaded. The first 12 bits after a gap
                    //                                      are supposed to be zeros, every 17th bit is 1.


                    if ((_CurrentSpeed > 0 && posNow > _NextRWPulse) || (_CurrentSpeed < 0 && posNow < _NextRWPulse))
                    {
                        rwFlag = true;
                    }

                    Cartridge.Position = posNow;
                }
                else
                {
                    // no tape present, or speed == 0
                    _NextRWPulse = null;
                }
            }

            // if ((_CommandRegister & CommandFlags.Search) != 0)
            //     throw new NotImplementedException();    // yikes!

            // handle mode change...
            if (newMode != _LastMode)   // mode transition... handle wrap ups...
            {
                _TPModeSwitch.Invoke(_LastMode, newMode);
                // first case: transition from any mode to a write mode...
                switch (newMode)
                {
                    case TapeMode.WriteData:
                        _SelectedHead = (_CommandRegister & CommandFlags.TrackB) == 0 ? 0 : 1;
                        if (!_WriteStarted.HasValue)    // we might start without a gap...
                            _WriteStarted = posNow;
                        // coming from write gap?
                        if (_LastStreak != null)
                            throw new InvalidOperationException();
                        _LastStreak = 
                            new DataStreak(_WriteStarted.Value, posNow, posNow- _WriteStarted.Value);
                        // GOTCHA!
                        //Debug.WriteLine("First data bit started: {0} {1} {4} {2} {3} {5} {6}", _BitWriteValueLatch, _LastMode, _NextRWPulse, _WriteLatched, posNow, rwFlag, Flag);
                        if (dirFlag <= 0)    
                            Debug.WriteLine("Write data started with non-forward direction?!");

                        // first pulse to start "bit 0" of the block will be here...
                        _NextRWPulse = posNow + dirFlag * FirstBitClockAfterInch;
                        _WriteBitCount = 0;
                        _WriteWord = 0;
                        _FirstBitFlag = !Flag;//!(_LastMode == TapeMode.ReadingData); // first bit issue with "read/write" transition.
                        _BitWriteValueLatch = false;
                        _WriteLatched = false;
                        break;
                    case TapeMode.WriteGap:
                        _SelectedHead = (_CommandRegister & CommandFlags.TrackB) == 0 ? 0 : 1;
                        // we switched into GAP recording mode. Make sure the last one is done.
                        CloseStreak();
                        // if (!_WriteStarted.HasValue)
                        //     _WriteStarted = posNow;
                        // if (_LastMode == TapeMode.WriteData)
                        // {
                        //     Debug.WriteLine("Switched from data to gap mode?!");
                        // }
                        _WriteStarted = posNow; // continue on with a gap...
                        _NextRWPulse = null;// posNow + dirFlag * FirstBitClockAfterInch;
                        break;
                    case TapeMode.ReadingData:
                    //case TapeMode.ReadingGaps:
                        // initialize reading operation...
                        // keep track of... 
                        //  1.: detect if we are running in a gap or data...
                        //  2.: if reading back or forward, simulate the bit transitions in data...
                        //  3.: if the gap is going on for more than
                        //_LastReadTick = _LastTick;  // gap detection is time based...
                        _GapStartedAtTime = null;   //
                        _ReadBitResult = false;  // make sure we start over with "0" result...
                        _SelectedHead = ((_CommandRegister & CommandFlags.TrackB) == 0) ? 0 : 1;
                        _NextRWPulse = posNow + dirFlag * FirstBitClockAfterInch;
                        goto default;   // make sure we close any pending writes...
                    default:
                        // turn off recording?
                        if (_WriteStarted.HasValue)
                        {
                            if (_LastStreak == null)
                                _LastStreak = new DataStreak(_WriteStarted.Value, posNow, posNow - _WriteStarted.Value);
                            CloseStreak();
                        }
                        _ReadBitResult = false;
                        break;
                }
            }
            else
            {
                // mode is holding...
                switch (newMode)
                {
                    case TapeMode.ReadingData:
                    //case TapeMode.ReadingGaps:
                        if (Cartridge != null)
                        {
                            // in high-speed mode, or in reverse only gap detection is working...
                            // might have to fake bits in reverse too, no idea if that is a thing, it certainly is only ever read forward...
                            bool handleDataBits = (_CommandRegister & CommandFlags.Fast) == 0 && (_CommandRegister & CommandFlags.Reverse) == 0 && (_CurrentSpeed > 0) && (_CommandRegister & CommandFlags.Tach) ==0;
                            // find out where we are now...
                            var currentBlock = Cartridge!.GetBlockAt(_SelectedHead, posNow);
                            if (currentBlock == null)
                            {
                                // empty part of tape... assume "gap"...
                                if (!_GapStartedAtTime.HasValue)
                                {
                                    _TPGapStart.Invoke(posNow, "(EOT)");
                                    _GapStartedAtTime = _LastTick;
                                }
                                HandleGapFlagsWhenInGap();
                                _LastBlock = null;
                            }
                            else
                            {
                                if (_LastBlock != currentBlock)
                                {
                                    if (_ReadBitCounter != 0)
                                        _TPBlockTransitionBeforeEnd.Invoke(_ReadWordOffset, _ReadBitCounter, posNow);
                                    else
                                        _TPBlockTransitioned.Invoke(posNow);

                                    _LastBlock = currentBlock;
                                    _ReadWordOffset = 0;
                                    _ReadBitCounter = 0;
                                }
                                // are wen in a gap...
                                if (posNow <= currentBlock.Start+currentBlock.Gap)
                                {
                                    // yes!
                                    if (!_GapStartedAtTime.HasValue)
                                    {
                                        // we just started a gap...
                                        _TPGapStart.Invoke(posNow);
                                        _GapStartedAtTime = _LastTick;
                                    }
                                    HandleGapFlagsWhenInGap();
                                }
                                else
                                {
                                    if (_SignalGap)
                                    {
                                        _TPGapEnd.Invoke(posNow);
                                        HandleGapFlagsWhenLeftGap();
                                        _NextRWPulse = posNow + dirFlag * FirstBitClockAfterInch;
                                        _WasGap = true;
                                    }
                                    // no...
                                    if (handleDataBits)
                                    {
                                        // If we just transitioned out of a gap, be sure to update the counters...
                                        if (!_WasGap)
                                        {
                                            // are we continuing data bit decoding? RWFLAG IS SET HERE!
                                            if (rwFlag)
                                            {
                                                // YES! Decoded a bit!
                                                if (_InitBitCounter<12) // first 12 bits are ALWAYS zero. Hardcoded.
                                                {
                                                    //if (_ReadWordOffset == 0) Debug.WriteLine("*I");
                                                    _ReadBitResult = false;//_InitBitCounter == 0;
                                                    _InitBitCounter++;
                                                    _ReadBitCounter++;
                                                }
                                                else
                                                {
                                                    if (_ReadWordOffset < currentBlock.Data.Count)
                                                    {
                                                        if (_ReadBitCounter>0)
                                                        {
                                                            //if (_ReadWordOffset == 0) Debug.WriteLine("*N");
                                                            var mask = 1 << (16 - _ReadBitCounter);
                                                            _ReadBitResult = (currentBlock.Data[_ReadWordOffset] & mask) != 0;
                                                            _ReadBitCounter++;
                                                            if (_ReadBitCounter == 17)
                                                            {
                                                                _TPWordReadDone.Invoke(currentBlock.Data[_ReadWordOffset], _ReadWordOffset, posNow);
                                                                _ReadWordOffset++;
                                                                _ReadBitCounter = 0;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            //if (_ReadWordOffset == 0) Debug.WriteLine("*E");
                                                            // return extra "1" bit.    written *after* the bits, but read before... x.x
                                                            _ReadBitResult = true;
                                                            _ReadBitCounter++;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // ummm... should *not* happen... 
                                                        _GapStartedAtTime = _LastTick;
                                                    }
                                                }
                                                _NextRWPulse = (_NextRWPulse ?? posNow) + (_ReadBitResult ? Bit1ClockEveryInch : Bit0ClockEveryInch) * dirFlag;
                                            }
                                            else
                                            {
                                                // continue...
                                            }
                                        }
                                        else
                                        {
                                            // are we starting data bit decoding?
                                            _WasGap = false;
                                            _InitBitCounter = 0;
                                            _NextRWPulse = posNow + Bit0ClockEveryInch * dirFlag;
                                            _ReadWordOffset = 0;
                                            _ReadBitCounter = 0;
                                        }
                                    }
                                }
                            }
                        }

                        break;
                    case TapeMode.WriteGap:
                        // do nothing...
                        break;
                    case TapeMode.WriteData:
                        // handle possible bit transition!
                        if (rwFlag) // YES!
                        {
                            if (!_FirstBitFlag) // first "flux transition" in new block is for "start bit", ignore as data bit!
                            {
                                _WriteLatched = false;  // waiting for next one...
                                // fetch next bit to write.
                                bool bit = _BitWriteValueLatch;
                                // NOTE: the first block (aka "dead zone" in the firmware) is written without pushing new bits;
                                //       to prevent countless warnings, this is removed for now.
                                // if (!_WriteLatched)
                                //     Debug.WriteLine("WARNING: write bit not received in time!");
                                _WriteBitCount++;
                                if(_WriteBitCount <= 16)
                                {
                                    _WriteWord <<= 1;
                                    if (_BitWriteValueLatch)
                                        _WriteWord |= 1;
                                }
                                else
                                {
                                    if (!_BitWriteValueLatch)
                                        _TPInvalidDummyBit.Invoke(posNow, "Dummy bit not set!");
                                    _TPWordWriteDone.Invoke(_WriteWord, _LastStreak?.Data.Count, posNow);
                                    if (_LastStreak?.Data.Count == 0 && _WriteWord != 1)
                                        _TPInvalidDummyBit.Invoke(posNow, "Preamble not 1!");

                                    // NOTE: incoming word logging disabled for simplicity.
                                    //Debug.WriteLine("Received word {0:x4}", _WriteWord);
                                    _LastStreak?.Data.Add((ushort)(_WriteWord & 0xFFFF));
                                    _WriteWord = 0;
                                    _WriteBitCount = 0;
                                }
                            }
                            else
                            {
                                //rwFlag = false; // skip initial bit!
                            }
                            _FirstBitFlag = false;
                            _NextRWPulse = posNow + dirFlag * (_BitWriteValueLatch ? Bit1ClockEveryInch : Bit0ClockEveryInch);
                        }
                        break;
                }
            }
            _LastMode = newMode;

            if (_SignalGap && !preTickGap && (_CommandRegister & CommandFlags.Search)!=0)
            {
                _DMARLatch = true;  // signal!
            }

            if (_CartridgeOut)
                _StatusRegister |= StatusFlags.CartridgeOut | StatusFlags.WriteProtect; // with no cartridge, the WP flag is also always set; nothing to push the button.
            else
            {
                _StatusRegister &= ~StatusFlags.CartridgeOut;
                if (_WriteProtected)
                    _StatusRegister |= StatusFlags.WriteProtect;
            }
            if(_ServoFailed)
                _StatusRegister |= StatusFlags.ServoFail;
            else
                _StatusRegister &= ~StatusFlags.ServoFail;

            if (_SignalInterRecordGap)
                _StatusRegister |= StatusFlags.InterRecordGap;
            else
                _StatusRegister &= ~StatusFlags.InterRecordGap;

            // set flag/status according to current state...
            if (Moving = Math.Abs(_CurrentSpeed)>Math.Abs(_TargetSpeed / 10))  // "MVG" signal...
            {
                _StatusRegister |= StatusFlags.Moving;
            }
            else
            {
                _StatusRegister &= ~StatusFlags.Moving;
            }
            if(_BETFlag)
            {
                _StatusRegister |= StatusFlags.BeginOrEndTape;
            }
            else
            {
                _StatusRegister &= ~StatusFlags.BeginOrEndTape;
            }

            if (((_CommandRegister & CommandFlags.Run) != 0 && (_ServoFailed || _CartridgeOut || _BETFlag || _SearchCompleted)))
            {
                _FlagLatch = true;  // error flag set...
                _NextRWPulse = null;
            }
            else
            {
                // flag hasn't been set by the "PS" trigger
                if ((_CommandRegister & CommandFlags.Tach) != 0)
                {
                    if (tacFlag)
                        _FlagLatch = true;
                }
                else
                {
                    // the RW flag is just a ticker for the individual bits...
                    // real HW derives that from tape speed detection circuitry,
                    // we can hardcode a delta tape path...
                    // sensible value...?
                    // specs say: typical 2750 bytes per second.
                    //            we write words, with one bit extra, i.e. 17bit chunks.
                    //            we have 1375 words per second... add one bit...
                    //            is 23375 bits per second... approx. at 22ips.
                    //            is ~1062.5 bits per inch... so wee need a clock pulse every 1/1062.5 inches...
                    //            The value is suspiciously close to 1024... let's go with that...
                    if (rwFlag)
                        _FlagLatch = true;
                }
            }

            DMAR = _DMARLatch && (_CommandRegister & CommandFlags.Search)!=0;
            Status = _SignalGap && ((_CommandRegister & CommandFlags.Search)==0) && ((_CommandRegister & CommandFlags.Run) != 0 && !(_ServoFailed || _CartridgeOut || _BETFlag || _SearchCompleted)); 
            Flag = _FlagLatch;
        }

        private void HandleGapFlagsWhenLeftGap()
        {
            _SignalGap = false;
            _SignalInterRecordGap = false;
            _GapStartedAtTime = null;
            _ReadBitResult = false; // Make sure we read "0" after a gap!
            _InitBitCounter = 0;    // gap resets block info...
            _ReadBitCounter = 0;
            _WasGap = true;
        }

        private void HandleGapFlagsWhenInGap()
        {
            if(!_GapStartedAtTime.HasValue)
            {
                return;
            }
            _InitBitCounter = 0;    // gap resets block info...
            _ReadBitCounter = 0;
            _NextRWPulse = null;    // make sure we *do* pulse as soon as we get out of the gap!
            var gapDuration = _LastTick.Subtract(_GapStartedAtTime.Value).TotalMicroseconds;
            if(gapDuration >= 125)
                _SignalGap = true;
            if (gapDuration > 2500)
                _SignalInterRecordGap = true;
        }

        private TapeMode _LastMode = TapeMode.Idle;
        private int _InitBitCounter;
        private double? _NextRWPulse = null;
        private int _ReadWordOffset;
        private int _ReadBitCounter;

        private enum TapeMode
        {
            Idle,
            ReadingData,
            //ReadingGaps,
            WriteData,
            WriteGap
        }

        private void CloseStreak()
        {
            if (_LastStreak != null && Cartridge != null)
            {
                if (_WriteBitCount > 0)
                {
                    if (_WriteBitCount < 16)
                    {
                        _TPInvalidDummyBit.Invoke(Cartridge.Position, $"Leftover bits at end of block: only {_WriteBitCount} received!");
                    }
                    else
                    {
                        //_TPInvalidDummyBit.Invoke(Cartridge.Position, $"Leftover bits at end of block: only {_WriteBitCount} received!");
                        _LastStreak.Data.Add((ushort)_WriteWord);
                    }
                }
                // we just stopped writing...
                var posTarget = Cartridge.Position;
                _LastStreak.End = posTarget;
                Cartridge.WriteBlock(_SelectedHead, _LastStreak);
                _LastStreak = null;
                _WriteStarted = null;
                // we collected values between started and now...
            }
            _WriteLatched = false;
            _WriteWord = 0;
            _WriteBitCount = 0;
        }

        private DataStreak? _LastStreak = null;
        private int _SelectedHead;

        private double? _WriteStarted = null;

        private int _WriteBitCount = 0;

        private const double Bit1ClockEveryInch = 1d / 1024d;
        internal const double Bit0ClockEveryInch = Bit1ClockEveryInch / 1.75;
        private const double FirstBitClockAfterInch = Bit1ClockEveryInch;   // assumption... validate?

        private bool _SearchCompleted;

        private bool _BETFlag;
        private bool _SignalInterRecordGap;         // state of the "IRG" line
        private bool _SignalGap;                    // state of the "GAP" line.

        private double _LastTacSignalPosition;

        //private const double FlagClockEveryInch = 1d / 523d;

        /// <summary>
        /// Found in source listing line 02710000 
        /// </summary>
        private const double TacPulsesPerInch = 483d;
        private const double FlagTacEveryInch = 1d / TacPulsesPerInch;

        private double[]? _TapeHolePositions = null;
        private int _WriteWord;
        private bool _FirstBitFlag;
        private TimeSpan _LastReadTick;
        private TimeSpan? _GapStartedAtTime;
        private bool _ReadBitResult;
        /// <summary>
        /// last used read bit block... to detect changes over time
        /// </summary>
        private DataStreak? _LastBlock;
        private ITracePointTrigger _TPSpeedChange;
        private ITracePointTrigger _TPSpeedupDone;
        private ITracePointTrigger _TPCommandStateChanged;
        private ITracePointTrigger _TPHole;
        private ITracePointTrigger _TPHoleReset;
        private ITracePointTrigger _TPModeSwitch;
        private ITracePointTrigger _TPGapStart;
        private ITracePointTrigger _TPGapEnd;
        private ITracePointTrigger _TPWordWriteDone;
        private ITracePointTrigger _TPWordReadDone;
        private ITracePointTrigger _TPBlockTransitionBeforeEnd;
        private ITracePointTrigger _TPBlockTransitioned;
        private ITracePointTrigger _TPInvalidDummyBit;
        private bool _WasGap;
        private const double MinTickTime = 1d / (TacPulsesPerInch * SpeedFastIps);
   }
}