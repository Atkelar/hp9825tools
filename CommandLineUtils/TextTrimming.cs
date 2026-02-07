namespace CommandLineUtils
{
    /// <summary>
    /// Option to trim text for output on column based devices.
    /// </summary>
    public enum TextTrimming
    {
        /// <summary>
        /// Trim texts at the end if needed.
        /// </summary>
        End,
        /// <summary>
        /// Trim at the beginning if needed.
        /// </summary>
        Beginning,
        /// <summary>
        /// Trim on both ends equally if needed.
        /// </summary>
        Both,
        /// <summary>
        /// No trim, always use full output even if it messes with formatting.
        /// </summary>
        NoTrim

    }
}