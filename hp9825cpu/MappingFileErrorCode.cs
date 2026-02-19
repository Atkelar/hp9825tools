namespace HP9825CPU
{
    public enum MappingFileErrorCode
    {
        MissingIntro = 1,
        DuplicateIntro = 2,
        SyntaxError = 3,
        InvalidOption = 4,
        UnknownDirective = 5,
        InvalidNumeral = 6,
        AddressOutOfRange = 7,
        MissingSection = 8,
        SubsectionAnormal = 9,
        MissingLabel = 10,
        LabelIsReservedName = 11,
        LabelIsInvalid = 12,
        InvalidOptionValue = 13,
        ValueOutOfRange = 14,
    }
}