namespace HP9825CPU
{
    public struct MnemonicUpdateSpec
    {
        public MnemonicUpdateSpec(int cm, int m, MnemonicUpdateInfo w, bool suf = false)
            : this(m, w, suf)
        {
            ConditionMask = cm;
        }
        public MnemonicUpdateSpec(int m, MnemonicUpdateInfo w, bool suf = false)
        {
            Mask = m;
            What = w;
            IsSuffixUpdate = suf;
        }
        public int Mask;
        public int ConditionMask;
        public MnemonicUpdateInfo What;
        public bool IsSuffixUpdate;
    }
}