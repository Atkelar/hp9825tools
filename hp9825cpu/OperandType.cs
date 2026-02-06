namespace HP9825CPU
{
    enum OperandType
    {
        None = 0,
        /// <summary>
        /// Operand is "n-1", i.e. real value is masked value + 1...
        /// </summary>
        NValue,
        /// <summary>
        /// Index of a register only.
        /// </summary>
        RegIndex,
        /// <summary>
        /// +/- skip value for branching.
        /// </summary>
        SkipValue,
        /// <summary>
        /// 11-bit special case for address references. Including page bit decoding!
        /// </summary>
        MemoryAddress,
        /// <summary>
        /// Same as SkipValue but suppress the *+ notation...
        /// </summary>
        SkipValueNonRelative,
    }
}