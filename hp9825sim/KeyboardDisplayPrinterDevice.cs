using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using HP9825CPU;

namespace HP9825Simulator
{
    /// <summary>
    /// Simulates the main input/output unit of the HP 9825A: Display (40 char), printer (16 char), and keyboard.
    /// </summary>
    public class KeyboardDisplayPrinterDevice
        : DeviceBase
    {

        // charset is an approximation to keep within the BMP of unicode.
        // Known "issues": 2 = X with a bar, 3 = N with a bar, 6 = n with a bar.
        // assumed: N is actually ň and Ň, for X - no plausible alternative found, 
        // using CHI instead
        private const string Charset =
            @"◀¿χŇαϑΓňΔσ↓λμ←τΦ" +   // 0-15
            @"ΘΩδÅåÄäÖöÜüӔӕ²£▒" +    // 16-31
            @" !""#$%&'()*+,-./" +   // 32-47 
            @"0123456789:;<=>?" +    // 48-63
            @"@ABCDEFGHIJKLMNO" +    // 64-79
            @"PQRSTUVWXYZ[√]↑_" +    // 80-95
            @"`abcdefghijklmno" +    // 96-111
            @"pqrstuvwxyzπ|→ΣͰ";    // 112-127

        private bool _HotReset;

        public KeyboardDisplayPrinterDevice(bool use32CharDisplay = true)
            : base("KDP", null)
        {
            _HotReset = false;
            _DisplayLength = use32CharDisplay ? 32 : 16;
            _CurrentDispay = new string(' ', _DisplayLength);
            _CurrentCursorDisplay = new string(' ', _DisplayLength);
            _DisplayBuffer = new char[_DisplayLength];
            _IsCursor = new bool[_DisplayLength];
            _DefaultKeyDelay = TimeSpan.FromMilliseconds(100);
            _LastKeyWasPressed = TimeSpan.Zero;
            _CursorTick = false;
            _PrinterBuffer = new char[16];
        }

        public string Display => _CursorTick && _AnyCursorVisible ? _CurrentCursorDisplay : _CurrentDispay;

        private bool _CursorTick;
        private int _DisplayLength;
        private bool _AnyCursorVisible;
        private string _CurrentDispay, _CurrentCursorDisplay;
        private char[] _DisplayBuffer;
        private int _CurrentOffset = 0;

        private string LastPrintedLine = string.Empty;
        private char[] _PrinterBuffer;
        private int _CurrentPrinterOffset = 0;

        private readonly TimeSpan CursorTicker = TimeSpan.FromMilliseconds(400);

        private readonly TimeSpan PrintLineDuration = TimeSpan.FromMilliseconds(600);

        protected override void Reset()
        {
            // We don't really reset anything in the display;
            // the firmware has to overwrite the line anyhow. 
            // Tested on RL device: a soft-reset keeps the display
            // buffer content and shifts in new characters!
            base.Reset();
            RunLight = true;    // TODO: validate... schematic on page 180 of KDP is a bit odd; Q where Qnot should be leads to BUSYnot...
            InsertCursor = true;
            PrinterBusy = false;
            _PrinterDone = TimeSpan.Zero;
            _LastCursorTick = TimeSpan.Zero;
            LastPrintedLine = string.Empty;
        }

        protected override int ReadIORegister(int regIndex)
        {
            // R5 (1) => status flag.
            // R4 => keyboard code: 0x80 => shift, 0x7F keyboard matrix code of "pressed" key.

            // sbOut.AppendFormat("R-{0}", regIndex);
            // sbOut.AppendLine();
            switch (regIndex)
            {
                case 0:
                    if (_CurrentKey.HasValue)
                    {
                        var k = _CurrentKey.Value;
                        _CurrentKey = null;
                        return (int)k.Key | (k.WithShift ? 0x80 : 0);
                    }
                    break;
                case 1:     // system status...
                    int flag = 0;
                    if (!_HotReset)
                        flag |= 8;
                    if (_DisplayLength == 16)
                        flag |= 1;
                    if (PaperOut)       // TODO: check if inverted!
                        flag |= 2;
                    if (PrinterBusy)       // TODO: check if inverted!
                        flag |= 4;
                    return flag;
            }
            return 0;
        }

        public bool PrinterBusy { get; set; }

        private TimeSpan _PrinterDone;

        public bool PaperOut { get; set; }

        private bool _RunLight, _InsertCursor;
        public bool RunLight { get => _RunLight; set { if (value != _RunLight) { _RunLight = value; _HasChanged = true; } } }

        public bool InsertCursor
        {
            get => _InsertCursor;
            set
            {
                if (value != _InsertCursor)
                {
                    _InsertCursor = value;
                    var sb = new StringBuilder();
                    UpdateCursorVersion(sb);
                    _CurrentCursorDisplay = sb.ToString();
                    _HasChanged = true;
                }
            }
        }

        public bool ShiftLock { get => _ShiftLock; }


        private struct KeySimulationDetails
        {
            public HP9825Key Key;
            public bool WithShift;
            public TimeSpan DelayFromLastKey;
        }

        TimeSpan _LastKeyWasPressed;

        private Queue<KeySimulationDetails> _SimulatorKeys = new Queue<KeySimulationDetails>();

        private TimeSpan _DefaultKeyDelay;
        public TimeSpan DefaultKeyDelay { get => _DefaultKeyDelay; set { if (value.TotalMilliseconds < 25) throw new ArgumentOutOfRangeException(nameof(value), value, "At least 25ms required between keys..."); _DefaultKeyDelay = value; } }

        /// <summary>
        /// Simulate a "normal" key press and release. Does handle shift/shift lock internally. Note: will queue the key into the simulation buffer, and "press" it with the next "tick".
        /// </summary>
        /// <param name="code">The key in question.</param>
        /// <param name="withShift">The key was pressed while the "shift" key was also down.</param>
        /// <param name="delayFromLast">The delay (in simulation time) to wait between the key presses.</param>
        public void PutKeyPress(HP9825Key code, bool withShift = false, TimeSpan? delayFromLast = null)
        {
            _SimulatorKeys.Enqueue(new KeySimulationDetails() { Key = code, WithShift = withShift, DelayFromLastKey = delayFromLast.GetValueOrDefault(_DefaultKeyDelay) });
        }

        protected virtual void OnDisplayChanged()
        {
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }

        private KeySimulationDetails? _CurrentKey = null;

        private TimeSpan _LastCursorTick;

        protected override void Tick()
        {
            if (System == null)
                return;
            if (_BeepScheduled)
            {
                _BeepScheduled = false;
                OnBeep();
            }
            if (PrinterBusy && System.RunTime > _PrinterDone)
            {
                PrinterBusy = false;
                OnLinePrinted(LastPrintedLine);
            }
            if (_AnyCursorVisible && _LastCursorTick.Add(CursorTicker) < System.RunTime)
            {
                // don't waste ticks for invisible cursors...
                _CursorTick = !_CursorTick;
                _LastCursorTick = System.RunTime;
                _HasChanged = true;
            }
            if (!_CurrentKey.HasValue)
            {
                if (_SimulatorKeys.Count > 0)  // poke CPU!
                {
                    // first, check if enough "time" has passed...
                    if (System.RunTime.Subtract(_LastKeyWasPressed) >= _SimulatorKeys.Peek().DelayFromLastKey)
                    {
                        // got a new key!
                        _CurrentKey = _SimulatorKeys.Dequeue();
                        _LastKeyWasPressed = System.RunTime;
                        //Debug.WriteLine("{2,18} Pressing key {0}{1}", _CurrentKey.Value.WithShift ? "Shift-" : "", _CurrentKey.Value.Key, System?.RunTime);
                        RequestInterrupt(); // TODO: handle key repeat and aging...
                    }
                }
            }
            if (_HasChanged)
            {
                _HasChanged = false;
                OnDisplayChanged();
            }
        }

        public event EventHandler<LinePrintedEventArgs>? PrintedLine;

        protected virtual void OnLinePrinted(string lastPrintedLine)
        {
            if (PrintedLine != null)
            {
                PrintedLine(this, new LinePrintedEventArgs(LastPrintedLine));
            }
        }

        protected virtual void OnBeep()
        {
            Beep?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? DisplayChanged;
        public event EventHandler? Beep;

        // System.Text.StringBuilder sbOut = new System.Text.StringBuilder();
        private bool _HasChanged;

        protected override void WriteIORegister(int regIndex, int value)
        {
            value = value & 0xFF;   // just in case; we only have an 8 bit connection to the bus!
            // write to register R4 (=index 0) => write to display line.
            // display collects all the characters, right to left
            switch (regIndex)
            {
                case 0:
                    _DisplayBuffer[_CurrentOffset] = (char)value;
                    _CurrentOffset++;
                    if (_CurrentOffset >= _DisplayBuffer.Length)
                        _CurrentOffset = 0;
                    break;
                case 2:     // R6 = printer buffer...
                    //Debug.WriteLine("Printer data: {0:x2} ({1})", value, (char)value);
                    if (PrinterBusy)
                        return;
                    _PrinterBuffer[_CurrentPrinterOffset] = (char)(value & 0x7F);
                    _CurrentPrinterOffset++;
                    if (_CurrentPrinterOffset >= _PrinterBuffer.Length)
                        _CurrentPrinterOffset = 0;
                    break;
                case 1:
                    // R5 = control run light, cursor mode and "buffer flushing".
                    switch (value & 24)
                    {
                        case 16:
                            RunLight = false;
                            break;
                        case 8:
                            RunLight = true;
                            break;
                        case 24:
                            RunLight = !RunLight;   // J/K flip flop: both inputs... should toggle.
                            break;
                    }
                    switch (value & 96)
                    {
                        case 96:
                            InsertCursor = !InsertCursor; // J/K flip flop: both inputs... should toggle.
                            break;
                        case 32:
                            InsertCursor = true;
                            break;
                        case 64:
                            InsertCursor = false;
                            break;
                    }
                    if ((value & 4) != 0)
                    {
                        // beep!
                        _BeepScheduled = true;
                    }
                    if ((value & 2) != 0)
                    {
                        // flush display buffer to display!
                        FlushDisplayBufferNow();
                    }
                    if ((value & 1) != 0)
                    {
                        FlushPrinterBufferNow();
                    }
                    break;
                default:
                    break;
            }

            // sbOut.AppendFormat("W-{0}: {1}", regIndex, value);
            // sbOut.AppendLine();
        }

        private void FlushPrinterBufferNow()
        {
            //Debug.WriteLine("Flush printer buffer");
            if (PrinterBusy || System == null)    // umm... shouldn't happen!
                return;
            PrinterBusy = true;
            _PrinterDone = System.RunTime.Add(PrintLineDuration);
            var sb = new StringBuilder();
            int co = _CurrentPrinterOffset < 0 ? 0 : _CurrentPrinterOffset;

            for (int i = co; i < _PrinterBuffer.Length; i++)
            {
                sb.Append(Charset[_PrinterBuffer[i] & 0x7F]);
            }
            for (int i = 0; i < co; i++)
            {
                sb.Append(Charset[_PrinterBuffer[i] & 0x7F]);
            }
            LastPrintedLine = sb.ToString();
        }

        private bool[] _IsCursor;
        private bool _ShiftLock;
        private bool _BeepScheduled;

        private void FlushDisplayBufferNow()
        {
            int co = _CurrentOffset < 0 ? 0 : _CurrentOffset;
            StringBuilder sb = new StringBuilder();
            int index = 0;

            for (int i = co; i < _DisplayBuffer.Length; i++)
            {
                sb.Append(Charset[_DisplayBuffer[i] & 0x7F]);
                _IsCursor[index++] = (_DisplayBuffer[i] & 0x80) != 0;
            }
            for (int i = 0; i < co; i++)
            {
                sb.Append(Charset[_DisplayBuffer[i] & 0x7F]);
                _IsCursor[index++] = (_DisplayBuffer[i] & 0x80) != 0;
            }
            _AnyCursorVisible = _IsCursor.Contains(true);
            _CurrentDispay = sb.ToString();
            if (_AnyCursorVisible)
            {
                sb.Clear();
                UpdateCursorVersion(sb);
                _CurrentCursorDisplay = sb.ToString();
            }
            else
                _CurrentCursorDisplay = _CurrentDispay;
            _HasChanged = true;
        }

        private void UpdateCursorVersion(StringBuilder sb)
        {
            for (int i = 0; i < _DisplayLength; i++)
            {
                if (_IsCursor[i])
                    sb.Append(InsertCursor ? '◀' : '█');
                else
                    sb.Append(_CurrentDispay[i]);
            }
        }

        internal void PutKeyPresses(string text, TimeSpan? delayForFirstChar = null, TimeSpan? delayBetweenChars = null)
        {
            TimeSpan delay = delayForFirstChar.GetValueOrDefault(DefaultKeyDelay);
            foreach (var c in text)
            {
                HP9825Key? code = null;
                bool shift = false;
                if (c >= '0' && c <= '9')
                {
                    code = (HP9825Key)(((int)c - (int)'0') + (int)HP9825Key.Text0);
                }
                else
                {
                    if (c >= 'A' && c <= 'Z')
                    {
                        code = (HP9825Key)(((int)c - (int)'A') + (int)HP9825Key.A);
                        shift = true;
                    }
                    else 
                    {
                        if (c >= 'a' && c <= 'z')
                        {
                            code = (HP9825Key)(((int)c - (int)'a') + (int)HP9825Key.A);
                        }
                        else
                        {
                            // TODO: lookup table?!
                            switch (c)
                            {
                                case '→':
                                    code= HP9825Key.GoesTo;
                                    break;
                                case '_':
                                    code = HP9825Key.EnterExponent;
                                    shift = true;
                                    break;
                                case '!':
                                    code = HP9825Key.Text1;
                                    shift = true;
                                    break;
                                case '"':
                                    code = HP9825Key.Text2;
                                    shift = true;
                                    break;
                                case '#':
                                    code = HP9825Key.Text3;
                                    shift = true;
                                    break;
                                case '$':
                                    code = HP9825Key.Text4;
                                    shift = true;
                                    break;
                                case '%':
                                    code = HP9825Key.Text5;
                                    shift = true;
                                    break;
                                case '&':
                                    code = HP9825Key.Text6;
                                    shift = true;
                                    break;
                                case '@':
                                    code = HP9825Key.Text7;
                                    shift = true;
                                    break;
                                case '[':
                                    code = HP9825Key.Text8;
                                    shift = true;
                                    break;
                                case ']':
                                    code = HP9825Key.Text9;
                                    shift = true;
                                    break;
                                case '\'':
                                    code = HP9825Key.Text0;
                                    shift = true;
                                    break;
                                case '|':
                                    code = HP9825Key.Pi;
                                    shift = true;
                                    break;
                                case ':':
                                    code = HP9825Key.QuestionMark;
                                    shift = true;
                                    break;
                                case '<':
                                    code = HP9825Key.Comma;
                                    shift = true;
                                    break;
                                case '>':
                                    code = HP9825Key.Period;
                                    shift = true;
                                    break;
                                case '^':
                                    code = HP9825Key.Power;
                                    break;
                                case '√':
                                case '\\':
                                    code = HP9825Key.Power;
                                    shift = true;
                                    break;
                                case ' ':
                                    code = HP9825Key.Space;
                                    break;
                                case '(':
                                    code = HP9825Key.OpenParenthesis;
                                    break;
                                case ')':
                                    code = HP9825Key.CloseParenthesis;
                                    break;
                                case '*':
                                    code = HP9825Key.Asterisk;
                                    break;
                                case '+':
                                    code = HP9825Key.Plus;
                                    break;
                                case ',':
                                    code = HP9825Key.Comma;
                                    break;
                                case '-':
                                    code = HP9825Key.Minus;
                                    break;
                                case '.':
                                    code = HP9825Key.Period;
                                    break;
                                case '/':
                                    code = HP9825Key.Slash;
                                    break;
                                case ';':
                                    code = HP9825Key.Semicolon;
                                    //=0x3C,
                                    break;
                                case '=':
                                    code = HP9825Key.Equals;
                                    //=0x3E,
                                    break;
                                case '?':
                                    code = HP9825Key.QuestionMark;
                                    break;
                                case 'π':
                                    code = HP9825Key.Pi;
                                    break;
                            }
                        }
                    }
                }
                if (code.HasValue)
                {
                    PutKeyPress(code.Value, shift, delay);
                    delay = delayBetweenChars.GetValueOrDefault(DefaultKeyDelay);
                }
                else
                    Debug.WriteLine("Uknown character for key string requested: {0}", c);
            }
        }
    }
}