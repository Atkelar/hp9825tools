using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace CommandLineUtils
{
    /// <summary>
    /// Provides an abstract base class for handling advanced console style output.
    /// </summary>
    public abstract class OutputHandlerBase
        : IDisposable
    {

        /// <summary>
        /// Display specifier... used to support redirecting stderr and stdout independent of verbose output...
        /// </summary>
        public abstract class DisplaySpec
        {
            /// <summary>
            /// Implementations need to provide up-to-date number of charactes per line. If the result is "null", it means: unlimited width (i.e. stream)
            /// </summary>
            public abstract int? DisplayWidth { get; }

            public abstract void WriteLine(string ouptut);

            public virtual void Flush() {}

            internal int IndentLevel {get;set;}
            internal bool IsInTable {get;set;}
        }

        private DisplaySpec?[] Targets = new DisplaySpec[5];    // we need onve per verbosity...

        /// <summary>
        /// Gets the verbosity level that was requested for this run.
        /// </summary>
        public VerbosityLevel Verbosity {get; private set; } = VerbosityLevel.Quiet;    // start off quiet until we know better...

        /// <summary>
        /// Returns true if the requested level should be output.
        /// </summary>
        /// <param name="level">The level to check.</param>
        /// <returns>True to provide output, false to null-out.</returns>
        public bool IsReuested(VerbosityLevel level)
        {
            return Verbosity >= level && level >= VerbosityLevel.Errors;
        }

        internal void Prepare(VerbosityLevel runAt = VerbosityLevel.Normal)
        {
            Verbosity = runAt;
            for(int i = (int) VerbosityLevel.Errors; i<= (int) VerbosityLevel.Trace;i++)
            {
                // verbosity is from 0 for quiet to n...
                var tl = (VerbosityLevel)i;
                if (IsReuested(tl))
                    Targets[i] = CreateFor(tl);
                else   
                    Targets[i] = null;
            }
        }

        /// <summary>
        /// Derived classes need to provide a valid display specification object for the target output.
        /// </summary>
        /// <param name="level">The level to wrap;</param>
        /// <returns>The proper display specifier.</returns>
        protected abstract DisplaySpec CreateFor(VerbosityLevel level);

        /// <summary>
        /// Gets the display spec for a provided level.
        /// </summary>
        /// <param name="level">The level in question.</param>
        /// <returns>The display specifier.</returns>
        protected DisplaySpec? TargetFor(VerbosityLevel level)
        {
            if (level< VerbosityLevel.Errors || level > VerbosityLevel.Trace)
                return null;
            return Targets[(int)level];
        }

        /// <summary>
        /// Starts an indented text part; ended with the disposal of the object returned.
        /// </summary>
        /// <param name="level">The level to target;</param>
        /// <param name="numChars">The number of characters to be indented. Maxes out at half the available width!</param>
        /// <returns>A tracker object. Use in a "using" block to undo-indent.</returns>
        public IDisposable Indent(VerbosityLevel level, int numChars)
        {
            var t = TargetFor(level);
            if (t==null)
                return new DummyIndent();

            
            // hard limit to 128 chars indentation. Even in files that should be plenty.
            var max = t.DisplayWidth.GetValueOrDefault(128) / 2;
            if (t.IndentLevel+numChars > max)
            {
                numChars = max - t.IndentLevel;
            }
            if (numChars < 0)
                numChars=0;
            t.IndentLevel+=numChars;
            return new Indenter(this, level, numChars);
        }

        private class DummyIndent
            : IDisposable
        {
            public void Dispose()
            {
            }
        }

        private class Indenter
            : IDisposable
        {
            internal Indenter(OutputHandlerBase parent, VerbosityLevel level, int delta)
            {
                _parent = parent;
                _delta = delta;
                _level = level;
            }

            int _delta;
            OutputHandlerBase? _parent;
            VerbosityLevel _level;

            void IDisposable.Dispose()
            {
                if (_parent != null)
                {
                    _parent.TargetFor(_level)!.IndentLevel-=_delta;
                    _parent.EnsureNewLine(_level, false);   // make sure we end the indentation here...
                }
                _parent = null;
            }        
        }

        /// <summary>
        /// Make sure we are now at the beginning of a new line, add line break if not.
        /// </summary>
        /// <param name="level">The verbosity level to aim for.</param>
        /// <param name="forceEmptyLine">True to force an empty line, false to stay in the current line if we are already on the beginning</param>
        public void EnsureNewLine(VerbosityLevel level, bool forceEmptyLine = false)
        {
            if (!_LineBuffers.TryGetValue(level, out var line))
            {
                if (!forceEmptyLine)
                    return; // no buffer, MUST be start of line.
                _LineBuffers.Add(level, line = new StringBuilder());
            }
            var t = TargetFor(level);
            if (t == null)
                return;
            if (line.Length >0 || forceEmptyLine)
                t.WriteLine(line.ToString());
            line.Clear();
        }

        /// <summary>
        /// Starts an indented text part, based on a headline; ended with the disposal of the object returned.
        /// </summary>
        /// <param name="level">The level to go for in the output.</param>
        /// <param name="header">The label text for the output; will be put into the first(current) line for the indented part.</param>
        /// <param name="numChars">The minimum number of characters to be indented. Maxes out at half the available width!</param>
        /// <returns>A tracker object. Use in a "using" block to undo-indent.</returns>
        public IDisposable IndentFor(VerbosityLevel level, string header, int numChars = 0)
        {
            var n = Math.Max(header.Length, numChars);
            EnsureNewLine(level);
            Write(level, SplitMode.Any, header);
            return Indent(level, n);
        }

        /// <summary>
        /// Creates a table formatter for tablularized output. Columns that don't fit to the current width, will be truncated! 1 char separator between columns, tables will be indented too!
        /// </summary>
        /// <param name="level">The level to go for in the output.</param>
        /// <param name="creator">Callback to create the table; will only get called if the level is actually requested.</param>
        /// <returns>A table formatter to use to create tabular output!</returns>
        public TableFormatter Table(VerbosityLevel level, Action<ITableBuilder> creator)
        {
            throw new NotImplementedException();
        }

        public interface ITableBuilder
        {
            ITableBuilder Column(Action<ITableColumnBuilder> build);
        }

        public interface ITableColumnBuilder
        {
            ITableColumnBuilder Width(int width);
            ITableColumnBuilder Width(int min, int max);
            ITableColumnBuilder Align(HorizontalAlignment align, TextTrimming trim = TextTrimming.Beginning);
            ITableColumnBuilder Head(string header);
            ITableColumnBuilder Format(string formatString);
            ITableColumnBuilder FooterFrom(Func<object?> source);
        }

        internal struct TableColumn
        {
            public string? FormatString { get; set; }
            public int Width { get; set; }
            public HorizontalAlignment Align { get; set; }
            public TextTrimming Trimming {get; set;}
        }

        /// <summary>
        /// A table formatting helper.
        /// </summary>
        public class TableFormatter
            : IDisposable
        {
            internal TableFormatter(OutputHandlerBase parent, VerbosityLevel level, TableColumn[] columns)
            {
                _parent = parent;
                _Columns = columns;
                _Level = level;
            }

            OutputHandlerBase? _parent;
            private readonly TableColumn[] _Columns;
            private readonly VerbosityLevel _Level;

            void IDisposable.Dispose()
            {
                if (_parent != null)
                {
                    // close table...
                    //_parent =;
                }
                _parent = null;
            }        
        }

        private Dictionary<VerbosityLevel, StringBuilder> _LineBuffers = new Dictionary<VerbosityLevel, StringBuilder>();

        private static readonly char[] Whitspaces = {' ', '\t'};

        public void Write(VerbosityLevel level, SplitMode split, string text)
        {
            var spec = TargetFor(level);
            StringBuilder line;
            if (spec == null)
                return;
            lock(this)
            {
                if (!_LineBuffers.TryGetValue(level, out line))
                {
                    _LineBuffers.Add(level, line = new StringBuilder());
                }
            }
            bool isBOL = line.Length==0;
            if (spec.IndentLevel > line.Length)
            {
                // append spaces...
                line.Append(' ' , spec.IndentLevel - line.Length);
                isBOL = true;   // force here for indent handling.
            }
            SplitMode thisSplit = split;
            if (spec.DisplayWidth.HasValue)
            {
                // the bulk... we check if we have a line length limit. if so, ...
                var max = spec.DisplayWidth.Value;  // maximum...
                if (line.Length + text.Length > max)
                {
                    max -= line.Length; // got max. chars now...
                    // need to split...
                    // TODO: better implementation, but recursion should do the trick for common use cases...
                    if (thisSplit == SplitMode.None)
                    {
                        if(isBOL)
                            thisSplit = SplitMode.Word; // fallback.
                        else
                        {
                            EnsureNewLine(level);  
                            Write(level, split, text);
                            return;
                        }
                    }
                    
                    if (thisSplit == SplitMode.Word)
                    {
                        int idx = text.LastIndexOfAny(Whitspaces, max);
                        if (idx > 0)   // nope, no split here, even if it starts with WS...
                        {
                            //Console.WriteLine($"split (word)  {text} at {max}/{line.Length}");
                            line.Append(text.Substring(0, idx));
                            spec.WriteLine(line.ToString());
                            line.Clear();
                            Write(level, split, text.Substring(idx+1)); // strip whitespace...
                        }
                        else
                        {
                            // word didn't work, use "any"
                            line.Append(text.Substring(0, max));
                            spec.WriteLine(line.ToString());
                            line.Clear();
                            Write(level, split, text.Substring(max));
                        }
                    }
                    else
                    {
                        // split any...
                        line.Append(text.Substring(0, max));
                        spec.WriteLine(line.ToString());
                        line.Clear();
                        Write(level, split, text.Substring(max));
                    }
                }
                else
                    line.Append(text);
            }
            else
            {
                line.Append(text);
            }
        }
        public void WriteLine(VerbosityLevel level, SplitMode split, string? text)
        {
            Write(level, split, text ?? string.Empty);
            EnsureNewLine(level,  true);     // if we ended up in a newline by the length constraints, we can accept a blank line too...
        }

        private bool IsDisposed = false;

        /// <summary>
        /// Throws the <see cref="ObjectDisposedException"/> if the object is disposed, does nothing if not.
        /// </summary>
        protected void DemandNotDisposed()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
        }

        /// <summary>
        /// Override to handle disposable objects.
        /// </summary>
        /// <param name="isDisposing">True if the call came from the explicit dispose, false if it came from a destructor.</param>
        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)    // make sure we get the last bit of data out...
            {
                foreach(var t in _LineBuffers)
                {
                    if (t.Value.Length >0)
                    {
                        var spec = TargetFor(t.Key);
                        spec?.WriteLine(t.Value.ToString());
                    }
                }
                foreach(var t in Targets)
                {
                    t?.Flush();
                }
            }
        }
        /// <summary>
        /// Implements <see cref="IDisposable"/> 
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                Dispose(true);
            }
            IsDisposed = true;
        }
    }
}