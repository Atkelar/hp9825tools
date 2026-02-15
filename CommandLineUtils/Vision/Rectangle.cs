using System;
using System.Collections.Generic;

namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// Defines a rectangular area, which may be "zero" width or height.
    /// </summary>
    public struct Rectangle
    {

        public Rectangle(int x, int y, int w, int h)
        {
            Position = new Location(x,y);
            Size = new Size(w,h);
        }

        public Rectangle(Location loc, Size size)
        {
            Position = loc;
            Size = size;
        }

        /// <summary>
        /// The top left location of the rectangle.
        /// </summary>
        public Location Position {get; private set;}

        /// <summary>
        /// The size of the rectangle.
        /// </summary>
        public Size Size { get; private set; }

        /// <summary>
        /// Localizes a given position.
        /// </summary>
        /// <param name="other">The position, in the "outside" coordinate space.</param>
        /// <returns>A location, relative to the rectangle's position.</returns>
        public Location Localize(Location other)
        {
            return new Location(other.X - Position.X, other.Y - Position.Y);
        }

        /// <summary>
        /// An empty rectangle.
        /// </summary>
        public static readonly Rectangle Empty = new Rectangle(0,0,0,0);

        /// <summary>
        /// True if the rectangle has no visible elements. Either width or height is zero.
        /// </summary>
        public bool IsEmpty => Size.Width == 0 || Size.Height == 0;

        /// <summary>
        /// True if the provided position falls within this rectangle.
        /// </summary>
        /// <param name="position">The position to check.</param>
        /// <returns>True if within, false otherwise.</returns>
        public bool Contains(Location position)
        {
            return position.X >= Position.X && position.Y >= Position.Y && position.X < Position.X + Size.Width && position.Y < Position.Y + Size.Height;
        }

        /// <summary>
        /// The lower right corner in coordinates, null if the rectangle is empty.
        /// </summary>
        public Location? BottomRight
        {
            get
            {
                if (IsEmpty)
                    return null;
                return new Location(Position.X + Size.Width -1, Position.Y + Size.Height - 1);
            }
        }

        /// <summary>
        /// Returns a recta
        /// </summary>
        /// <returns>A rectangle that encompasses the provided coordinates.</returns>
        public static Rectangle FromCoordinates(int x1, int y1, int x2, int y2)
        {
            int width;
            int height;
            if (x2>= x1)
                width = (x2 - x1) + 1;
            else
            {
                width = (x1 - x2) + 1;
                x1 = x2;
            }
            if (y2 >= y1)
                height = (y2 - y1) + 1;
            else    
            {
                height = (x1 - y2) + 1;
                y1 = y2;
            }
            return new Rectangle(x1, y1, width, height);
        }

        /// <summary>
        /// Retreives the "common" boundary of both rectangles.
        /// </summary>
        /// <param name="other">The other rectangle.</param>
        /// <returns>The combined area that encompasses both rectangles, or an empty one if there is no overlap.</returns>
        public Rectangle Intersect(Rectangle other)
        {
            if (this.IsEmpty || other.IsEmpty)
                return Empty;
            
            var lr1 = this.BottomRight!.Value;  // can't be null, empty above should take care of that.
            var lr2 = other.BottomRight!.Value;

            // catch all cases where there is absolutely no overlap.
            if (Position.X > lr2.X || Position.Y > lr2.Y || other.Position.X > lr1.X || other.Position.Y > lr1.Y)
                return Empty;

            return Rectangle.FromCoordinates(Math.Max(Position.X, other.Position.X), Math.Max(Position.Y, other.Position.Y), Math.Min(lr1.X, lr2.X), Math.Min(lr1.Y, lr2.Y));
        }

        /// <summary>
        /// Retreives the "over all" boundary of both rectangles.
        /// </summary>
        /// <param name="other">The other rectangle.</param>
        /// <returns>The combined area that encompasses both rectangles.</returns>
        public Rectangle Union(Rectangle other)
        {
            if (this.IsEmpty)
                return other;
            if (other.IsEmpty)
                return this;    
            var lr1 = this.BottomRight!.Value;  // can't be null, empty above should take care of that.
            var lr2 = other.BottomRight!.Value;
            return Rectangle.FromCoordinates(Math.Min(Position.X, other.Position.X), Math.Min(Position.Y, other.Position.Y), Math.Max(lr1.X, lr2.X), Math.Max(lr1.Y, lr2.Y));
        }

        internal Rectangle Move(Location delta)
        {
            return new Rectangle(Position.Move(delta), Size);
        }

        /// <summary>
        /// True if the other rectangle is 100% covered by THIS rectangle.
        /// </summary>
        /// <param name="other">The rectangle to check.</param>
        /// <returns>True if the other rectangle is completely covered.</returns>
        internal bool Covers(Rectangle other)
        {
            if (other.Position.X >= this.Position.X && other.Position.Y >= this.Position.Y)
            {
                return other.Position.X + other.Size.Width <= this.Position.X + this.Size.Width && other.Position.Y + other.Size.Height <= this.Size.Height + this.Position.Y;
            }
            return false;
        }

        internal Rectangle[] SplitAround(Rectangle otherArea)
        {
            /*
            this is a beast... the goal is to get the following..

            +---------------------------+
            |    top                    |
            |       +---------+         |
            |  left |         |  right  |
            |       +---------+         |
            |    bottom                 |
            +---------------------------+

            top area: lines that are above the "other" rectangle.
            left area: lines/columns below "top" that are left of the other rectangle.
            right area: lines/columns below "top" that are to the right of the other rectangle.
            bottom area: lines that are below the "other" rectangle.

            If any of these areas is empty, the result is omitted.
            */
            if (!this.Intersects(otherArea))
                return Array.Empty<Rectangle>();
            // from here on out, we HAVE some overlap.

            List<Rectangle> result = new List<Rectangle>();
            int currentY = Position.Y;
            int currentHeight = Size.Height;
            int temp;
            if(currentY< otherArea.Position.Y)
            {
                // build top section...
                temp = Math.Min(otherArea.Position.Y - currentY, currentHeight);
                result.Add(new Rectangle(Position.X, currentY, Size.Width, temp));
                currentHeight-=temp;
                currentY+=temp;
                if (currentHeight <=0)
                    return result.ToArray();    // only top...
            }
            int overlapH = Math.Min(otherArea.Size.Height, currentHeight);
            if (Position.X < otherArea.Position.X)
            {
                // we have a "left" area...
                result.Add(new Rectangle(Position.X, currentY, otherArea.Position.X - Position.X, overlapH));
            }
            temp = (Position.X + Size.Width) - (otherArea.Position.X + otherArea.Size.Width);
            if (temp > 0)
            {
                result.Add(new Rectangle(otherArea.Position.X + otherArea.Size.Width, currentY, temp, overlapH));
            }
            currentHeight -= overlapH;
            currentY += overlapH;
            if (currentHeight > 0)
            {
                result.Add(new Rectangle(Position.X, currentY, Size.Width, currentHeight));
            }

            return result.ToArray();
        }

        internal bool Intersects(Rectangle other)
        {
            return 
                other.Position.X < this.Position.X + this.Size.Width &&
                other.Position.Y < this.Position.Y + this.Size.Height &&
                other.Position.X + other.Size.Width > this.Position.X &&
                other.Position.Y + other.Size.Height > this.Position.Y;
        }

        public bool Equals(Rectangle other)
        {
            return other.Position.Equals(this.Position) && other.Size.Equals(this.Size);
        }


        public override string ToString()
        {
            return string.Format("{0}-{1}", Position, BottomRight);
        }

        internal Rectangle MoveBack(Location delta)
        {
             return new Rectangle(Position.MoveMack(delta), Size);
       }
    }
}