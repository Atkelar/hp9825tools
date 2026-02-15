using System;

namespace HP9825CPU
{
    public class TickedEventArgs
        : EventArgs
    {
        public TickedEventArgs(long prevTicks, long currentTicks)
        {
            PreviousTicks = prevTicks;
            CurrentTicks = currentTicks;
        }

        public long PreviousTicks { get; private set; }

        public long CurrentTicks { get; private set; }
    }
}