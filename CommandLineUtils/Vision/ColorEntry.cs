using System;
using System.Drawing;

namespace CommandLineUtils.Visuals
{
    public class ColorEntry
    {
        internal ColorEntry(string title, ConsoleColor foreground, ConsoleColor background)
        {
            Foreground = foreground;
            Background = background;
            Title = title;
        }
        internal ColorEntry(ColorEntry source)
        {
            Title = source.Title;
            Foreground = source.Foreground;
            Background = source.Background;
        }
        public string Title { get; private set; }
        public ConsoleColor Foreground { get; set; }
        public ConsoleColor Background { get;  set; }
    }
}