using System;
namespace HP9825CPU
{
    public class CodeFormatOptions
    {
        public NumberFormatType NumberFormat { get; set; } = NumberFormatType.Octal;

        public bool IncludeComments { get; set; } = true;

        public bool SuppressExtraLines { get; set; } = false;

        public bool IncludeNonPrintedLines { get; set; } = false;

        /// <summary>
        /// Format any non-code to all-upper case too (comments, headers, ...) Code elements (Mnemonics, labels,...) are always uppdercased!
        /// </summary>
        public bool FormatUpperCase { get; set; } = true;

        public bool NewPageOnHeadlineChange { get; set; } = true;

        public string PageNumberFormat { get; set; } = "Page {0:000}";

        public PageHeaderFormat PageHeader { get; set; } = PageHeaderFormat.HPDefault;
        /// <summary>
        /// Number of lines between header and code. Only used when there IS a header! Will also be used when the header changes as a prefix spacing.
        /// </summary>
        public int HeaderSpacing { get; set; } = 2;


        public bool IncludeLineNumbers { get; set; } = true;

        public bool IncludeAddress { get; set; } = true;

        public bool IncludeValues { get; set; } = true;

        public string FormatWord(int? word, bool is15BitAddress = false, bool includeTypeCharacter = false)
        {
            if (!word.HasValue || word < 0 || word > 0xFFFF)
                return new string(' ', WordWidth(is15BitAddress));

            switch (NumberFormat)
            {
                case NumberFormatType.Octal:
                    var temp = ("000000" + Convert.ToString(word.Value, 8));
                    return temp.Substring(temp.Length - (is15BitAddress ? 5 : 6)) + (includeTypeCharacter ? "B": string.Empty);
                case NumberFormatType.Hex:
                    return word.Value.ToString("x4") + (includeTypeCharacter ? "H": string.Empty);
                case NumberFormatType.Decimal:
                    return word.Value.ToString("00000");
                case NumberFormatType.Binary:
                    temp = ("0000000000000000" + Convert.ToString(word.Value, 2));
                    return temp.Substring(temp.Length - (is15BitAddress ? 15 : 16)) + (includeTypeCharacter ? "N": string.Empty);
            }
            throw new System.InvalidOperationException("Unknown number format?!");
        }

        public int WordWidth(bool is15BitAddress)
        {
            switch (NumberFormat)
            {
                case NumberFormatType.Octal:
                    return is15BitAddress ? 5 : 6;
                case NumberFormatType.Hex:
                    return 4;
                case NumberFormatType.Decimal:
                    return 5;
                case NumberFormatType.Binary:
                    return is15BitAddress ? 15 : 16;
            }
            throw new System.InvalidOperationException("Unknown number format?!");
        }
    }
}