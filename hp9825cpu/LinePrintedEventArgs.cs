using System;

namespace HP9825CPU
{
    /// <summary>
    /// Event data structure for a printed line (16 chars) of the internal thermal printer.
    /// </summary>
    public class LinePrintedEventArgs
        : EventArgs
    {
        internal LinePrintedEventArgs(string lastPrintedLine, TimeSpan simulationTime)
        {
            this.Text = lastPrintedLine;
            this.SimulationTime = simulationTime;
            this.RealTime = DateTime.UtcNow;
        }

        /// <summary>
        /// The printed text (up to 16 characters, as defined in the character set for the <see cref="KeyboardDisplayPrinterDevice"/>)
        /// </summary>
        public string Text { get; }
        /// <summary>
        /// The "up time" of the computer, when the line was printed.
        /// </summary>
        public TimeSpan SimulationTime { get; }
        /// <summary>
        /// The "actual" time when the line was printed. Depending on the simulation, this time is "real wall time" or "simulated wall time".
        /// </summary>
        public DateTime RealTime { get; }
    }
}