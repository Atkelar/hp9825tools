using System.Runtime.CompilerServices;

namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// A "desktop" - a single pattern character drawn out across the entire size of the visual, no input handling.
    /// </summary>
    public class Desktop
        : Visual
    {
        public Desktop(char fillerChar = '▒')
        {
            _Filler = fillerChar;
        }

        private char _Filler;

        public override void ParentSizeChanged(Size newSize)
        {
            // we keep the "desktop" at the same size as the parent.
            this.Position = Location.Origin;
            this.Size = newSize;
            base.ParentSizeChanged(newSize);
        }

        protected override void Paint(PaintContext p)
        {

            for(int y = 0; y < Size.Height;y++)
                p.Repeat(new Location(0,y), _Filler, Size.Width, 0);
        }
    }
}