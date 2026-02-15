using System;

namespace CommandLineUtils.Visuals
{
    public interface IVisualHost
    {
        Palette PaletteFor(Type type);
        
        void QueueMessage(string message, Visual? sender, object? parameter = null);

        public bool Monochrome { get; }
    }
}