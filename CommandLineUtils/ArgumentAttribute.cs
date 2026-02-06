using System;
using System.Collections;
using System.Text.Json.Serialization.Metadata;

namespace CommandLineUtils
{

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ArgumentAttribute
        : Attribute
    {
        public ArgumentAttribute(string longArg)
        {
            LongName = longArg;
        }

        public ArgumentAttribute(string shortArg, string longArg)
        {
            ShortName = shortArg;
            LongName = longArg;
        }

        public int Positional { get; set; }
        public string? ShortName { get; private set; }
        public string LongName { get; private set; }
        public string? HelpText { get; set; }
        public string? DefaultValue { get; set; }
        public bool Required { get; set; }
    }
}