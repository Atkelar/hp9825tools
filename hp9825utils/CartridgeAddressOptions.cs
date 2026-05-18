using System;
using CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Diagnostics;

namespace HP9825Utils
{
    internal class CartridgeAddressOptions
    {
        internal void Validate(bool allowBothTracks, bool allowRange, ReturnCodeGroup<CartridgeJobCodes> results)
        {
            Track = (Track ?? "A").Trim().ToUpperInvariant();
            if (!allowBothTracks && Track == "AB")
                throw results.Happened(CartridgeJobCodes.InvalidAddressing, "AB", "Only one track allowed here; either A or B, not both!");
            if (!allowRange && EndIndex.HasValue)
                throw results.Happened(CartridgeJobCodes.InvalidAddressing, EndIndex, "This command doesn't allow an end file index!");
            if (EndIndex.HasValue)
            {
                if (EndIndex.Value < StartIndex)
                   throw results.Happened(CartridgeJobCodes.InvalidAddressing, EndIndex, "The end index is less than the start index!");
            }
        }



        internal int[] GetTracks()
        {
            switch(Track)
            {
                case "A":
                    return new int[] {0};
                case "B":
                    return new int[] {1};
                case "AB":
                    return new int[] {0, 1};
                default:
                    throw new NotImplementedException();
            }
        }

        [Argument("s", "Start", HelpText = "The first file index to operate on. 0-based.", DefaultValue = "0")]
        [Range(0, 32767, ErrorMessage = "The range of valid file numbers is 0 to 32767 only!")]
        public int StartIndex {get; set; } = 0;

        [Argument("e", "End", HelpText = "The last file index to operate on. 0-based.")]
        [Range(0, 32767, ErrorMessage = "The range of valid file numbers is 0 to 32767 only!")]
        public int? EndIndex {get; set; } = null;


        [StringLength(2, MinimumLength = 1, ErrorMessage = "Only between 1 and 2 characters allowed!")]
        [RegularExpression(@"^([aA]|[bB]|[aA][bB])$", ErrorMessage = "The format is either A or B.")]
        [Argument("t", "Track", HelpText = "The track to operate on, as A or B. Note that some commands support 'both' or 'any', which is represented as 'AB' here. Otherwise, either A or B has to be specified.", DefaultValue = "A")]
        public string Track { get; set; } = "A";
    }
}
