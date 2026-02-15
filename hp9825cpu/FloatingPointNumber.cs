using System;
using System.Collections.Generic;
using System.Text;

namespace HP9825CPU
{
    /// <summary>
    /// A floating point number, as defined by the HP9825 CPU: BCD with binary exponent.
    /// </summary>
    public struct FloatingPointNumber
    {
        // TODO: implement "INumber<FloatingPointNumber>"
        // The format is defined on page 52 of the CPU guide.
        // with examples on page 55
        /// <summary>
        /// First word.
        /// </summary>
        public int M { get; private set; }
        /// <summary>
        /// Second word.
        /// </summary>
        public int M1 { get; private set; }
        /// <summary>
        /// Third word.
        /// </summary>
        public int M2 { get; private set; }
        /// <summary>
        /// Fourth word.
        /// </summary>
        public int M3 { get; private set; }

        /// <summary>
        /// New, empty (zero) number.
        /// </summary>
        public FloatingPointNumber()
        {
            M=M1=M2=M3=0;
        }

        /// <summary>
        /// Masks the invalid bits (reserved) bits in the first word.
        /// </summary>
        private const int MInvalidMask = 0x3E;

        /// <summary>
        /// New number based on the four words.
        /// </summary>
        /// <param name="a">First</param>
        /// <param name="b">Second</param>
        /// <param name="c">Third</param>
        /// <param name="d">Fourth</param>
        public FloatingPointNumber(int a, int b, int c, int d)
        {
            M = a;
            M1 = b;
            M2 = c;
            M3 = d;
        }

        /// <summary>
        /// Constant, ready to use zero.
        /// </summary>
        public static readonly FloatingPointNumber Zero = new FloatingPointNumber();
        /// <summary>
        /// Constant, ready to use "+1".
        /// </summary>
        public static readonly FloatingPointNumber One = new FloatingPointNumber(0,0x1000,0,0);
        /// <summary>
        /// Constant, ready to use "-1".
        /// </summary>
        public static readonly FloatingPointNumber MinusOne = new FloatingPointNumber(1,0x1000,0,0);
        /// <summary>
        /// Constant, ready to use "+10".
        /// </summary>
        public static readonly FloatingPointNumber Ten = new FloatingPointNumber(0x40,0x1000,0,0);
        /// <summary>
        /// Constant, ready to use: Pi - as close as possible.
        /// </summary>
        public static readonly FloatingPointNumber Pi = new FloatingPointNumber(0, 0x3141, 0x5926, 0x5360);


        /// <summary>
        /// True if the value is considered zero.
        /// </summary>
        public bool IsZero => M1 == 0 && M2==0 && M3==0;    // technically, we don't care about the signes or exponent, if all other is zero, the number is zero.
        /// <summary>
        /// True if the value is a negative one.
        /// </summary>
        public bool IsNegative => (M & 1) != 0;

        /// <summary>
        /// True if there are set bits in the reserved section.
        /// </summary>
        public bool HasInvalidBits => (M & MInvalidMask)!=0;

        /// <summary>
        /// True if the number is considered a valid format, i.e. no invalid bits and no digits outside 0-9.
        /// </summary>
        public bool IsValid => (!HasInvalidBits) 
            && Digit(1) < 10 && Digit(2) < 10 && Digit(3) < 10 && Digit(4) < 10
            && Digit(5) < 10 && Digit(6) < 10 && Digit(7) < 10 && Digit(8) < 10
            && Digit(9) < 10 && Digit(10) < 10 && Digit(11) < 10 && Digit(12) < 10;


        /// <summary>
        /// Equality comparison.
        /// </summary>
        /// <param name="a">Value to compare.</param>
        /// <param name="b">Value to compare wiht.</param>
        /// <returns>True if the two numbers are equal, false if not.</returns>
        public static bool operator == (FloatingPointNumber a, FloatingPointNumber b) => (a.M & ~MInvalidMask) == (b.M & ~MInvalidMask) && a.M1 == b.M1 && a.M2 == b.M2 && a.M3 == b.M3;
        /// <summary>
        /// Equality comparison.
        /// </summary>
        /// <param name="a">Value to compare.</param>
        /// <param name="b">Value to compare wiht.</param>
        /// <returns>True if the two numbers are equal, false if not.</returns>
        public static bool operator != (FloatingPointNumber a, FloatingPointNumber b) => (a.M & ~MInvalidMask) != (b.M & ~MInvalidMask) || a.M1 != b.M1 || a.M2 != b.M2 || a.M3 != b.M3;

        /// <summary>
        /// Direct access to mantissa elements.
        /// </summary>
        /// <param name="index">The index (1-12!) to read.</param>
        /// <returns>The value (0-9) of the given digit.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The index was outside the valid range.</exception>
        public int Digit(int index)
        {
            int m = index < 1 ? throw new ArgumentOutOfRangeException(nameof(index), index, "1-12 only!") : (index < 5 ? M1 : (index < 9 ? M2 : (index < 13 ? M3 : throw new ArgumentOutOfRangeException(nameof(index), index, "1-12 only!"))));
            switch(index)
            {
                case 1:
                case 5:
                case 9:
                    m >>= 12;
                    break;
                case 2:
                case 6:
                case 10:
                    m >>= 8;
                    break;
                case 3:
                case 7:
                case 11:
                    m >>= 4;
                    break;
            }   
            return m & 0xF;
        }

        /// <summary>
        /// Gets the stored exponent, -512..+511.
        /// </summary>
        public int Exponent => (M & 0x8000) != 0 ? (-512 + ((M >> 6) & 0x1FF)) : ((M >> 6) & 0x3FF);

        /// <summary>
        /// Returns true if the number is normalized, i.e. the first digit is non-zero; except for "all zero", which is also normalized.
        /// </summary>
        bool IsNormalized => (M1 & 0xF000) != 0 || IsZero;

        /// <summary>
        /// Convert to double.
        /// </summary>
        /// <param name="input">The number to convert.</param>
        public static implicit operator double(FloatingPointNumber input)
        {
            return input.ToDouble();
        }
        /// <summary>
        /// Convert from double.
        /// </summary>
        /// <param name="input">The number to convert.</param>
        public static implicit operator FloatingPointNumber(double input)
        {
            return FromDouble(input);
        }

        /// <summary>
        /// Create from the individual digits/parts.
        /// </summary>
        /// <param name="negative">True for negative mantissa.</param>
        /// <param name="d1">First digtit; must be 1-9.</param>
        /// <param name="d2">Second digit, 0-9</param>
        /// <param name="d3">Third digit, 0-9</param>
        /// <param name="d4">4th digit, 0-9</param>
        /// <param name="d5">5th digit, 0-9</param>
        /// <param name="d6">6th digit, 0-9</param>
        /// <param name="d7">7th digit, 0-9</param>
        /// <param name="d8">8th digit, 0-9</param>
        /// <param name="d9">9th digit, 0-9</param>
        /// <param name="d10">10th digit, 0-9</param>
        /// <param name="d11">11th digit, 0-9</param>
        /// <param name="d12">12th digit, 0-9</param>
        /// <param name="exponent">The exponent. -512 to +511.</param>
        /// <returns>The prepared (packed) BCD number.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Some of the parameters didn't add up.</exception>
        public static FloatingPointNumber FromDigits(
            bool negative, 
            int d1, int d2=0, int d3=0, int d4=0,int d5=0,int d6=0,int d7=0,int d8=0,int d9=0,int d10=0,int d11=0,int d12=0, 
            int exponent = 0)
        {
            if (exponent < -512 || exponent > 511)
                throw new ArgumentOutOfRangeException(nameof(exponent), exponent, "Floating points are e[-512/+511] only!");
            if (d1<1 || d1 > 9)
                throw new ArgumentOutOfRangeException(nameof(d1), d1, "Digit 1 needs to be non-zero and maximum 9!");
            return new FloatingPointNumber(
                (exponent & 0x3FF) << 6 | (negative ? 1 : 0),
                PackDigit(d1, nameof(d1), 12) | PackDigit(d2, nameof(d2), 8) | PackDigit(d3, nameof(d3), 4) | PackDigit(d4, nameof(d4), 0),
                PackDigit(d5, nameof(d5), 12) | PackDigit(d6, nameof(d6), 8) | PackDigit(d7, nameof(d7), 4) | PackDigit(d8, nameof(d8), 0),
                PackDigit(d9, nameof(d9), 12) | PackDigit(d10, nameof(d10), 8) | PackDigit(d11, nameof(d11), 4) | PackDigit(d12, nameof(d12), 0)
            );
        }

        private static int PackDigit(int value, string name, int bits)
        {
            if(value < 0 || value > 9)
                throw new ArgumentOutOfRangeException(name, value, "Digit needs to be 0-9 for BCD coding!");
            return value << bits;
        }

        /// <summary>
        /// Converts from double.
        /// </summary>
        /// <param name="d">The input.</param>
        /// <returns>The parsed number.</returns>
        /// <exception cref="NotImplementedException">Ideas?</exception>
        public static FloatingPointNumber FromDouble(double d)
        {
            // umm.... ideas?
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the floating point number from a memory location.
        /// </summary>
        /// <param name="m">The memroy manager to query.</param>
        /// <param name="address">The starting address for the number.</param>
        /// <returns>The created number.</returns>
        public static FloatingPointNumber FromMemory(MemoryManager m, int address)
        {
            return new FloatingPointNumber(
                m[address],
                m[address+1],
                m[address+2],
                m[address+3]
            );
        }

        /// <summary>
        /// Gets the floating point number from a memory location.
        /// </summary>
        /// <param name="m">The memroy manager to query.</param>
        /// <param name="address">The starting address for the number.</param>
        /// <returns>The created number.</returns>
        public static FloatingPointNumber FromMemory(Memory m, int address)
        {
            return new FloatingPointNumber(
                m[address],
                m[address+1],
                m[address+2],
                m[address+3]
            );
        }

        /// <summary>
        /// Gets the floating point number from specified words.
        /// </summary>
        /// <param name="a">First</param>
        /// <param name="b">Second</param>
        /// <param name="c">Third</param>
        /// <param name="d">Fourth</param>
        /// <returns>The created number.</returns>
        public static FloatingPointNumber FromParts(int a, int b, int c, int d)
        {
            var x = new FloatingPointNumber(a,b,c,d);
            if (!x.IsValid)
                throw new InvalidOperationException("Number contains invalid bits!");
            return x;
        }

        /// <summary>
        /// Converts the number to a double value.
        /// </summary>
        /// <returns>The converted number. Might include rounding issues from the double type...</returns>
        public double ToDouble()
        {
            double baseNumber = 0;
            for(int i = 12; i > 0; i--)
            {
                baseNumber/=10;
                baseNumber+=Digit(i);
            }
            if (IsNegative) // considered using ?: but that would add a "*1" to ever positive conversion...
                return -baseNumber * Math.Pow(10, Exponent);
            return baseNumber * Math.Pow(10, Exponent);
        }

        /// <summary>
        /// Writes the current number back to the provided memory address.
        /// </summary>
        /// <param name="m">The target memory.</param>
        /// <param name="address">The target address.</param>
        public void WriteTo(MemoryManager m, int address)
        {
            m[address] = M;
            m[address+1] = M1;
            m[address+2] = M2;
            m[address+3] = M3;
        }

        /// <summary>
        /// Writes the current number back to the provided memory address.
        /// </summary>
        /// <param name="m">The target memory.</param>
        /// <param name="address">The target address.</param>
        public void WriteTo(Memory m, int address)
        {
            m[address] = M;
            m[address+1] = M1;
            m[address+2] = M2;
            m[address+3] = M3;
        }

        /// <summary>
        /// Gets the string version of the number; Plain, fixed format.
        /// </summary>
        /// <returns>String version.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (IsNegative)
                sb.Append('-');
            sb.Append(Digit(1));
            sb.Append('.');
            for(int i = 2; i <= 12; i++)
                sb.Append(Digit(i));
            sb.Append('e');
            sb.Append(Exponent);
            return sb.ToString();
        }

        /// <summary>
        /// Parses the provided string as a number.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">For malformed input.</exception>
        /// <remarks>
        /// <para>We support combinations of mantissa and exponent, leading zeros are ignored, digits past 12 are ignored. The '.' is the decimal separator, leading '+' for signs are ignored.</para>
        /// </remarks>
        public static FloatingPointNumber Parse(string input)
        {
            int i = 0;
            bool isNegative = false;
            bool isInExponent = false;
            bool isInFraction = false;
            bool isNegativeExponent = false;
            bool started = false;
            int exponentImplied = 0;
            int exponentProvided = 0;
            List<int> significantDigits = new List<int>();
            while (i < input.Length)
            {
                char c = input[i];
                i++;
                if (char.IsWhiteSpace(c))
                    continue;
                switch(c)
                {
                    case '+':
                    case '-':
                        if (!started)
                        {
                            started = true;
                            if (!isInExponent)
                                isNegative = c == '-';
                            else
                                isNegativeExponent = c == '-';
                        }
                        else
                            throw new FormatException("The sign is not allowed within the numeric part!");
                        break;
                    case '.':
                        if (isInExponent)
                            throw new FormatException("Decimal point invalid in exponent part!");
                        if (isInFraction)
                            throw new FormatException("Duplicate decimal point!");
                        isInFraction = true;
                        break;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        started = true;     // no longer allow sign.
                        int dVal = ((int)c) - ((int)'0');
                        if (isInExponent)
                        {
                            exponentProvided *= 10;
                            exponentProvided += dVal;
                        }
                        else
                        {
                            // cases...
                            if (!isInFraction)
                            {
                                // 0000001234  - ignore leading zeros...
                                if (dVal != 0 || significantDigits.Count>0)
                                {
                                    // found a relevant digit.
                                    if (significantDigits.Count < 12)
                                        significantDigits.Add(dVal);
                                    // starting at the second significant digit, we have to increment the exponent...
                                    if (significantDigits.Count>1)
                                        exponentImplied++;
                                }
                            }
                            else
                            {
                                // we found fractional values...
                                if (significantDigits.Count == 0)
                                {
                                    // we have a 0000.0000x case, just move the exponent...
                                    exponentImplied--;
                                }
                                if (dVal != 0 || significantDigits.Count>0)
                                {
                                    // found one digit at least...
                                    if (significantDigits.Count < 12)
                                        significantDigits.Add(dVal);
                                }
                            }
                        }
                        break;
                    case 'e':
                        if (isInExponent)
                            throw new FormatException("Duplicate exponent marker!");
                        isInExponent = true;
                        started = false;    // wait for possible sign...
                        break;
                    default:
                        throw new FormatException($"Invalid character in floating point number: {c}!");
                }
            }

            if (significantDigits.Count == 0)   // why?
                return Zero;
            // now we should have all we need to create the number... 
            for(i = significantDigits.Count; i < 12; i++)   // fill to 12 digits...
                significantDigits.Add(0);
            if (isNegativeExponent)
                exponentProvided*=-1;
            int exponent = exponentProvided + exponentImplied;
            if (exponent < -512 || exponent > 511)
                throw new FormatException($"The parsed number had an invalid exponent. -512..511 are valid, but was {exponent}");
            var num = FromDigits(isNegative, 
                significantDigits[0], significantDigits[1], significantDigits[2], significantDigits[3], 
                significantDigits[4], significantDigits[5], significantDigits[6], significantDigits[7], 
                significantDigits[8], significantDigits[9], significantDigits[10], significantDigits[11], 
                exponent);
            if (!num.IsNormalized || !num.IsValid)
                throw new FormatException($"The parsed number resulted in an invalid state... this should not happen?!");
            return num;
        }

        internal int[] GetMantissa()
        {
            var result = new int[12];
            for(int i = 0; i< 12; i++)
                result[i] = Digit(i+1);
            return result;
        }

        internal void PutMantissa(int[] digits)
        {
            M1 = PackDigit(digits[0], "[0]", 12) | PackDigit(digits[1], "[1]", 8) | PackDigit(digits[2], "[2]", 4) | PackDigit(digits[3], "[3]", 0);
            M2 = PackDigit(digits[4], "[4]", 12) | PackDigit(digits[5], "[5]", 8) | PackDigit(digits[6], "[6]", 4) | PackDigit(digits[7], "[7]", 0);
            M3 = PackDigit(digits[8], "[8]", 12) | PackDigit(digits[9], "[9]", 8) | PackDigit(digits[10], "[10]", 4) | PackDigit(digits[11], "[11]", 0);
        }
    }
}