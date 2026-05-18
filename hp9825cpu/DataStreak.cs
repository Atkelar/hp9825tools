using System;
using System.Collections.Generic;

namespace HP9825CPU
{
    // A single run ("streak") of tape data, following a specific "gap" without data.
    internal class DataStreak
    {
        public DataStreak(double start, double end, double gap)
        {
            Gap = gap;
            Start = start;
            End = end;
            if (end < start)
                throw new ArgumentOutOfRangeException(nameof(end), end, "End doesn't add up. End < start!");
            if (gap < 0)
                throw new ArgumentOutOfRangeException(nameof(gap), gap, "Gap is negative!");
            if (start + gap > end)
                throw new ArgumentOutOfRangeException(nameof(end), end, "End doesn't add up. Start + gap > end!");
        }
        public List<ushort> Data { get; internal set; } = new List<ushort>();
        public double Gap { get; private set; }
        public double Start { get; private set; }
        public double End { get; set; }

        public override string ToString()
        {
            return string.Format("{0:0.0000} [{1:0.00000}] -> {2:0.0000} ({3}wd)", Start, Gap, End, Data.Count);
        }
    }
}