namespace HP9825CPU
{
    public struct MnemonicUpdateInfo
    {
        public MnemonicUpdateInfo(char ph, char z, char nz)
        {
            Placeholder = ph;
            ZeroReplace = z;
            NonZeroReplace = nz;
        }
        public char Placeholder;
        public char ZeroReplace;
        public char NonZeroReplace;
    }
}