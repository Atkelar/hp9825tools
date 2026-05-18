namespace HP9825CPU
{
    public enum MnemonicClass : byte
    {
        UnexpectedCode = 0,
        Operand = 1,
        UnaryOperator = 2,
        BinaryOperator = 3,
        EndOfLine = 4,
        Literal = 5,
        GtoOrGsb = 6,
        OptionalROM = 7,
        CharacterString = 8,
        RealNumber = 9,
        IntegerNumber = 10,
        Ignore = 11,
    }
}