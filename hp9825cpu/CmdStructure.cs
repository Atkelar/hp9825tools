using System.Collections.Generic;
using System.Linq;

namespace HP9825CPU
{

    internal struct CmdStructure
    {
        public CmdStructure(int v, int m, string mn, OperandType ot, int om)
            : this(v, m, mn)
        {
            OperandMask = om;
            OperandType = ot;
        }
        public CmdStructure(int v, int m, string mn, bool is16BitOnly = false)
        {
            Value = v;
            ValueMask = m;
            Mnemonic = mn;
            Is16Bit = is16BitOnly;
        }

        public bool Is16Bit;
        public int Value;
        public int ValueMask;
        public string Mnemonic;
        public int OperandMask;
        public OperandType OperandType;
        public MnemonicUpdateSpec[]? Updates;

        private List<Variant>? Variants = null;

        public class Variant
        {
            public int OpCode { get; internal set; }
            public string Mnemonic { get; internal set; }
        }

        public IEnumerable<Variant> GetVersions()
        {
            if (Variants == null)
            {
                Variants = new List<Variant>();
                Variants.Add( new Variant() { Mnemonic = this.Mnemonic, OpCode = this.Value });
                if (Updates != null)
                {
                    foreach (var u in Updates.Where(x => !x.IsSuffixUpdate))
                    {
                        int oldOnes = Variants.Count;

                        // copy current version and set the "1" case...
                        for (int i = 0; i < oldOnes; i++)
                        {
                            Variants.Add(new Variant()
                            {
                                Mnemonic = Variants[i].Mnemonic.Replace(u.What.Placeholder, u.What.NonZeroReplace),
                                OpCode = Variants[i].OpCode | u.Mask
                            });
                        }

                        // we need to update the current versions with the "0" case mnemonic...
                        for (int i = 0; i < oldOnes; i++)
                        {
                            Variants[i].Mnemonic = Variants[i].Mnemonic.Replace(u.What.Placeholder, u.What.ZeroReplace);
                        }
                    }
                }
            }
            return Variants;
        }
    }

}