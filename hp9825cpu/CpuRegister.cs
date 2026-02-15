namespace HP9825CPU
{
    /// <summary>
    /// Access the CPU registers by name... the numeric value matches the "memory location" of the register.
    /// </summary>
    public enum CpuRegister
    {
        /// <summary>
        /// The first accumulator.
        /// </summary>
        A = 0,
        /// <summary>
        /// The second accumulator.
        /// </summary>
        B = 1,
        /// <summary>
        /// The program counter (PC)
        /// </summary>
        P = 2,
        /// <summary>
        /// Return (call) Stack Pointer
        /// </summary>
        R = 3,
        /// <summary>
        /// R4 - I/O register 0
        /// </summary>
        R4 = 4,
        /// <summary>
        /// R4 - I/O register 1
        /// </summary>
        R5 = 5,
        /// <summary>
        /// R5 - I/O register 2
        /// </summary>
        R6 = 6,
        /// <summary>
        /// R6 - I/O register 3
        /// </summary>
        R7 = 7,
        /// <summary>
        /// Interrupt vecotor (upper 12 bits only!)
        /// </summary>
        IV = 8,
        /// <summary>
        /// Peripheral Address (lower 4 bits only!)
        /// </summary>
        PA = 9,
        /// <summary>
        /// Working Register.
        /// </summary>
        W = 10,
        /// <summary>
        /// DMA Peripheral Address (lower 4 bits) and on 16-bit CPUs, C and D block in upper two bits.
        /// </summary>
        DMAPA = 11,
        /// <summary>
        /// DMA Memory Address and direction.
        /// </summary>
        DMAMA = 12,
        /// <summary>
        /// DMA Counter Register
        /// </summary>
        DMAC = 13,
        /// <summary>
        /// Stack Pointer 1
        /// </summary>
        C = 14,
        /// <summary>
        /// Stack Pointer 2
        /// </summary>
        D = 15,
        /// <summary>
        /// BCD Accumulator (4 words, starting here)
        /// </summary>
        AR2 = 16,
        /// <summary>
        /// Shift Extend Register.
        /// </summary>
        SE = 20,
//            X= 21,
    }
}