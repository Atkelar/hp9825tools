using System;

namespace HP9825CPU
{
    [Flags()]
    public enum PageHeaderFormat
    {
        /// <summary>
        /// No page header.
        /// </summary>
        None = 0,
        /// <summary>
        /// The headline, as defind in the source code.
        /// </summary>
        Headline = 1,
        /// <summary>
        /// Include a page number in the upper right: "Page #"
        /// </summary>
        PageNumber = 2,
        /// <summary>
        /// Include the name of the source file to the left "filename.asm"
        /// </summary>
        Filename = 4,

        /// <summary>
        /// Include the headline (only!) inside a running page if it chages;
        /// </summary>
        HeadlineInPage = 8,

        // SpacingOptionMask = 0x30,
        // SpacingOne = 0x10,
        // SpacingTwo = 0x20,
        // SpacingThree = 0x30,


        /// <summary>
        /// The default as defined in the original ASM.
        /// </summary>
        HPDefault = Headline | HeadlineInPage

    }
}