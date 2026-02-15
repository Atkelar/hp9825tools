using System;
using System.Collections.Generic;

namespace CommandLineUtils.Visuals
{
    public class Palette
    {
        private ColorEntry[] _Entries;
        private byte[] _RawColors;

        public Palette(string displayName, ColorEntry[] colorEntries)
        {
            this._Entries = colorEntries;
            this._RawColors = DeriveColors();
            this.Title = displayName;
        }

        public string Title { get; private set; }

        public void Update()
        {
            _RawColors = DeriveColors();
        }

        public void Revert()
        {
            ConsoleColor f,b;
            for(int i=0;i<_RawColors.Length;i++)
            {
                Extract(_RawColors[i], out f, out b);
                _Entries[i].Foreground = f;
                _Entries[i].Background = b;
            }
        }


        public static void Extract(byte stuffed, out ConsoleColor foreground, out ConsoleColor background)
        {
            foreground =(ConsoleColor)(stuffed & 0xF);
            background =(ConsoleColor)((stuffed >> 4) & 0xF);
        }


        private byte[] DeriveColors()
        {
            byte[] result = new byte[_Entries.Length];
            for(int i=0;i<result.Length;i++)
            {
                result[i] = Stuff(_Entries[i].Foreground, _Entries[i].Background);
            }
            return result;
        }

        internal static byte Stuff(ConsoleColor foreground, ConsoleColor background)
        {
            return (byte)((((int)background << 4) | (int)foreground) & 0xFF);
        }

        public int Count => _Entries.Length;

        public ColorEntry this[int index] => _Entries[index];

        internal byte Color(int index)
        {
            return _RawColors[index];
        }

        private byte DefaultColor = Stuff(ConsoleColor.Gray, ConsoleColor.Black);

        internal byte ByIndex(int paletteIndex)
        {
            if (paletteIndex <0 || paletteIndex >= _RawColors.Length)
                return DefaultColor;
            return _RawColors[paletteIndex];
        }
    }
}