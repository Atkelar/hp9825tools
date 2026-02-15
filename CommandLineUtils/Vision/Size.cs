using System;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;

namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// Encapsulates the size of an element on screen. 
    /// </summary>
    public struct Size
    {
        /// <summary>
        /// Creates a new size structure - with the provided paramters.
        /// </summary>
        /// <param name="width">The number of characters across.</param>
        /// <param name="height">The number of lines hight.</param>
        public Size(int width, int height)
        {
            Width = width < 0 ? 0 : width;
            Height = height < 0 ? 0 : height;
        }

        /// <summary>
        /// An empty (0x0) size.
        /// </summary>
        public static readonly Size Null = new Size();
        /// <summary>
        /// A single character (1x1) size.
        /// </summary>
        public static readonly Size Char = new Size(1,1);

        /// <summary>
        /// Maximum possible size.
        /// </summary>
        public static readonly Size MaxValue = new Size(int.MaxValue, int.MaxValue);

        /// <summary>
        /// The width in characters.
        /// </summary>
        public int Width { get; private set; }
        /// <summary>
        /// The height in lines.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Returns a new size definitin, that fits between minimum and maximum size values.
        /// </summary>
        /// <param name="minimum">The smallest allowable size.</param>
        /// <param name="maximum">The largest allowable size. If null, will not be limited.</param>
        /// <returns></returns>
        internal Size TrimTo(Size minimum, Size? maximum = null)
        {
            var max = maximum.GetValueOrDefault(MaxValue);

            int w = Width > max.Width ? max.Width : Width;
            int h = Height > max.Height ? max.Height : Height;

            w = w < minimum.Width ? minimum.Width : Width;
            h = w < minimum.Height ? minimum.Height : Height;

            return new Size(w, h);
        }

        /// <summary>
        /// Parses a size string, formatted as ##x## into a size reference.
        /// </summary>
        /// <param name="sizeSpec">The size string.</param>
        /// <returns>The parsed size. May be "empty"!</returns>
        public static Size Parse(string sizeSpec)
        {
            var p = sizeSpec.Split("x", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (p.Length != 2)
                throw new FormatException("The input is not a size specification: #x# was expected!");
            int w,h;
            if (!int.TryParse(p[0], out w))
                throw new FormatException("The input is not a size specification: #x# was expected!");
            if (!int.TryParse(p[1], out h))
                throw new FormatException("The input is not a size specification: #x# was expected!");
            if (h < 0 || w < 0)
                throw new FormatException("The input is not a size specification: #x# was expected!");
            return new Size(w, h);
        }

        /// <summary>
        /// Parses a size string, formatted as ##x## into a size reference.
        /// </summary>
        /// <param name="sizeSpec">The size string.</param>
        /// <returns>The parsed size. May be "empty"!</returns>
        public static Size? Parse(string? sizeSpec, Size minimumSize, Size? maximumSize)
        {
            if (string.IsNullOrWhiteSpace(sizeSpec))
                return null;
            var s = Parse(sizeSpec);
            int w = s.Width;
            int h = s.Height;
            if (maximumSize.HasValue)
            {
                w = Math.Min(maximumSize.Value.Width, w);
                h = Math.Min(maximumSize.Value.Height, h);
            }
            return new Size(Math.Max(w, minimumSize.Width), Math.Max(h, minimumSize.Height));
        }

        public bool Equals(Size other)
        {
            return this.Width == other.Width && this.Height == other.Height;
        }

        public override string ToString()
        {
            return string.Format("{0}*{1}", Width, Height);
        }
    }
}