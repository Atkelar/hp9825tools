using CommandLineUtils;
using System.ComponentModel.DataAnnotations;

namespace HP9825Utils
{
    public class MappingSettings
    {
        /// <summary>
        /// The size of the ROM banks
        /// </summary>
        [Argument("bs", "BankSize", HelpText = "Provides the size of the memory bank to use for banked addressing mode.", DefaultValue = "16384")]
        [Range(1024,10*1024*1024, ErrorMessage = "Banks smaller than 1024 or larger than 10M not supported.")] 
        public int BankSize { get; set; } = 16384;
        /// <summary>
        /// The size of the ROM banks
        /// </summary>
        [Argument("b", "Bank", HelpText = "Provides the index of the memory bank to write to. Zero based.", DefaultValue = "0")]
        [Range(0, 1024, ErrorMessage = "Bank index below zero or above 1024 not supported!")]
        public int Bank { get; set; } = 0;
    }
}