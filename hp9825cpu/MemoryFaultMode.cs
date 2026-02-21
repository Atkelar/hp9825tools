namespace HP9825CPU
{
    /// <summary>
    /// Indicates what type of memory error to be simulated.
    /// </summary>
    public enum MemoryFaultMode
    {
        /// <summary>
        /// Bits in the mask are always 1 when read from the CPU - equates a "stuck to ground" electrical bit.
        /// </summary>
        StuckOn,
        /// <summary>
        /// Bits in the mask are alays 0 when read from the CPU - equates a "stuck to 5V" electrical bit.
        /// </summary>
        StuckOff,
        /// <summary>
        /// Toggles. Every time this fault is triggered, it toggles between 0 and 1, not caring about the actual bit.
        /// </summary>
        Toggle,
        /// <summary>
        /// Random bit values. The faulty bits will be read from a pseudo-random number generator.
        /// </summary>
        Random,
        /// <summary>
        /// Alwas return the inverted bits from the actual memory location.
        /// </summary>
        Invert
    }
}