using System;
using System.Collections;
using System.Text.Json.Serialization.Metadata;

namespace CommandLineUtils
{
    /// <summary>
    /// Marks a property in a command line app as a command line argument.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ArgumentAttribute
        : Attribute
    {
        /// <summary>
        /// Base argument defintion.
        /// </summary>
        /// <param name="longArg">The long name for the argumant. Short name will not be available.</param>
        public ArgumentAttribute(string longArg)
        {
            LongName = longArg;
        }

        /// <summary>
        /// Base argument definition with shorthand.
        /// </summary>
        /// <param name="shortArg">The short name of the argument. Used for "-" parsing.</param>
        /// <param name="longArg">The long name of the argument. Used for "--" parsing.</param>
        public ArgumentAttribute(string shortArg, string longArg)
        {
            ShortName = shortArg;
            LongName = longArg;
        }

        /// <summary>
        /// If set, the argument will be avaliable without the name, i.e. as positional argument. The number will sort them in ascending order.
        /// </summary>
        public int Positional { get; set; }
        /// <summary>
        /// The short name of the argument. Null if not available.
        /// </summary>
        public string? ShortName { get; private set; }
        /// <summary>
        /// The long name of the argument.
        /// </summary>
        public string LongName { get; private set; }
        /// <summary>
        /// A help text for the argument. Including detailed description.
        /// </summary>
        public string? HelpText { get; set; }
        /// <summary>
        /// The default value of the argument. Note: this will only be displayed and can contain human readable wording.
        /// </summary>
        public string? DefaultValue { get; set; }
        /// <summary>
        /// True to make this a mandatory argument.
        /// </summary>
        public bool Required { get; set; }
        /// <summary>
        /// True to allow "read from file" option for this argument. The syntax is @(filname) - to use @ as the starting character instead, double it!
        /// </summary>
        public bool AllowFileIngest { get; set; }

        /// <summary>
        /// A syntax description for help text creation. Use the backtick to separate multiple sample values. The list is trimmed and empty ones are discarded.
        /// </summary>
        public string? Syntax { get; set; } = null;

        /// <summary>
        /// Set to true to make boolean properties a normal +/- argument instead of a switch.
        /// </summary>
        public bool NoSwitch { get; set; }
    }
}