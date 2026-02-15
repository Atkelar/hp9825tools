using System;
using System.Runtime;
using System.Runtime.InteropServices;

namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// Encapsulates the output drawing context; similar to a "Device Context" in Windows, this object provides character output primitives.
    /// </summary>
    public class PaintContext
    {
        private readonly Rectangle _Clip;
        private readonly CharBuffer _Target;
        private readonly Location _Offset;
        private readonly Palette? _Palette;

        internal PaintContext(Palette? palette, Rectangle clip, Location offset, CharBuffer target)
        {
            _Clip = clip;
            _Target = target;
            _Offset = offset;
            _Palette = palette;
        }

        /// <summary>
        /// Draw a single character to the output buffer.
        /// </summary>
        /// <param name="position">The location for the character.</param>
        /// <param name="character">The character to paint.</param>
        /// <param name="paletteIndex">The color index within the visual's palette.</param>
        public void DrawChar(Location position, char character, int paletteIndex)
        {
            if (position.Y < _Clip.Position.Y || position.Y >= _Clip.Position.Y + _Clip.Size.Height ||
                position.X < _Clip.Position.X || position.X >= _Clip.Position.X + _Clip.Size.Width)
                return;
            position = position.Move(_Offset);
            if (_Palette != null)
            {
                _Target.SetColor(position, _Palette.ByIndex(paletteIndex));
            }
            _Target.Set(position, character);
        }

        /// <summary>
        /// Draws the provided string to the indicated location, based on "local coordinates".
        /// </summary>
        /// <param name="position">The position within the visual.</param>
        /// <param name="content">The string to print.</param>
        /// <param name="paletteIndex">The color index from the Visual's palette.</param>
        public void DrawString(Location position, string content, int paletteIndex)
        {
            if (position.Y< _Clip.Position.Y || position.Y >= _Clip.Position.Y + _Clip.Size.Height)
                return;
            var len = content.Length;
            if (position.X + len < _Clip.Position.X || position.X >= _Clip.Position.X + _Clip.Size.Width)
                return;
            var pos = position.Move(_Offset);

            int ofs = 0;
            // trim start...
            if (pos.X < _Clip.Position.X)
            {
                ofs = _Clip.Position.X - pos.X;
                pos = new Location(pos.X + ofs, pos.Y);
                len -= ofs;
            }
            if (len + _Clip.Position.X > _Clip.Size.Width)
            {
                len -= _Clip.Size.Width - _Clip.Position.X;
            }

            if (_Palette != null)
            {
                var r = new Rectangle(pos, new Size(len, 1));
                _Target.FillColor(r, _Palette.ByIndex(paletteIndex));
            }
            _Target.Transfer(pos, content, ofs, len);
        }

        /// <summary>
        /// Fills a single line with a specific character.
        /// </summary>
        /// <param name="position">The starting position, within the Visual.</param>
        /// <param name="paletteIndex">The color index within the Visual's palette.</param>
        /// <param name="what">The character to repeat.</param>
        /// <param name="number">The number of times to repeat the character.</param>
        public void Repeat(Location position, char what, int number, int paletteIndex)
        {
            if (position.Y< _Clip.Position.Y || position.Y > _Clip.Position.Y + _Clip.Size.Height)
                return;
            var r = new Rectangle(position.Move(_Offset), new Size(number, 1));
            if (_Palette != null)
                _Target.FillColor(r, _Palette.ByIndex(paletteIndex));
            _Target.Fill(r, what);
        }
       
    }
}