using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CommandLineUtils.Visuals
{
    public class PaletteHandler
    {
        internal static readonly Palette MonoPalette = new Palette("Monochrome", Array.Empty<ColorEntry>()) {  };

        internal Palette GetFor(Type type)
        {
            if (_Registration.TryGetValue(type, out var p))
                return p;
            return MonoPalette;
        }

        /// <summary>
        /// Builds a new group of colors.
        /// </summary>
        public interface IPaletteBuilder
        {
            /// <summary>
            /// Adds a new color to the list of supported colors.
            /// </summary>
            /// <param name="displayName">The display name of the palette entry for the color selection dialog.</param>
            /// <param name="foregroundColor">Foreground color to use.</param>
            /// <param name="backgroundColor">Background to use; if null, uses the most recently added background, or "Black" if none has been added before.</param>
            /// <returns>The same object for call chaining.</returns>
            IPaletteBuilder Color(string displayName, ConsoleColor foregroundColor, ConsoleColor? backgroundColor = null);
        }

        private class PaletteBuider
            : IPaletteBuilder
        {
            public PaletteBuider(Palette? basePal = null)
            {
                if (basePal != null)
                {
                    for (int i = 0;i<basePal.Count;i++)
                        _entries.Add(basePal[i]);
                }
            }

            private ConsoleColor LastBackGround = ConsoleColor.Black;

            public IPaletteBuilder Color(string name, ConsoleColor foregroundColor, ConsoleColor? backgroundColor = null)
            {
                if (backgroundColor != null)
                    LastBackGround = backgroundColor.Value;
                _entries.Add(new ColorEntry(name, foregroundColor, backgroundColor.GetValueOrDefault(LastBackGround)));
                return this;
            }

            List<ColorEntry> _entries = new List<ColorEntry>();

            internal Palette Build(string title)
            {
                return new Palette(title, _entries.ToArray());
            }
        }

        /// <summary>
        /// Register colors based on an existing color palette.
        /// </summary>
        /// <param name="typeDisplayName">The name to show for the group in the color picker dialog.</param>
        /// <typeparam name="T">The class to register.</typeparam>
        /// <typeparam name="TBase">The base class to use as a template.</typeparam>
        /// <param name="make">The builder activity.</param>
        public void Register<T, TBase>(string typeDisplayName, Action<IPaletteBuilder> make)
            where T : Visual
            where TBase : Visual
        {
            Type tBase = typeof(TBase);
            PaletteBuider b = new PaletteBuider(_Registration[tBase]);
            make(b);
            Type tReg = typeof(T);
            _Registration.Add(tReg, b.Build(typeDisplayName));
        }

        /// <summary>
        /// Register colors for a new visual.
        /// </summary>
        /// <param name="typeDisplayName">The name to show for the group in the color picker dialog.</param>
        /// <typeparam name="T">The class to register.</typeparam>
        /// <param name="make">The builder activity.</param>
        public void Register<T>(string typeDisplayName, Action<IPaletteBuilder> make)
            where T : Visual
        {
            PaletteBuider b = new PaletteBuider();
            make(b);
            Type tReg = typeof(T);
            _Registration.Add(tReg, b.Build(typeDisplayName));
        }

        private Dictionary<Type, Palette> _Registration = new Dictionary<Type, Palette>();

        /// <summary>
        /// Register a single-color-visual.
        /// </summary>
        /// <typeparam name="T">The visual type</typeparam>
        /// <param name="typeDisplayName">The name to show for the group in the color picker dialog.</param>
        /// <param name="displayName">The name to show in the color picker dialog.</param>
        /// <param name="foreground">The foreground color.</param>
        /// <param name="background">The background color.</param>
        /// <exception cref="NotImplementedException"></exception>
        public void Register<T>(string typeDisplayName, string displayName, ConsoleColor foreground, ConsoleColor background)
            where T : Visual
        {
            Type tReg = typeof(T);
            _Registration.Add(tReg, new Palette(typeDisplayName, new ColorEntry[] { new ColorEntry(displayName, foreground, background) }));
        }
    }
}