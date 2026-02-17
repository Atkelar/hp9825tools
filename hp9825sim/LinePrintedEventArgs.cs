using System;

namespace HP9825Simulator
{
    public class LinePrintedEventArgs
        : EventArgs
    {
        public LinePrintedEventArgs(string lastPrintedLine)
        {
            this.Text = lastPrintedLine;
        }

        public string Text { get; private set; }
    }
}