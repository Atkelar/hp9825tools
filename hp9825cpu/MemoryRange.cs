using System;

namespace HP9825CPU
{
    public class MemoryRange
    {
        public MemoryRange(int start, int end)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(start, 0, nameof(start));
            ArgumentOutOfRangeException.ThrowIfLessThan(end, start, nameof(start));
            End = end;
            Start = start;
        }

        public bool Overlaps(MemoryRange other)
        {
            return !(Start > other.End || End < other.End);
        }
        public int Start { get; private set; }
        public int End { get; private set; }
    }
}