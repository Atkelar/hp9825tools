using System;
using System.Linq;

namespace CommandLineUtils.Visuals
{

    internal class CharBuffer
    {
        internal LineBuffer[]? Lines;

        internal struct LineBuffer
        {
            public LineBuffer(int width)
            {
                _CharBuffer = new char[width];
                _ColorBuffer = new byte[width];
            }
            public char[] _CharBuffer;
            public byte[] _ColorBuffer;
        }

        public CharBuffer(Size size)
        {
            Size = size;
            Lines = new LineBuffer[size.Height];
            for(var i = 0;i< size.Height;i++)
            {
                Lines[i] = new LineBuffer(size.Width);
            }
        }

        public Size Size {get;private set;}

        internal void CopyFrom(CharBuffer source, Location targetPosition, Location sourcePosition, Size size)
        {
            if (Lines == null || source.Lines== null)
                return;

            int ys = sourcePosition.Y;
            int yt = targetPosition.Y;
            for(int i = 0; i < size.Height && ys < source.Size.Height && yt < Size.Height;i++,yt++,yt++)
            {
                if (ys<0 || yt<0)
                    continue;

                var sl = source.Lines[ys];
                var tl = Lines[yt];
                
                int xs = sourcePosition.X;
                int xt = targetPosition.X;
                for (int j = 0; j < size.Width && xs < source.Size.Width && xt < Size.Width; j++,xs++,ys++)
                {
                    if (xs<0 || ys<0)
                        continue;
                    tl._CharBuffer[xt] = sl._CharBuffer[xs];
                    tl._ColorBuffer[xt] = sl._ColorBuffer[xs];
                }
            }
        }

        internal void FillColor(Rectangle rectangle, byte color)
        {
            var br = rectangle.BottomRight;
            if (br.HasValue && Lines != null)
            {
                for(int y = rectangle.Position.Y; y <= br.Value.Y; y++)
                {
                    for(int x = rectangle.Position.X; x <= br.Value.X; x++)
                    {
                        Lines[y]._ColorBuffer[x] = color;
                    }
                }
            }
        }

        internal void Fill(Rectangle rectangle, char what)
        {
            var br = rectangle.BottomRight;
            if (br.HasValue && Lines != null)
            {
                for(int y = rectangle.Position.Y; y <= br.Value.Y; y++)
                {
                    for(int x = rectangle.Position.X; x <= br.Value.X; x++)
                    {
                        Lines[y]._CharBuffer[x] = what;
                    }
                }
            }
        }

        /// <summary>
        /// Transfers "length" characters form the string "content", starting at "startOffset" to the location "l"
        /// </summary>
        /// <param name="l">The target position.</param>
        /// <param name="content">The string to copy.</param>
        /// <param name="startOffset">The start offset within the string.</param>
        /// <param name="length">The number of characters to copy.</param>
        internal void Transfer(Location l, string content,int startOffset, int length)
        {
            if (l.Y < 0 || l.Y >= Lines.Length || l.X + length <= 0)
                return;
            var b = Lines[l.Y]._CharBuffer;
            if (l.X >= b.Length)
                return;
            var x = l.X;
            if (x < 0)
            {
                startOffset-=x;
                length+=x;
                x = 0;
            }
            if (x + length > b.Length)
                length = b.Length - x;
            
            content.CopyTo(startOffset, b, x, length);
        }

        internal void SetColor(Location position, byte color)
        {
            if (Lines != null && position.Y >=0 && position.Y < Lines.Length)
            {
                var l = Lines[position.Y]._ColorBuffer;
                if (position.X>=0 && position.X < l.Length)
                    l[position.X] = color;
            }
        }

        internal void Set(Location position, char character)
        {
            if (Lines != null && position.Y >=0 && position.Y < Lines.Length)
            {
                var l = Lines[position.Y]._CharBuffer;
                if (position.X>=0 && position.X < l.Length)
                    l[position.X] = character;
            }
        }
    }
}