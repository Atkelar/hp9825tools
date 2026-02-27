using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Text;

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
        /// <param name="deviceName"></param>
        /// <param name="enableExtension">True to enable the "extended" (rebuilt) version. Supports years and checks/validates leap years.</param>
        /// <param name="configureEuropeanMode">True to use the "european" date format. Usually set with a solder jumper inside the device.</param>
        public RTCDevice(bool configureEuropeanMode = false, bool enableExtension = false, string? deviceName = null)
            : base(9, "HP98035A", deviceName)   // service manuel page 12
        {
            _EuropeanDateFormat = configureEuropeanMode;
            _ExtendedFormat = enableExtension;
            Status = true;  // always set (decoding logic is done by the device manager for convenience.)
        }

        /// <summary>
        /// Resets the RTC module.
        /// </summary>
        protected internal override void Reset()
        {
            base.Reset();
            _InterruptFlag = false;
        }

        public override bool Flag { get => !_HasInput; protected set {} }

        // TODO: provide properties for simulation of "absolute real time" or "reset to current simulation time" only.

        private const string DateFormatUS = "MM:dd:HH:mm:ss";
        private const string DateFormatEuro = "dd:MM:HH:mm:ss";

        private bool _HasError = false;

        protected internal override int ReadIORegister(int regIndex)
        {
            int result = 0;
            // status register: R5 (1)
            //  read: bit 0 = error bit.
            //  read: bit 1 = interrupt bit?
            //  read: bit 5 = signature bit? Always "gnd"

            Debug.WriteLine("RTC READ {0}", regIndex);

            switch(regIndex)
            {
                case 1:
                    result |= _HasError ? 0x1 : 0;
                    result |= _InterruptFlag  ? 0x2: 0;
                    result |= _InvertOutput ? 0x4:0;
                    result |= _InvertInput ? 0x8:0;
                    // TODO: interrupt bit?
                    result |= 0x20; // fixed bit...
                    result |= _DMA ? 0x40 : 0;
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
                _HasError = !HandleCommand(_NextCommand);
                _NextCommand = null;
            }
            bool commandDone = false;
            if (_HasInput)
            {
                if (_LatchInput == 10 || _LatchInput == 47)
                    commandDone = true;
                else
                {
                    char cInput = (char)_LatchInput;
                    if ((cInput >= '0' && cInput <='9') || (cInput >= 'A' && cInput <= 'Z') || cInput == '=')   // ignore any non-relevand characters, as per definition.
                        _CommandInputBuffer.Append(cInput);
                }
                _HasInput = false;
            }
            if (commandDone)
            {
                _NextCommand = _CommandInputBuffer.ToString();
                _CommandInputBuffer.Clear();
            }
        }

        /// <summary>
        /// Reutrn true for "OK", false for "error" status.
        /// </summary>
        /// <param name="command">The command to handle...</param>
        /// <returns></returns>
        private bool HandleCommand(string command)
        {
            if (command.Length==0)
                return true;    // empty commands are ignored...
            switch(command[0])
            {
                case 'S':
                    return HandleSetTime(command.Substring(1));
                case 'R':
                    if (command.Length>1)
                        return false;
                    return HandleRequestTime();
                default:
                    return false;
            }
            return true;
        }

        private bool HandleRequestTime()
        {
            if(RTCError)
                SetOutputBuffer(ErrorTime);
            else
            {
                if(_ExtendedFormat)
                {
                    SetOutputBuffer(GetSimulatedNow().ToString("yyyy:" + (_EuropeanDateFormat ? DateFormatEuro : DateFormatUS)));
                }
                else
                {
                    SetOutputBuffer(GetSimulatedNow().ToString(_EuropeanDateFormat ? DateFormatEuro : DateFormatUS));
                }
            }

            return true;   
        }

        private void SetOutputBuffer(string value)
        {
            _OutIndex = 0;
            _OutputBuffer = value;   // append LF will be done by "empty buffer" condition.
        }

        private bool HandleSetTime(string timeString)
        {
            
            if (timeString.Length == 0 || (timeString.Length % 2) !=0)
                return false;
            // from right to left... update:
            // seconds, minutes, hours, month, day (the latter two base don EU or US format)
            if (_ExtendedFormat)
            {
                if (timeString.Length > 14)
                    return false;
            }
            else
            {
                if (timeString.Length > 10)
                    return false;
            }
            int[] values = new int[timeString.Length / 2];
            for(int i=0;i<timeString.Length / 2;i++)
            {
                values[values.Length - 1 - i] = int.Parse(timeString.Substring(i*2,2));
            }
            var now = GetSimulatedNow();
            int year = now.Year;
            int month = now.Month;
            int day = now.Day;
            int hour = now.Hour;
            int minute=now.Minute;
            int second=values[0];
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
            if (_ExtendedFormat && values.Length > 5)
            {
                // handle [yy]yy in last items...
                if (values.Length>6)
                {
                    year = values[6] * 100 + values[5];
                }
                else
                {
                    year = ((year / 100) * 100) + values[5];
                }
            }
            else
            {
                if(month == 2 && day == 29)
                    year = 2000;    // y2k was a leap year, so it should work as pseudo-year.
                else
                    year = 2001;    // for all other dates, assume year 2001, non-leap year so the auto-rollover for 28 days works.
            }
            try
            {
                var newTime = new DateTime(year,month, day, hour, minute, second, 10);  // add 10 milliseconds as per doc.
                SetSimulatedTime(newTime);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
            return true;
        }

        /// <summary>
        /// set to true to simulate an RTC error condition - returns 88:88... as per page 32 of the manual.
        /// </summary>
        public bool RTCError {get;set;}

        private TimeSpan _OffsetToRealTime = TimeSpan.Zero;

        private void SetSimulatedTime(DateTime newTime)
        {
            _OffsetToRealTime = newTime.Subtract(DateTime.Now);
            RTCError = false;
        }

        private DateTime GetSimulatedNow()
        {
            // TODO handle simulation time offsets...
            return DateTime.Now.Add(_OffsetToRealTime);
        }

        private bool _InterruptStatus = false;
        private bool _InvertOutput = false;
        private bool _InvertInput = false;
        private bool _DMA = false;
        private bool _InterruptFlag = false;

        private int _LatchInput;
        private bool _HasInput;

        private int _LatchOutput;
        private bool _HasOutput;
        private string? _NextCommand = null;
        private int _OutIndex = 0;
        private string? _OutputBuffer;
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
                    break;
                case 3: // R7 
                    _HasInput = true;   // read latch now...
                    {
                        if(_OutputBuffer == null)   
                            _LatchOutput = 10;
                        else
                        {
                            if (_OutIndex < _OutputBuffer.Length)
                            {
                                _LatchOutput = ((int)_OutputBuffer[_OutIndex]) & 0xFF;
                                _OutIndex++;
                            }
                            else
                            {
                                _OutputBuffer = null;
                                _LatchOutput = 10;
                            }
                        }
                    }
                    break;
                case 1: // R5
                    _InterruptStatus = (value & 0x80) != 0;
                    break;
            }
        }
    }
}