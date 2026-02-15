using System;
using System.Threading;

namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// Encapsulates a "location". Note that this can also be negative, to account for "moving off screen" conditions.
    /// </summary>
    public struct Location
    {

        /// <summary>
        /// Creates a new location at a specific coordiate.
        /// </summary>
        /// <param name="x">X-coordinate (horizontal, positive towards the right!)</param>
        /// <param name="y">Y-coordinate (vertical, positive down!)</param>
        public Location(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// A location that is 0/0.
        /// </summary>
        public static readonly Location Origin = new Location();

        /// <summary>
        /// X-coordinate.
        /// </summary>
        public int X { get; private set; }
        /// <summary>
        /// Y-coordinate.
        /// </summary>
        public int Y { get; private set; }

        public Location Move(int deltaX, int deltaY)
        {
            return new Location(X + deltaX, Y + deltaY);
        }

        public Location Move(Location offset)
        {
            return new Location(X + offset.X, Y + offset.Y);
        }


        /// <summary>
        /// Creates a "relative" position by subtracting the current location from the provided origin.
        /// </summary>
        /// <param name="origin">Relative "to" this position.</param>
        /// <returns>The new position, might be negative!</returns>
        public Location RelateiveTo(Location origin)
        {
            return new Location(X - origin.X, Y - origin.Y);
        }

        public bool Equals(Location other)
        {
            return this.X == other.X && this.Y == other.Y;
        }

        public override string ToString()
        {
            return string.Format("{0}/{1}", X, Y);
        }

        internal Location MoveMack(Location delta)
        {
            return new Location(this.X-delta.X, this.Y - delta.Y);
        }
    }
}