using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace HP9825CPU
{
    public static class HP9825Charset
    {
                // charset is an approximation to keep within the BMP of unicode.
        // Known "issues": 2 = X with a bar, 3 = N with a bar, 6 = n with a bar.
        // assumed: N is actually ň and Ň, for X - no plausible alternative found, 
        // using CHI instead
        private const string Charset =
            @"◀¿χŇαϑΓňΔσ↓λμ←τΦ" +   // 0-15
            @"ΘΩδÅåÄäÖöÜüӔӕ²£▒" +    // 16-31
            @" !""#$%&'()*+,-./" +   // 32-47 
            @"0123456789:;<=>?" +    // 48-63
            @"@ABCDEFGHIJKLMNO" +    // 64-79
            @"PQRSTUVWXYZ[√]↑_" +    // 80-95
            @"`abcdefghijklmno" +    // 96-111
            @"pqrstuvwxyzπ|→ΣͰ";    // 112-127

        public static string FromHP9825(IEnumerable<byte> input)
        {
            StringBuilder sb = new StringBuilder();
            foreach(var b in input)
            {
                sb.Append(Charset[b & 0x7F]);
            }
            return sb.ToString();
        }

        public static char FromHP9825(byte inputCode)
        {
            return Charset[inputCode & 0x7F];
        }

        public static byte[] ToHP9825(string input)
        {
            byte[] result = new byte[input.Length];
            int cnt = 0;
            foreach(var c in input)
            {
                int idx = Charset.IndexOf(c);
                if (idx<0)
                    throw new InvalidOperationException($"Input string had non-existing character {c}!");
                result[cnt] = (byte)idx;
                cnt++;
            }
            return result;
        }
        
    }
}