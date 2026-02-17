namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// Utility functions around the visual part of the library.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Pads the provided string to a specific (hard limited) length. Characters are added to the right end of the string.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="length"></param>
        /// <param name="padding"></param>
        /// <returns></returns>
        public static string PadRight(this string input, int length, char padding = ' ')
        {
            if (input.Length==length)
                return input;
            if (input.Length>length)
                return input.Substring(0,length);
            return input + new string(padding, (input.Length - length));
        }

        /// <summary>
        /// Pads the provided string to a specific (hard limited) length. Characters are added to the left end of the string.
        /// </summary>
        public static string PadLeft(this string input, int length, char padding = ' ')
        {
            if (input.Length==length)
                return input;
            if (input.Length>length)
                return input.Substring(input.Length - length);
            return new string(padding, (input.Length - length)) + input;
        }


        public static string PadCenter(this string input, int length, char padding = ' ')
        {
            if (input.Length==length)
                return input;
            int l1,l2;
            if (input.Length>length)
            {
                l1 = (input.Length - length) / 2;
                return input.Substring(l1, length);
            }
            l1 = (length - input.Length) / 2;
            l2 = length - l1;

            return (l1 > 0 ? new string(padding, l1) : string.Empty) + input + (l2 > 0 ? new string(padding, l2) : string.Empty);
        }
    }
}