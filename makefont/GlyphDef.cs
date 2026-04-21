
namespace makefont
{
    internal class GlyphDef
    {
        private int _Width;
        private int _Height;

        public GlyphDef(int w, int h, int num, char displayAs)
        {
            _Width = w;
            _Height = h;
            Index = num;
            DisplayChar = displayAs;
            Matrix = new long[_Height];
            _Line = 0;
        }

        public int Index { get; private set; }
        public char DisplayChar { get; private set; }

        private long[] Matrix;
        private int _Line;

        internal LineParseResult AppendLine(string line)
        {
            if (_Line >= _Height)
                return LineParseResult.TooManyLines;
            line = line.TrimEnd();  // ignore empty part if too long...
            if (line.Length > _Width)
                return LineParseResult.TooWide;
            for(int x = 0; x < Math.Min(line.Length, _Width);x++ )
            {
                SetPix(x, _Line, line[x] != ' ');
            }
            _Line++;
            return LineParseResult.OK;
        }

        internal void SetPix(int x, int y, bool on)
        {
            long mask = 1L << x;
            if (on)
            {
                Matrix[y] |= mask;
            }
            else
            {
                Matrix[y] &= ~mask;
            }
        }

        internal bool GetPix(int x, int y)
        {
            long mask = 1L << x;
            return (Matrix[y] & mask) != 0;
        }
    }
}