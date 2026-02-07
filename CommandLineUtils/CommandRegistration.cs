using System;

namespace CommandLineUtils
{
    internal class CommandRegistration
    {
        public string Prefix { get; set; }
        public string? ShortName { get; set; }
        public string LongName { get; set; }
        public string? HelpMessage { get; set; }
        public Type Implementation { get; set; }

        public ProcessBase CreateImplementation(string cmdName, bool ignoreCase, string procName, bool supportsHelp)
        {
            ProcessBase? p = Activator.CreateInstance(Implementation) as ProcessBase;
            if (p == null)
                throw new InvalidOperationException($"Cannot create/cast {Implementation.FullName} to ProcessBase?!");
            p.CommandName = cmdName;
            p.IgnoreCase = ignoreCase;
            p.ProcessName = procName;
            p.Prepare(supportsHelp);
            return p;
        }
    }
}