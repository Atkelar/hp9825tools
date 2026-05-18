using System.Diagnostics.CodeAnalysis;

namespace HP9825CPU
{
    internal struct MixedContentPart
    {
        public FloatingPointNumber? Number {get;}
        public string? String {get;}
        public int DimLength {get;}

        public MixedContentPart(string str, int dimLength)
        {
            String = str;
            DimLength = dimLength;
            Number = null;
        }

        public int StorageSize
        {
            get
            {
                if (Number.HasValue)
                    return 8;
                int rLength = DimLength;
                if (rLength % 2 != 0)
                    rLength ++;
                return rLength + 8; // header...
            }
        }
        
        public MixedContentPart(FloatingPointNumber num)
        {
            String = null;
            DimLength = 0;
            Number = num;
        }
    }
}