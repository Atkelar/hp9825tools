using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommandLineUtils;
using CommandLineUtils.Visuals;

namespace HP9825Simulator
{
    public class PrinterOutput
        : Visual
    {
        private KeyboardDisplayPrinterDevice _Device;

        private List<PrinterLine> _Paper = new List<PrinterLine>();

        internal const string ExportToHtmlCommand="prt-out-html-now";

        public PrinterOutput(KeyboardDisplayPrinterDevice dev)
        {
            this._Device = dev;
            dev.PrintedLine += NewPrintedLine;
            this.Size = new Size(18,1);
        }

        private const string SnippedHere = "✂";
        public string SnipMarkerLine {get;set;} = "- ✂ -".PadCenter(16);

        protected override bool HandleEvent(EventData latestEvent)
        {
            if (latestEvent is MessageEventData md)
            {
                if (md.Code == ExportToHtmlCommand)
                {
                    string? file = md.Args as string;
                    if (file !=null)
                    {
                        ExportToHtml(file, "test", "test comment\n\nother paragraph", "Atkelar").Wait();
                    }
                    return true;
                }
            }
            return base.HandleEvent(latestEvent);
        }

        public class PrinterLine
        {
            internal PrinterLine(string text, TimeSpan simulated, DateTime when)
            {
                Text = text;
                SimulatedWhen= simulated;
                When = when;
            }
            public DateTime When {get;}
            public TimeSpan SimulatedWhen {get;}
            public string Text {get;}

        }

        private void NewPrintedLine(object? sender, LinePrintedEventArgs e)
        {
            _Paper.Add(new PrinterLine(e.Text, e.SimulationTime, e.RealTime));
            Invalidate();
        }

        private void SnipHere()
        {
            _Paper.Add(new PrinterLine(SnipMarkerLine, TimeSpan.Zero, DateTime.UtcNow));
        }

        public int Lines => _Paper.Count;

        public async Task ExportToHtml(string filename, string? headline, string? commentText, string? author = null, string? language = null, int startWithLine = 0, int? endWithLine = null)
        {
            int endLine = endWithLine.GetValueOrDefault(_Paper.Count-1);
            // if we are "empty", create an empty file...
            if (startWithLine < 0)
                throw new ArgumentOutOfRangeException(nameof(startWithLine), startWithLine, "Must be zero or larger!");
            if (endLine < startWithLine && _Paper.Count>0)
                throw new ArgumentOutOfRangeException(nameof(endWithLine), endLine, "Ending line needs to follow the first line!");

            language ??= System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            
            using(var f = System.IO.File.CreateText(filename))
            {
                await WriteHtmlToWriter(f, headline, commentText, author, language, startWithLine, endLine);
            }



        }

        // TODO: export to resource files for localization and easier maintaining.
        private static readonly string[] HtmlTemplateSections = {
            // document header section. Placeholders: 0 = title/topic., 1 = language, 2 = keywords, 3 = summary, 4 = creation date
            @"<!DOCTYPE html>
<html lang=""{1}"">
    <head>
        <title>HO9825 - {0}</title>
        <meta name=""keywords"" content=""{2}""/>
        <meta name=""language"" content=""{1}""/>
        <meta name=""robots"" content=""index,follow"" />
        <meta name=""summary"" content=""{3}""/>
        <meta name=""creator"" content=""Atkelar's HP9825A Simulator"" />
        <meta name=""created"" content=""{4:r}"" />
        <style>
            html
            {{
                background-color: white;
                color: #222222;
                font-family: Arial, Helvetica, sans-serif;
            }}
            hr
            {{
                border-style: dashed;
                color: darkblue;
            }}
            div.paper
            {{
                width: 16ch;
                margin: 1em;
                padding-left: 2em;
                padding-right: 2em;
                background-color: blanchedalmond;
                color: black;
                font-family: 'Courier New', Courier, monospace;
                text-wrap: nowrap;
                white-space: pre;
            }}
        </style>
    </head>
    <body>
",
// headline
@"        <h1>{0}</h1>",    
// summary text (0= html formatted paragraphs...)
@"{0}
<hr/>",
// "page" intro, 0= number of "page" starting with 1, 1 = number of pages, 2 = number of lines in page
@"      <h2>Page {0} of {1}</h2>
        <div class=""paper"">",
// line template number 0=text (0-16 chars), 1 = line number within page, 2 = line number in absolute format. 3 = date/time of the line in wall time, 4 = timespan in simulation time.
@"{0}",
// "page" outro - same placeholders as in page intro.
@"        </div>",
// footer area. Same placeholders as the header section.
@"    </body>
</html>",
// nothing to print - output.
@"<div class=""emptyprint"">No lines to export.</div>"
        };

        private async Task WriteHtmlToWriter(TextWriter target, string? headline, string? commentText, string? author, string language, int startWithLine, int endLine)
        {
            if (headline != null) headline = System.Web.HttpUtility.HtmlEncode(headline);
            if (commentText != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach(var s in commentText.Replace("\r","").Split("\n\n", StringSplitOptions.RemoveEmptyEntries |  StringSplitOptions.TrimEntries))
                {
                    sb.Append("<p>");
                    bool first = true;
                    foreach(var line in s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!first)
                            sb.Append("<br/>");
                        first=false;
                        sb.Append(System.Web.HttpUtility.HtmlEncode(line));
                    }
                    sb.Append("</p>");
                }
                commentText = sb.ToString();
            }
            if (author != null) author = System.Web.HttpUtility.HtmlEncode(author);
            // Sunday, July 18th, 2010, 5:15 pm
            await target.WriteLineAsync(string.Format(HtmlTemplateSections[0], 
                headline ?? string.Format("printout {0}", DateTime.Now), 
                language.ToLowerInvariant(), 
                "HP9825, Printout", 
                "summary",
                DateTimeOffset.Now));

            if(headline != null)
            {
                await target.WriteLineAsync(string.Format(HtmlTemplateSections[1], headline));
            }
            if (commentText != null)
            {
                await target.WriteLineAsync(string.Format(HtmlTemplateSections[2], commentText));
            }
            if (endLine < startWithLine)
            {
                // empty...
                await target.WriteLineAsync(HtmlTemplateSections[7]);
            }
            else
            {
                // split the pages at the "snip" line...
                int numLines = 0;
                List<List<PrinterLine>> pages = new  List<List<PrinterLine>>();
                List<PrinterLine> currentPage = new List<PrinterLine>();
                pages.Add(currentPage);
                for (int i = startWithLine; i <= endLine; i++)
                {
                    var l = _Paper[i];
                    if (l.Text==SnipMarkerLine)
                    {
                        // new page...
                        pages.Add(currentPage = new List<PrinterLine>());
                    }
                    else
                    {
                        numLines++;
                        currentPage.Add(l);
                    }
                }
                int lineAbsolute = 0;
                for(int page = 0; page < pages.Count; page++)
                {
                    var thisPage = pages[page];
                    await target.WriteLineAsync(string.Format(HtmlTemplateSections[3], page+1, pages.Count, thisPage.Count));
                    for(int line = 0; line < thisPage.Count; line++)
                    {
                        lineAbsolute++;
                        var thisLine= thisPage[line];
                        await target.WriteLineAsync(string.Format(HtmlTemplateSections[4], System.Web.HttpUtility.HtmlEncode(thisLine.Text.TrimEnd()), line+1, lineAbsolute, thisLine.When, thisLine.SimulatedWhen));
                    }
                    await target.WriteLineAsync(string.Format(HtmlTemplateSections[5], page+1, pages.Count, thisPage.Count));
                }
            }
            await target.WriteLineAsync(string.Format(HtmlTemplateSections[6], 
                headline ?? string.Format("printout {0}", DateTime.Now), 
                language.ToLowerInvariant(), 
                "HP9825, Printout", 
                "summary",
                DateTimeOffset.Now));
        }

        protected override void Paint(PaintContext p)
        {
            int index = _Paper.Count - 1;
            for(int i = Size.Height-1; i>=0; i--)
            {
                Location pos = new Location(0, i);
                if(index >=0)
                {
                    p.DrawChar(pos, ' ', 0);
                    // got paper...
                    var line = _Paper[index];
                    if (line.Text == SnippedHere)
                    {
                        p.DrawString(pos.Move(1,0), SnipMarkerLine, 1);
                    }
                    else
                    {
                        p.DrawString(pos.Move(1,0), line.Text.PadRight(16), 0);
                    }
                    p.DrawChar(pos.Move(17,0), ' ', 0);
                }
                else
                {
                    p.Repeat(pos, ' ', 18, 0);
                }
                index--;
            }
        }
    }
}