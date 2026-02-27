namespace HP9825CPU
{
    /// <summary>
    /// The pre-defined RAM configurations.
    /// </summary>
    public enum RamConfiguration
    {
        /// <summary>
        /// Base unit - 8k RAM (4k-word)
        /// </summary>
        Ram8k = 1,
        /// <summary>
        /// 16k RAM (8k-oword, Option 001)
        /// </summary>
        Ram16k = 2,
        /// <summary>
        /// 24k RAM (12k-word, Option 002)
        /// </summary>
        Ram24k = 3,
        /// <summary>
        /// 32k RAM (16k-word, Option 003)
        /// </summary>
        Ram32k = 4
    }
}