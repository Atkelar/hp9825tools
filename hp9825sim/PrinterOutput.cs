using System;
using System.Collections.Generic;
using CommandLineUtils.Visuals;

namespace HP9825Simulator
{
    public class PrinterOutput
        : Visual
    {
        private KeyboardDisplayPrinterDevice _Device;

        private List<string> _Paper = new List<string>();

        public PrinterOutput(KeyboardDisplayPrinterDevice dev)
        {
            this._Device = dev;
            dev.PrintedLine += NewPrintedLine;
            this.Size = new Size(18,1);
        }

        private const string SnippedHere = "✂";
        public string SnipMarkerLine {get;set;} = "- ✂ -".PadCenter(16);

        private void NewPrintedLine(object? sender, LinePrintedEventArgs e)
        {
            _Paper.Add(e.Text);
            Invalidate();
        }

        private void SnipHere()
        {
            _Paper.Add(SnippedHere);
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
                    if (line == SnippedHere)
                    {
                        p.DrawString(pos.Move(1,0), SnipMarkerLine, 1);
                    }
                    else
                    {
                        p.DrawString(pos.Move(1,0), line.PadRight(16), 0);
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