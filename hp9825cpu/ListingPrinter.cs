using System;
using System.IO;

namespace HP9825CPU
{
    public class ListingPrinter
    {
        /// <summary>
        /// Create a new listing printer implementation that handles paging and formatting.
        /// </summary>
        /// <param name="width">The page width.</param>
        /// <param name="pageHeight">The number of lines on the page.</param>
        /// <param name="opts">The formatting options.</param>
        /// <param name="use16Bit">True to use 16-bit address formatting (one more digit in octal).</param>
        public ListingPrinter(int width = 80, int pageHeight = 60, CodeFormatOptions? opts = null, bool use16Bit = false, System.IO.TextWriter? target = null)
        {
            Width = width;
            PageHeight = pageHeight;
            Format = opts ?? new CodeFormatOptions();
            _Output = target ?? new StringWriter();
            _Is16Bit = use16Bit;
        }

        private readonly bool _Is16Bit;
        private TextWriter _Output;

        int _CurrentLine = -1;  // current line index; if 0 we are pre-page (inter-page gap), if -1 we are pre-output.
        int _CurrentPage = 0;   // current page number.
        int _LineCounter = 0;   // source line counter.

        int _SubLineCount = 0;  // sub-section line counter.

        public string Headline { get; private set; }

        /// <summary>
        /// Updates the headline 
        /// </summary>
        /// <param name="newHeadline"></param>
        public void SetHeadline(string? newHeadline)
        {
            if (newHeadline != Headline)
            {
                Headline = newHeadline;
                if (Format.PageHeader != PageHeaderFormat.None)
                {
                    if (Format.NewPageOnHeadlineChange)
                    {
                        NewPageNow();
                    }
                    else
                    {
                        if ((Format.PageHeader & PageHeaderFormat.HeadlineInPage) != PageHeaderFormat.None && !SuppressOutput)
                        {
                            // got to print the new header inline on page. 
                            // but only if we have enough space for at least one more line after it.
                            if (_CurrentLine + 2 * Format.HeaderSpacing + 2 <= PageHeight)
                            {
                                string pt = Headline ?? string.Empty;
                                if (pt.Length > Width - 2)
                                    pt = pt.Substring(0, Width - 2);
                                int pad = (Width - pt.Length) / 2;
                                string headline = string.Concat(new string(' ', pad), pt);
                                if (!string.IsNullOrWhiteSpace(headline))
                                {
                                    if (Format.FormatUpperCase)
                                        headline = headline.ToUpperInvariant();
                                    for (int i = 0; i < Format.HeaderSpacing; i++)
                                    {
                                        _Output.WriteLine();
                                    }
                                    _Output.WriteLine(headline);
                                    for (int i = 0; i < Format.HeaderSpacing; i++)
                                    {
                                        _Output.WriteLine();
                                    }
                                    _CurrentLine += 2 * Format.HeaderSpacing + 1;
                                }
                            }
                            else
                            {
                                // out of space here, just do a page flip.
                                NewPageNow();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Ends the current page, resets page counters but doesn't print anything yet...
        /// </summary>
        public void NewPageNow()
        {
            if (_CurrentLine != 0)  // avoid empty pages for multi-calls
            {
                _CurrentLine = 0;   // intra-page
                _CurrentPage++;
            }
        }

        /// <summary>
        /// Writes a page break if needed and then prints the page header, according to the current formatting options.
        /// </summary>
        private void PrintPageIntro()
        {
            if (_CurrentPage > 1)
                _Output.Write('\u000C');
            _CurrentLine = 1;
            if (Format.PageHeader != PageHeaderFormat.None)
            {
                // format: left/center/right part...
                string fn = ((Format.PageHeader & PageHeaderFormat.Filename) != PageHeaderFormat.None ? Filename : null) ?? string.Empty;
                string pg = (Format.PageHeader & PageHeaderFormat.PageNumber) != PageHeaderFormat.None ? string.Format(Format.PageNumberFormat, _CurrentPage) : string.Empty;
                string pt = Headline ?? string.Empty;
                int hLen = Width - fn.Length - pg.Length - 2;

                if (pt.Length > hLen)
                    pt = pt.Substring(0, hLen);
                int pad = (hLen - pt.Length) / 2;
                string headline = string.Concat(fn, " ", new string(' ', pad), pt, new string(' ', hLen - (pad + pt.Length)), " ", pg);
                if (!string.IsNullOrWhiteSpace(headline))   // just in case we end up with nothing anyhow...
                {
                    if (Format.FormatUpperCase)
                        headline = headline.ToUpperInvariant();
                    _Output.WriteLine(headline);
                    _CurrentLine++;
                    for (int i = 0; i < Format.HeaderSpacing; i++)
                    {
                        _Output.WriteLine();
                        _CurrentLine++;
                    }
                }
            }
        }

        public string? Filename { get; set; }

        /// <summary>
        /// Makes sure we have a current page and there is room for a line here.
        /// </summary>
        private void PrepareForNewLine()
        {
            if (_CurrentLine < 0 || _CurrentLine >= PageHeight) // outside of page...
                NewPageNow();
            if (_CurrentLine == 0)  // between pages...
                PrintPageIntro();
            else
                _CurrentLine++; // gotcha.
        }

        private int WriteLineCounter()
        {
            if (Format.IncludeLineNumbers)
            {
                // line counter is decimal
                // NOTE: the patent listing has three zeros attached to the line counter... no clue yet what those are. Sub-Line count for "REP" or "ASC" output maybe?
                _Output.Write("{0:00000}{1:000} ", _LineCounter, _SubLineCount);
                return 9;
            }
            return 0;
        }

        private int WriteAddress(int? address)
        {
            if (Format.IncludeAddress)
            {
                var w = Format.WordWidth(!_Is16Bit);
                if (address.HasValue)
                    _Output.Write(Format.FormatWord(address.Value, !_Is16Bit));
                else
                    _Output.Write(SpaceBuffer, 0, w); // address
                _Output.Write(' ');
                return w + 1;
            }
            return 0;
        }

        private int WriteValue(int? value)
        {
            if (Format.IncludeValues)
            {
                int w = Format.WordWidth(false);
                if (value.HasValue)
                    _Output.Write(Format.FormatWord(value.Value, false));
                else
                    _Output.Write(SpaceBuffer, 0, w);     // value
                _Output.Write("  ");
                return w + 1;
            }
            return 0;
        }

        /// <summary>
        /// Prints a comment line, including line number, spacing and the leading "*"
        /// </summary>
        /// <param name="comment">The comment to print.</param>
        public void PrintCommentLine(string? comment)
        {
            _LineCounter++;
            _SubLineCount = 0;

            if (SuppressOutput || !Format.IncludeComments) // we don't want...
                return;
            PrepareForNewLine();

            int loc = 0;
            loc += WriteLineCounter();
            loc += WriteAddress(null);
            loc += WriteValue(null);

            _Output.Write("*");
            loc += 1;
            if (string.IsNullOrWhiteSpace(comment))
                _Output.WriteLine();
            else
            {
                if (loc + comment.Length > Width)
                {
                    comment = comment.Substring(0, Width - loc);
                }
                _Output.WriteLine(Format.FormatUpperCase ? comment.ToUpperInvariant() : comment);
            }
        }

        public void PrintSourceSubLine(int? address, int? value)
        {
            _SubLineCount++;
            if (SuppressOutput || SuppressExtraLines || Format.SuppressExtraLines) // we don't want...
                return;
            PrepareForNewLine();

            WriteLineCounter();
            WriteAddress(address);
            WriteValue(value);
            _Output.WriteLine("*"); // pretend it's a comment line just to make the source compatible.
        }

        public void PrintEmptyLines(int count)
        {
            if (SuppressOutput) // we don't want...
                return;
            for (int i = 0; i < count; i++)
            {
                PrepareForNewLine();
                _Output.WriteLine();
            }
        }

        public void PrintSourceLine(int? address, int? value, string? label, string mnemonic, string? operands, string? comment, bool isNonPrinted, bool isFromMacro)
        {
            if (isFromMacro)
                _SubLineCount++;
            else
            {
                _LineCounter++;
                _SubLineCount = 0;
            }

            if (SuppressOutput) // we don't want...
                    return;
            if (isNonPrinted && !Format.IncludeNonPrintedLines)
                return;
            PrepareForNewLine();

            int loc = WriteLineCounter();
            loc += WriteAddress(address);
            loc += WriteValue(value);

            int spc;
            if (label != null)
            {
                _Output.Write(label);
                spc = Math.Max(1, 6 - label.Length);
                _Output.Write(SpaceBuffer, 0, spc);     // Label alignment
            }
            else
                _Output.Write(SpaceBuffer, 0, 6);     // Label alignment

            loc += 6;
            _Output.Write(mnemonic);
            loc += mnemonic.Length;

            if (operands != null)
            {
                _Output.Write(' ');
                _Output.Write(operands);
                spc = Math.Max(1, 16 - operands.Length);
                loc += 1 + operands.Length + spc;
            }
            else
            {
                spc = 16;
                loc += 16;
            }
            if (!string.IsNullOrWhiteSpace(comment))
            {
                _Output.Write(SpaceBuffer, 0, spc);     // Label alignment
                if (loc + comment.Length > Width)
                {
                    if (loc <= Width)
                        comment = comment.Substring(0, Width - loc);
                    else
                        comment = "";
                }
                _Output.WriteLine(Format.FormatUpperCase ? comment.ToUpperInvariant() : comment);
            }
            else
                _Output.WriteLine();
        }

        char[] SpaceBuffer = new string(' ', 1024).ToCharArray();   // pre-compute a space array to optimize the output of whitespaces.

        /// <summary>
        /// Adds a gap in the source line counter. Used to facilitate non-printed but counted lines.
        /// </summary>
        /// <param name="delta">Number to increment by.</param>
        public void IncrementSourceLineCounter(int delta = 1)
        {
            _LineCounter += delta;
        }

        public bool SuppressOutput { get; set; }
        public bool SuppressExtraLines { get; set; }

        public int Width { get; private set; }
        public int PageHeight { get; private set; }

        public CodeFormatOptions Format { get; private set; }

        public override string ToString()
        {
            return _Output.ToString();
        }
    }
}