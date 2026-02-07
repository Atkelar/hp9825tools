using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CommandLineUtils
{
    public class MultiCommandHost
    {
        public MultiCommandHost(string commandName, bool ignoreCase = true)
        {
            CommandName = commandName;
            IgnoreCase = ignoreCase;
        }

        public string CommandName { get; private set; }
        public bool IgnoreCase { get; private set; }

        /// <summary>
        /// Registers a type for a sub-command. The prefix will be added to the definition if provided.
        /// </summary>
        /// <typeparam name="T">The implementation to register.</typeparam>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public MultiCommandHost Register<T>(string? prefix = null)
            where T : ProcessBase, new()
        {
            var t = typeof(T);
            var attr = t.GetCustomAttributes(typeof(ProcessAttribute), true);
            if (attr.Length == 0)
                throw new InvalidOperationException($"The type {t.FullName} is missing the Process attribute!");
            foreach (ProcessAttribute a in attr)
            {
                string longName = prefix == null ? a.LongName : prefix + ":" + a.LongName;
                string? shortName = null;
                if (a.ShortName != null)
                    shortName = prefix == null ? a.ShortName : prefix + ":" + a.ShortName;

                var reg = new CommandRegistration()
                {
                    Prefix = prefix ?? string.Empty,
                    ShortName = shortName,
                    LongName = longName,
                    HelpMessage = a.HelpMessage,
                    Implementation = t
                };

                if (IgnoreCase)
                {
                    longName = longName.ToLowerInvariant();
                    shortName = shortName?.ToLowerInvariant();
                }

                if (LongNames.ContainsKey(longName))
                    throw new InvalidOperationException($"The command long-name {longName} already exists!");
                if (shortName != null && ShortNames.ContainsKey(shortName))
                    throw new InvalidOperationException($"The command short-name {shortName} already exists!");

                All.Add(reg);
                LongNames.Add(longName, reg);
                if (shortName != null)
                    ShortNames.Add(shortName, reg);
            }
            return this;
        }

        private class CommandRegistration
        {
            public string Prefix { get; set; }
            public string? ShortName { get; set; }
            public string LongName { get; set; }
            public string? HelpMessage { get; set; }
            public Type Implementation { get; set; }

            public ProcessBase CreateImplementation(string cmdName, bool ignoreCase, string procName)
            {
                ProcessBase? p = Activator.CreateInstance(Implementation) as ProcessBase;
                if (p == null)
                    throw new InvalidOperationException($"Cannot create/cast {Implementation.FullName} to ProcessBase?!");
                p.CommandName = cmdName;
                p.IgnoreCase = ignoreCase;
                p.ProcessName = procName;
                p.Prepare();
                return p;
            }
        }

        private List<CommandRegistration> All = new List<CommandRegistration>();
        private Dictionary<string, CommandRegistration> LongNames = new Dictionary<string, CommandRegistration>();
        private Dictionary<string, CommandRegistration> ShortNames = new Dictionary<string, CommandRegistration>();

        private void WriteHelpOverview(string topicMesage, bool longListing = false)
        {
            Console.WriteLine("{1} for {0}", CommandName, topicMesage);
            Console.WriteLine();
            Console.WriteLine("  The following commands are available, use 'help' as a command to get a more detailed list, or 'help <commandname>' for specific details.");
            Console.WriteLine();
            bool first = true;
            foreach (var item in All.OrderBy(x => x.Prefix).ThenBy(x => x.LongName))
            {
                if (!first && !longListing)
                    Console.Write(',');
                first = false;
                Console.Write(' ');
                if (item.Prefix.Length>0)
                    Console.Write("{0}:", item.Prefix);
                Console.Write("{0}", item.LongName);
                if (item.ShortName != null)
                {
                    if (longListing)
                    {
                        Console.Write("  alias: ");
                        if (item.Prefix.Length > 0)
                            Console.Write("{0}:", item.Prefix);
                        Console.Write("{0}", item.ShortName);
                    }
                    else
                    {
                        Console.Write(" (");
                        if (item.Prefix.Length > 0)
                            Console.Write("{0}:", item.Prefix);
                        Console.Write("{0}", item.ShortName);
                        Console.Write(')');
                    }
                }
                if (longListing)
                {
                    Console.WriteLine();
                    if (item.HelpMessage != null)
                    {
                        Console.Write("   ");
                        Console.WriteLine(item.HelpMessage);
                        Console.WriteLine();
                    }
                }
            }
            if (!longListing)
                Console.WriteLine();
        }

        private int ShowDetailedHelpFor(string cmd, string? page = null)
        {
            if (!LongNames.TryGetValue(cmd, out var selected))
            {
                if (!ShortNames.TryGetValue(cmd, out selected))
                {
                    WriteHelpOverview($"Command '{cmd}' was not found");
                    return -2;
                }
            }
            Console.WriteLine("Help for {0}:", selected.LongName);
            Console.WriteLine();
            if (selected.HelpMessage != null)
            {
                Console.Write(" Summary: ");
                Console.WriteLine(selected.HelpMessage);
                Console.WriteLine();
            }
            ProcessBase p = selected.CreateImplementation(CommandName, IgnoreCase, cmd);
            var details = p.GetExtendedHelp(page);
            if (details != null)
            {
                Console.WriteLine(details);
                Console.WriteLine();
            }
            details = p.GetParameterHelp(80);
            if (details != null)
            {
                Console.WriteLine(details);
                Console.WriteLine();
            }
            details = p.GetReturnCodeHelp(80);
            if (details != null)
            {
                Console.WriteLine("The process will return the following exit codes:");
                Console.WriteLine();
                Console.WriteLine(details);
                Console.WriteLine();
            }
            return 0;
        }

        /// <summary>
        /// Runs the program, including any pre-defined parameter parsing.
        /// </summary>
        /// <param name="args">The command line args, as recieved by the hosting application.</param>
        /// <returns>The exit code for the operating system.</returns>
        public async Task<int> Run(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    throw ReturnCode.ParseError.Happened("<value not provided>", "Command is missing!");
                }
                else
                {
                    var cmd = args[0].Trim();
                    if (IgnoreCase)
                        cmd = cmd.ToLowerInvariant();
                    if (cmd == "help")  // predefined...
                    {
                        if (args.Length > 1)
                        {
                            cmd = args[1].Trim();
                            if (IgnoreCase)
                                cmd = cmd.ToLowerInvariant();
                            // help cmd
                            return ShowDetailedHelpFor(cmd, args.Length > 2 ? args[2] : null);
                        }
                        else
                        {
                            WriteHelpOverview("Command Listing", true);
                        }
                    }
                    else
                    {
                        if (!LongNames.TryGetValue(cmd, out var selected))
                        {
                            if (!ShortNames.TryGetValue(cmd, out selected))
                            {
                                throw ReturnCode.ParseError.Happened($"Command '{cmd}' was not found");
                            }
                        }
                        // got a command now...
                        var thisCommand = selected.CreateImplementation(CommandName, IgnoreCase, cmd);
                        string[] newArgs = new string[args.Length - 1];
                        Array.Copy(args, 1, newArgs, 0, newArgs.Length);
                        await thisCommand.Run(newArgs);
                    }
                }
            }
            catch (ReturnCodeException ex)
            {
                if (ex.IsNonError)
                    Console.WriteLine(ex.Message);
                if (!ex.IsNonError)
                    Console.WriteLine(ex.Message);
                return ex.Code;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ReturnCode.UhandledError.ErrorMessageTemplate, ex.Message);
                return ReturnCode.UhandledError.Code;
            }
            return ReturnCode.Success.Code; 
        }
    }
}