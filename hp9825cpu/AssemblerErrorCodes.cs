namespace HP9825CPU
{
    public enum AssemblerErrorCodes
    {
        None = 0,
        LabelOnly = 1,
        InvalidLabel = 2,
        LabelWithoutLocation = 3,
        DuplicateLabel = 4,
        ValueUndefined = 5,
        OrrMissingOrg = 6,
        NotImplemented = 7,
        DataWithoutLocation = 8,
        InvalidPerCpuMode = 9,
        InvalidSuffix = 10,
        IntegerOverflow = 11,
        MissingArguments = 12,
        ValueOutOfRange = 13,
        MissingLabel = 14,
        InstructionWithoutLocation = 15,
        AddressOutOfRange = 16,
        UnknownMnemonic = 17,

        InvaldRegister = 18,
        SyntaxError = 19,
        InvalidNumeral = 20,
        RelocationRecursion = 21,
        InputAfterEnd = 22,
        MissingEnd = 23,
        RepMalformed = 24,

        InvalidConditionalNesting = 25,

        LabelUsesReservedName = 26
    }
}