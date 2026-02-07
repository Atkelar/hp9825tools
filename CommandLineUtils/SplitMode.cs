namespace CommandLineUtils
{
    /// <summary>
    /// Allowed split points for formatted outputs.
    /// </summary>
    public enum SplitMode
    {
        /// <summary>
        /// Do not split text, force newline if it doesn't fit, falls back to "word" when we are on a new line.
        /// </summary>
        None = 0,
        /// <summary>
        /// Splits at the last possible white space in the line before the line is at the maximum. If we are at the beginning of a line and this isn't possible, falls back to Any.
        /// </summary>
        Word = 1,
        /// <summary>
        /// splits anywhere.
        /// </summary>
        Any = 2
    }
}