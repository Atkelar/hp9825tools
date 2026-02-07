using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

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

            /// <summary>
            /// Implementors need to write the provided string to the output media and append a newline character.
            /// </summary>
            /// <param name="ouptut">The text to write.</param>
            public abstract void WriteLine(string ouptut);

            /// <summary>
            /// Flushes the output buffers; makes sure all text written so far lands on whatever media it is going to.
            /// </summary>
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
            var spec = TargetFor(level);
            if(spec == null)
                return new TableFormatter(null, level, Array.Empty<TableColumn>(),false,false,false);
            var b = new TableBuilder(spec.DisplayWidth, level);
            creator(b);
            return b.Make(this);
        }

        private class TableBuilder
            : ITableBuilder
        {
            public TableBuilder(int? width, VerbosityLevel level)
            {
                _Width = width;
                _Level = level;
            }

            public TableFormatter Make(OutputHandlerBase parent)
            {
                // validate columns...
                if (_Columns.Count==0)
                    throw new InvalidOperationException("No columns in table!");
                
                int minWidth = _Columns.Sum(x=>x.Width ?? x.MinWidth ?? throw new InvalidOleVariantTypeException("Column is dynamic but missing min length"));
                int maxWidth = _Columns.Sum(x=>x.Width ?? x.MaxWidth ?? throw new InvalidOleVariantTypeException("Column is dynamic but missing max length"));
                minWidth+=_Columns.Count-1;
                maxWidth+=_Columns.Count-1;

                if (_Width.HasValue && minWidth > _Width) // found width, but too wide!
                {
                    // Truncate to minWidth...
                    foreach(var item in _Columns)
                    {
                        if (!item.Width.HasValue)
                            item.Width = item.MinWidth;
                    }
                    int width = 0;
                    for(int i=0;i<_Columns.Count;i++)
                    {
                        int cWidth = (i==0 ? 0 : 1) + _Columns[i].Width!.Value;
                        if (width + cWidth > maxWidth)
                        {
                            if (width+3 > maxWidth)
                            {
                                // we don't even have space for any part past the separapor, trim and exit.
                                _Columns.RemoveRange(i, _Columns.Count - i);
                                break;
                            }
                            else
                            {
                                // truncate column...
                                _Columns[i].Width = (_Width.Value - (width + 2));
                                _Columns[i].ShowTruncate=true;
                                // trim rest...
                                if (i+1 < _Columns.Count)
                                    _Columns.RemoveRange(i+1, _Columns.Count - (i + 1));
                                break;
                            }
                        }
                        else
                            width+=cWidth;
                    }
                }
                else
                {
                    if (minWidth != maxWidth)
                    {
                        // we need to stretch something...
                        if (_Width.HasValue &&  maxWidth>_Width.Value )
                            maxWidth = _Width.Value;
                        // we have between min and max characters to add to all the dynamic columns. Proportionally stretch them, based on avg. possible width.

                        double total = maxWidth-minWidth;   // got total columns to distribute.

                        var avgWidths = _Columns.Select(x=>x.Width.HasValue ? 0d : ((double)x.MinWidth!.Value + (double)x.MaxWidth!.Value) / 2d).ToArray();
                        var sum =avgWidths.Sum(x=>x);
                        for(int i=0;i<avgWidths.Length;i++)
                        {
                            avgWidths[i]/=sum;
                        }
                        int? lastColumn = null;
                        for(int i=0;i<avgWidths.Length;i++)
                        {
                            if(_Columns[i].Width.HasValue)
                                continue;
                            if (avgWidths[i]== 0)
                                _Columns[i].Width = _Columns[i].MinWidth;
                            else
                            {
                                double give = total * avgWidths[i];
                                int given = (int)Math.Floor(give);
                                _Columns[i].Width=_Columns[i].MinWidth!.Value + given;
                                total -= give;
                                lastColumn = i;
                            }
                        }
                        if (total > 1 && lastColumn.HasValue)   // account for roundings...
                            _Columns[lastColumn.Value].Width += (int)Math.Floor(total);
                    }
                }

                parent.EnsureNewLine(_Level);
                
                return new TableFormatter(parent, _Level, _Columns.ToArray(), Ticks,SepHead, SepFoot)
                {
                    RowCounter = RowCount
                };
            }

            private List<TableColumn> _Columns = new List<TableColumn>();
            private int? _Width;
            private string? RowCount;
            private bool SepHead, SepFoot, Ticks;
            private VerbosityLevel _Level;
            public ITableBuilder Column(int width,  Action<ITableColumnBuilder> build)
            {
                var b = new TableColumnBuilder(width);
                build(b);
                _Columns.Add(b.Make());
                return this;
            }

            public ITableBuilder Column(int min, int max,  Action<ITableColumnBuilder> build)
            {
                var b = new TableColumnBuilder(min, max);
                build(b);
                _Columns.Add(b.Make());
                return this;
            }

            public ITableBuilder Separators(bool headerSeparator, bool footerSeparator, bool useTicks = false)
            {
                SepFoot = footerSeparator;
                SepHead = headerSeparator;
                Ticks = useTicks;
                return this;
            }

            public ITableBuilder RowCountTemplate(string template)
            {
                RowCount = template;
                return this;    
            }
        }

        private class TableColumnBuilder
            : ITableColumnBuilder
        {
            public TableColumnBuilder(int w)
            {
                col.Width=w;
            }
            public TableColumnBuilder(int min,int max)
            {
                col.MinWidth=min;
                col.MaxWidth=max;
            }
            private TableColumn col = new TableColumn()
            {
                Align = HorizontalAlignment.Left,
                Trimming = TextTrimming.Beginning
            };
            public ITableColumnBuilder Align(HorizontalAlignment align, TextTrimming trim = TextTrimming.Beginning)
            {
                col.Align = align;
                col.Trimming = trim;
                return this;
            }

            public ITableColumnBuilder FooterFrom(Func<object?> source)
            {
                col.FooterCallback = source;
                return this;
            }

            public ITableColumnBuilder Format(string formatString)
            {
                col.FormatString = formatString;
                return this;
            }

            public ITableColumnBuilder Head(string header)
            {
                col.Header = header;
                return this;
            }


            internal TableColumn Make()
            {
                return col;
            }
        }

        /// <summary>
        /// Creates a new table definitin.
        /// </summary>
        public interface ITableBuilder
        {
            /// <summary>
            /// Adds a column to a table.
            /// </summary>
            /// <param name="width">The width in characters.</param>
            /// <param name="build">The column defnition.</param>
            /// <returns>The builder for chained calls.</returns>
            ITableBuilder Column(int width, Action<ITableColumnBuilder> build);
            /// <summary>
            /// Adds a column to a table.
            /// </summary>
            /// <param name="min">Minimum width in chacacters.</param>
            /// <param name="max">Maximum width in characters.</param>
            /// <param name="build">The column defnition.</param>
            /// <returns>The builder for chained calls.</returns>
            ITableBuilder Column(int min, int max, Action<ITableColumnBuilder> build);
            /// <summary>
            /// Enable header or footer separator for the table.
            /// </summary>
            /// <param name="headerSeparator">True to print a dashed line between the headline and the body.</param>
            /// <param name="footerSeparator">True to print a dashed line between the body and footer of the table.</param>
            /// <param name="useTicks">True to add + marks between columns.</param>
            /// <returns>The builder for chained calls.</returns>
            ITableBuilder Separators(bool headerSeparator, bool footerSeparator, bool useTicks = false);

            /// <summary>
            /// Sets a template string (with placeholder {0}) for a "summary row" to show number of rows in a line after the table.
            /// </summary>
            /// <param name="template">The template string.</param>
            /// <returns>The builder for chained calls.</returns>
            ITableBuilder RowCountTemplate(string template);

        }

        /// <summary>
        /// Creates a single table column.
        /// </summary>
        public interface ITableColumnBuilder
        {
            /// <summary>
            /// Sets the alignment and trimming mode for the column. Default would be left aligned, end trimming.
            /// </summary>
            /// <param name="align">The alignment.</param>
            /// <param name="trim">The trimming mode.</param>
            /// <returns>The builder for chaining calls.</returns>
            ITableColumnBuilder Align(HorizontalAlignment align, TextTrimming trim = TextTrimming.Beginning);
            /// <summary>
            /// Sets an optional header string.
            /// </summary>
            /// <param name="header">The header text.</param>
            /// <returns>The builder for chaining calls.</returns>
            ITableColumnBuilder Head(string header);
            /// <summary>
            /// Sets a format string for formatting <see cref="IFormattable"/>  based objects in the column.
            /// </summary>
            /// <param name="formatString">The format string.</param>
            /// <returns>The builder for chaining calls.</returns>
            ITableColumnBuilder Format(string formatString);
            /// <summary>
            /// Sets a callback to fetch a footer value for the column.
            /// </summary>
            /// <param name="source">A function that will be called when the table is done. Any value returned here will be added as a footer line.</param>
            /// <returns>The builder for chaining calls.</returns>
            ITableColumnBuilder FooterFrom(Func<object?> source);
        }

        internal class TableColumn
        {
            public string? FormatString { get; set; }
            public int? Width { get; set; }
            public int? MinWidth {get;set;}
            public int? MaxWidth {get;set;}
            public HorizontalAlignment Align { get; set; }
            public TextTrimming Trimming {get; set;}
            public Func<object?>? FooterCallback { get; set; }
            public string? Header {get;set;}
            public bool ShowTruncate { get; internal set; }
        }

        /// <summary>
        /// A table formatting helper.
        /// </summary>
        public class TableFormatter
            : IDisposable
        {
            internal TableFormatter(OutputHandlerBase? parent, VerbosityLevel level, TableColumn[] columns, bool useTicks, bool headSep, bool footSep)
            {
                _parent = parent;
                _Columns = columns;
                _Level = level;
                HeaderSeparator = headSep;
                FooterSeparator = footSep;
                SeparatorTicks = useTicks;

                var t = _parent?.TargetFor(level);
                if(t == null || columns.Length==0)
                    _parent = null; // we just default to null-out
                else
                {
                    if (t.IsInTable)
                        throw new InvalidOperationException("Nested tables are not supported!");
                    t.IsInTable = true;
                    if (_Columns.Any(x=>x.Header != null))
                    {
                        Line(t, _Columns.Select(x=>x.Header).ToArray());
                        RowCount=0; // start over.
                        if(headSep)
                            PrintSeparatorRow(t);
                    }
                }
            }

            internal bool HeaderSeparator {get;set;}
            internal bool FooterSeparator {get;set;}
            internal bool SeparatorTicks {get;set;}
            internal string? RowCounter {get;set;}
           
            int RowCount = 0;

            OutputHandlerBase? _parent;
            private readonly TableColumn[] _Columns;
            private readonly VerbosityLevel _Level;

            void IDisposable.Dispose()
            {
                if (_parent != null)
                {
                    // close table...
                    //_parent =;
                    var t = _parent.TargetFor(_Level);
                    if (t!=null)
                    {
                        // visually close table...
                        int count = RowCount;
                        if (_Columns.Any(x=>x.FooterCallback != null))
                        {
                            // we need a footer...
                            if (this.FooterSeparator)
                            {
                                PrintSeparatorRow(t);
                            }
                            Line(t, _Columns.Select(x=>x.FooterCallback?.Invoke()).ToArray());
                        }
                        if (RowCounter != null)
                        {
                            t.WriteLine(string.Format(RowCounter, count));
                        }
                        t.IsInTable = false;
                    }
                }
                _parent = null;
            }



            private void PrintSeparatorRow(DisplaySpec t)
            {
                StringBuilder sb= new StringBuilder();
                foreach(var c in _Columns)
                {
                    if(sb.Length>0)
                        sb.Append(SeparatorTicks ? '+':'-');
                    sb.Append('-', c.Width!.Value);
                }
                t.WriteLine(sb.ToString());
            }

            private void Line(DisplaySpec target, params object?[] columns)
            {
                StringBuilder sb= new StringBuilder();
                bool lastWasTruncated = false;
                int i = 0;
                object?[]? overflow=null;
                foreach(var c in _Columns)
                {
                    if(sb.Length>0)
                        sb.Append(lastWasTruncated ? '…' : ' ');
                    string vText;
                    if (i< columns.Length && columns[i] != null)
                    {
                        if (columns[i] is IFormattable fmt && c.FormatString != null)
                        {
                            vText = fmt.ToString(c.FormatString, null);
                        }
                        else
                        {
                            vText = columns[i]?.ToString() ?? string.Empty;
                        }
                    }
                    else    
                        vText = string.Empty;
                    if (vText.Length > c.Width!.Value)
                    {
                        int len = c.Width!.Value;
                        // handle trimming...
                        switch(c.Trimming)
                        {
                            case TextTrimming.End:
                                vText = vText.Substring(0, len);
                                lastWasTruncated = true;
                                break;
                            case TextTrimming.Beginning:
                                vText = vText.Substring(vText.Length - len);
                                lastWasTruncated = true;
                                break;
                            case TextTrimming.Both:
                                int beg = len / 2;
                                len = len - beg;
                                vText = vText.Substring(beg, len);
                                lastWasTruncated = true;
                                break;
                            case TextTrimming.NoTrim:
                                // ugh... overlflow.
                                overflow ??= new object?[columns.Length];
                                overflow[i] = vText.Substring(len);
                                vText = vText.Substring(0, len);
                                break;
                        }
                    }
                    if (vText.Length < c.Width.Value)
                    {
                        int len = c.Width!.Value;
                        // handle padding.
                        switch(c.Align)
                        {
                            case HorizontalAlignment.Left:
                                sb.Append(vText);
                                sb.Append(' ', len - vText.Length);
                                break;
                            case HorizontalAlignment.Right:
                                sb.Append(' ', len - vText.Length);
                                sb.Append(vText);
                                break;
                            case HorizontalAlignment.Center:
                                int beg = (len - vText.Length) / 2;
                                len -= beg;
                                sb.Append(' ', beg);
                                sb.Append(vText);
                                sb.Append(' ', len - vText.Length);
                                break;
                        }
                    }
                    else    // exact length.
                        sb.Append(vText);
                    i++;
                }

                if (_Columns[_Columns.Length-1].ShowTruncate && lastWasTruncated)  
                {
                    sb.Append('…');
                }

                target.WriteLine(sb.ToString());
            }

            /// <summary>
            /// Print a single line in a table.
            /// </summary>
            /// <param name="columns">The valus for the columns.</param>
            public void Line(params object?[] columns)
            {
                RowCount++;
                if(_parent!=null && _Columns.Length>0)
                { 
                    var t = _parent.TargetFor(_Level);
                    if (t != null)
                        Line(t, columns);
                }
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