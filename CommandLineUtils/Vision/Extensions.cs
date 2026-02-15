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
    }
}