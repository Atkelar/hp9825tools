using System;

namespace CommandLineUtils.Visuals
{
    public class ScreenDriver
        : IDisposable, IVisualHost
    {
        public Screen? Screen { get; internal set; }
        public Input? Input { get; internal set; }
        public bool Monochrome { get; set; }

        private PaletteHandler _Palette;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Screen?.Dispose();
                Input?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected internal virtual void Configure(Size minSize, VisualProcessParameters options, PaletteHandler paletteFrom)
        {
            if (Screen == null)
                Screen = new ConsoleScreen();
            if (Input == null)
                Input = new ConsoleInput();
            Monochrome = Monochrome || options.ForceMonochrome;
            _Palette = paletteFrom;
        }


        public Palette PaletteFor(Type type)
        {
            if (Monochrome)
                return PaletteHandler.MonoPalette;
            return _Palette.GetFor(type);
        }

        public void QueueMessage(string code, Visual? sender, object? parameter = null)
        {
            Input?.QueueMessage(code, sender, parameter);
        }
    }
}