using System;
using CommandLineUtils.Visuals;

namespace HP9825Simulator
{
    public class DisplayOutput
        : Visual
    {
        private KeyboardDisplayPrinterDevice _HookedTo;

        public DisplayOutput(KeyboardDisplayPrinterDevice dev)
        {
            _HookedTo = dev;
            dev.Beep += BeepNow;
            dev.DisplayChanged += DisplayUpdated;
            Size = new Size(36, 1);
        }

        private void DisplayUpdated(object? sender, EventArgs e)
        {
            if (object.ReferenceEquals(_HookedTo, sender))
            {
                Invalidate();
            }
        }

        private void BeepNow(object? sender, EventArgs e)
        {
            if (object.ReferenceEquals(_HookedTo, sender))
            {
                QueueMessage(MessageCodes.Beep);
            }
        }

        protected override void Paint(PaintContext p)
        {
            var pos = new Location(0,0);
            p.DrawChar(pos, ' ', 0);
            p.DrawChar(pos.Move(1,0), _HookedTo.RunLight ? '◉' : '○', 1);
            p.DrawChar(pos.Move(2,0), ' ', 0);
            p.DrawString(pos.Move(3,0), _HookedTo.Display, 0);
            p.DrawChar(pos.Move(35,0), ' ', 0);
        }
    }
}