using HP9825CPU;
using CommandLineUtils;
using System.Collections;
using System;

namespace HP9825Assembler
{
    public class ListingFormatOptions
    {
        public ListingFormatOptions()
        {
            Options = new CodeFormatOptions();
        }
        public CodeFormatOptions Options { get; private set; }

        [Argument("mc", "MixedCase", HelpText = "If specified, will format comments/headlines with mixed casing. By default, all text is upper-cased.")]
        public bool MixedCase
        {
            get => !Options.FormatUpperCase;
            set
            {
                Options.FormatUpperCase = !value;
            }
        }

        [Argument("hs", "HeaderSpacing", HelpText = "Number of lines to pring after (or before) a source listing headline.", DefaultValue = "2")]
        public int HeaderSpacing
        {
            get { return Options.HeaderSpacing; }
            set { Options.HeaderSpacing = value; }
        }

        [Argument("w", "Width", HelpText = "The number of characters to use for the listing output formatting. Valid range is 40-1024.", DefaultValue = "80")]
        public int PageWidth { get; set; } = 80;

        [Argument("h", "Height", HelpText = "The number of lines to use for the listing output formatting. Valid range is 10-1024.", DefaultValue = "60")]
        public int PageHeight { get; set; } = 60;


        [Argument("nf", "NumberFormat", HelpText = "The style of numbers to use for the address and value fields. Can be either oct, dec, hex or bin.", DefaultValue = "oct")]
        public string NumberFormat
        {
            get { return Options.NumberFormat.ToString(); }
            set { Options.NumberFormat = value.ToLowerInvariant() switch { "bin" => NumberFormatType.Binary, "oct" => NumberFormatType.Octal, "hex" => NumberFormatType.Hex, "dec" => NumberFormatType.Decimal, _ => throw new ArgumentOutOfRangeException(nameof(NumberFormat), value, "invalid format!") }; }
        }

        [Argument("pnf", "PageNumberFormat", HelpText = "A .NET format string with 0-placeholder for creating the page numbers in the output.", DefaultValue = "Page {0:000}")]
        public string PageNumberFormat
        {
            get { return Options.PageNumberFormat; }
            set { Options.PageNumberFormat = value; }
        }

        [Argument("hf", "HeaderFormat", HelpText = "The style of the page header. Use either 'n' for none, 'hp' for HP defaults, 'a' for all, or a combination of the following letters: l=headline, #=page number, f=filename, i=include headline in page when it is changed.", DefaultValue = "hp")]
        public string x
        {
            get { return string.Empty; }
            set
            {
                var str = value.ToLowerInvariant();
                switch (str)
                {
                    case "a":
                        Options.PageHeader = PageHeaderFormat.PageNumber | PageHeaderFormat.Headline | PageHeaderFormat.Filename | PageHeaderFormat.HeadlineInPage;
                        return;
                    case "n":
                        Options.PageHeader = PageHeaderFormat.None;
                        return;
                    case "hp":
                        Options.PageHeader = PageHeaderFormat.HPDefault;
                        return;
                }
                Options.PageHeader = PageHeaderFormat.None;
                if (str.IndexOf('l') >= 0)
                    Options.PageHeader |= PageHeaderFormat.Headline;
                if (str.IndexOf('#') >= 0)
                    Options.PageHeader |= PageHeaderFormat.PageNumber;
                if (str.IndexOf('f') >= 0)
                    Options.PageHeader |= PageHeaderFormat.Filename;
                if (str.IndexOf('i') >= 0)
                    Options.PageHeader |= PageHeaderFormat.HeadlineInPage;
            }
        }

        // [Argument("x", "x", HelpText = "", DefaultValue = "")]
        // public int x
        // {
        //     get { return Options.x; }
        //     set { Options.c = value; }
        // }

    }
}