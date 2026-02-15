using System;
using System.Runtime.InteropServices;

namespace CommandLineUtils.Visuals
{
    public class ConsoleScreen
        : Screen
    {
        private ConsoleColor _OrgFG;
        private ConsoleColor _OrgBG;

        public ConsoleScreen()
        {
            _OrgFG = Console.ForegroundColor;
            _OrgBG = Console.BackgroundColor;
        }

        private void Clear()
        {
            Console.ForegroundColor = _OrgFG;
            Console.BackgroundColor = _OrgBG;
            Console.Clear();    // TODO: save?
        }

        protected override void BufferToScreen(int x, int y, ConsoleColor foreground, ConsoleColor background, char[] buffer, int index, int count)
        {
            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background;
            Console.SetCursorPosition(x, y);
            Console.Write(buffer, index, count);
        }

        private void HandleProcessSignal(PosixSignalContext context)
        {
            switch(context.Signal)
            {
                case PosixSignal.SIGWINCH:
                    SizeChanged();
                    break;
            }
        }

        private PosixSignalRegistration? _ConsoleSizeChanged;

        protected override void Starting()
        {
            base.Starting();
            try
            {
                _ConsoleSizeChanged = PosixSignalRegistration.Create(PosixSignal.SIGWINCH, HandleProcessSignal);
            }
            catch(Exception ex)
            {
                throw;
                // TODO: better implementation for the "not supported" case... :(
            }
            Clear();
        }

        protected override void SizeChanged()
        {
            Console.Clear();    // TODO: improve...
            base.SizeChanged();
        }

        protected override void Stopping()
        {
            var x = _ConsoleSizeChanged;
            _ConsoleSizeChanged = null;
            x?.Dispose();
            Clear();
            base.Stopping();
        }

        protected override Size GetCurrentSize()
        {
            return new Size(Console.WindowWidth, Console.WindowHeight);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                _ConsoleSizeChanged?.Dispose();

                Console.ForegroundColor = _OrgFG;
                Console.BackgroundColor = _OrgBG;
            }
            base.Dispose(isDisposing);
        }
    }
}