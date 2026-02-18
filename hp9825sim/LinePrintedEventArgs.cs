using System;

namespace HP9825Simulator
{
    public class LinePrintedEventArgs
        : EventArgs
    {
        public LinePrintedEventArgs(string lastPrintedLine, TimeSpan simulationTime)
        {
            this.Text = lastPrintedLine;
            this.SimulationTime = simulationTime;
            this.RealTime = DateTime.UtcNow;
        }

        public string Text { get; }
        public TimeSpan SimulationTime { get; }
        public DateTime RealTime { get; }
    }
}