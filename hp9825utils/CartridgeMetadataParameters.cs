using System;
using System.Globalization;
using CommandLineUtils;

namespace HP9825Utils
{
    public class CartridgeMetadataParameters
    {
        [Argument("lb", "Label", DefaultValue = "A created name including the current time/date.", HelpText = "Provide a readable name for the cartridge. Used in browsing and display sections. Maximum of 80 characters!")]
        public string? Label { get; set; }
        [Argument("len", "Length", DefaultValue = "1680", HelpText = "The length of the new tape in inches; default value is an estimate of the original tapes. An absolute minimum of 100 is required for the tapes to usefully function.")]
        public double Length { get; set; } = 1680;
        [Argument("com", "Comment", HelpText = "Provides a more complex comment to be added to the tape. The label is limited by design, this field can hold arbitrary information, such as copyright info, authorship details...", AllowFileIngest = true)]
        public string? Comment { get; set; }

        internal void Validate(ReturnCodeGroup<CartridgeJobCodes> results)
        {
            if (string.IsNullOrWhiteSpace(Comment)) // null for empty in all cases...
                Comment = null;
            Label ??= string.Format("unnamed-{0:yyyyMMdd-HHmmss}", DateTime.Now);
            if (Label.Length > 80)
                throw results.Happened(CartridgeJobCodes.InvalidMetadata, "Label",  Label, "Maximum of 80 characters, no special characters please!");
            if (Length < 100)
                throw results.Happened(CartridgeJobCodes.InvalidMetadata, "Length", Length, "The tape must be at least 100 inches long!");
        }
    }
}