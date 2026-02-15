using System.Diagnostics.Contracts;

namespace HP9825CPU
{
    public struct MnemonicUpdateSpec
    {
        public MnemonicUpdateSpec(int cm, int m, MnemonicUpdateInfo w, bool suf = false, bool defaultsToSet = false)
            : this(m, w, suf, defaultsToSet)
        {
            ConditionMask = cm;
        }
        public MnemonicUpdateSpec(int m, MnemonicUpdateInfo w, bool suffix = false, bool defaultsToSet = false)
        {
            Mask = m;
            What = w;
            IsSuffixUpdate = suffix;
            DefaultsToSet = defaultsToSet;
        }
        public int Mask;
        public int ConditionMask;
        public MnemonicUpdateInfo What;
        public bool IsSuffixUpdate;
        public bool DefaultsToSet;
    }
}