using System;

namespace CommandLineUtils
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class ProcessAttribute
        : Attribute
    {
        public ProcessAttribute(string shortName, string longName)
        {
            ShortName = shortName;
            LongName = longName;
        }

        public ProcessAttribute(string longName)
        {
            LongName = longName;
        }

        public string LongName { get; private set; }
        public string? ShortName { get; private set; }
        public string? HelpMessage { get; set; }
    }
}