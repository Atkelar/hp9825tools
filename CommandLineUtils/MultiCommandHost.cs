using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandLineUtils
{
    /// <summary>
    /// Hosts a multiplexed command, i.e. a whole list of commands to be selected via the first command line argument.
    /// </summary>
    public class MultiCommandHost
        : IDisposable
    {
        /// <summary>
        /// Initializes the host object.
        /// </summary>
        /// <param name="commandName">The command name - used for call syntax display.</param>
        /// <param name="ignoreCase">true if the command line parser will ignore upper/lower case differences.</param>
        public MultiCommandHost(string commandName, bool ignoreCase = true)
        {
            CommandName = commandName;
            IgnoreCase = ignoreCase;
        }

        /// <summary>
        /// The command name - used for call syntax display.
        /// </summary>
        public string CommandName { get; private set; }
        
        /// <summary>
        /// true if the command line parser will ignore upper/lower case differences.
        /// </summary>
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

        private void WriteDetailedHelpFor(string cmd, string? page = null)
        {
            if (!LongNames.TryGetValue(cmd, out var selected))
            {
                if (!ShortNames.TryGetValue(cmd, out selected))
                {
                    throw ReturnCode.ParseError.Happened("command selection", $"Command '{cmd}' was not found!");
                }
            }
            if (Output==null)
                return;
            Output.WriteLine( VerbosityLevel.Normal, SplitMode.Word, "Help for {0}:", selected.LongName);
            Output.WriteLine( VerbosityLevel.Normal);
            if (selected.HelpMessage != null)
            {
                Output.Write( VerbosityLevel.Normal, SplitMode.Word, " Summary: ");
                Output.Write( VerbosityLevel.Normal, SplitMode.Word, selected.HelpMessage);
                Output.WriteLine( VerbosityLevel.Normal);
            }
            ProcessBase p = selected.CreateImplementation(CommandName, IgnoreCase, cmd, false);
            p.WriteExtendedHelp(Output, page);
            p.WriteHelpText(Output);
            p.WriteReturnCodeHelp(Output);
        }

  /// <summary>
        /// Sets up the banner message for the program.
        /// </summary>
        /// <typeparam name="T">Base type for the assembly version; will be used to pull the version number.</typeparam>
        /// <param name="appName">Application main name.</param>
        /// <param name="copyrightBy">If provided, a "Copyright by" will be added.</param>
        /// <param name="copyrightFromYear">If provided, a x-y part will be added to the copyright message.</param>
        /// <param name="copyrightToYear">If provided, the copyright to year will be fixed to this one, otherwise the "today" year will be used. Either will only show if larger than the from year.</param>
        public void SetupBanner<T>(string appName, string? copyrightBy, int? copyrightFromYear = null, int? copyrightToYear = null)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(appName);

            var t = typeof(T);
            var version = t.Assembly.GetName().Version;

            sb.Append(' ');
            sb.Append(version);

            if (copyrightBy != null)
            {
                sb.Append(" Copyright (c) ");
                if (copyrightFromYear.HasValue)
                {
                    sb.Append(copyrightFromYear);
                    var yt = copyrightToYear.GetValueOrDefault(DateTime.Today.Year);
                    if(yt > copyrightFromYear.Value)
                    {
                        sb.Append(" - ");
                        sb.Append(yt);
                    }
                    sb.Append(' ');
                }
                sb.Append("by ");
                sb.Append(copyrightBy);
            }

            BannerMessage = sb.ToString();
        }

        private string? BannerMessage = null;

        public OutputHandlerBase? Output {get;private set;}

        /// <summary>
        /// Provide an output handler implementation. If not specified, the normal console will be used.
        /// </summary>
        /// <param name="handler">The implementation.</param>
        public void OutputTo(OutputHandlerBase handler)
        {
            if(object.ReferenceEquals(Output, handler))
                return;

            var x = Output;
            if (Output != null)
            {
                Output.WriteLine(VerbosityLevel.Trace, "switching output handler...");
            }
            Output = handler;
            x?.Dispose();
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
                        if (Output == null)
                        {
                            var x = new ConsoleBasedOutput();
                            x.Prepare(VerbosityLevel.Normal);
                            this.OutputTo(x);
                        }

                        if (args.Length > 1)
                        {
                            cmd = args[1].Trim();
                            if (IgnoreCase)
                                cmd = cmd.ToLowerInvariant();
                            // help cmd
                            WriteDetailedHelpFor(cmd, args.Length > 2 ? args[2] : null);
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
                        var thisCommand = selected.CreateImplementation(CommandName, IgnoreCase, cmd, false);
                        string[] newArgs = new string[args.Length - 1];
                        Array.Copy(args, 1, newArgs, 0, newArgs.Length);
                        if (await thisCommand.Parse(newArgs) != null)
                            throw ReturnCode.ParseError.Happened($"Command '{cmd}' requsted help. Use global help command instead!");
                        
                        if (Output == null)
                        {
                            var x = new ConsoleBasedOutput();
                            x.Prepare(VerbosityLevel.Normal);
                            this.OutputTo(x);
                        }

                        thisCommand.SetOutput(Output!);
                        if (BannerMessage != null)
                        {
                            Output?.WriteLine(VerbosityLevel.Normal, BannerMessage);
                        }
                        await thisCommand.Run();
                    }
                }
            }
            catch (ReturnCodeException ex)
            {
                if (Output == null)
                {
                    var x = new ConsoleBasedOutput();
                    x.Prepare(VerbosityLevel.Normal);
                    this.OutputTo(x);
                }
                Output?.WriteLine(ex.IsNonError ?  VerbosityLevel.Normal : VerbosityLevel.Errors, SplitMode.Word, ex.Message);
                return ex.Code;
            }
            catch(Exception ex)
            {
                if (Output == null)
                {
                    var x = new ConsoleBasedOutput();
                    x.Prepare(VerbosityLevel.Normal);
                    this.OutputTo(x);
                }
                Output?.WriteLine(VerbosityLevel.Errors, SplitMode.Word, ReturnCode.UhandledError.ErrorMessageTemplate, ex.Message);
                return ReturnCode.UhandledError.Code;
            }
            return ReturnCode.Success.Code; 
        }
        private bool IsDisposed = false;

        /// <summary>
        /// Throws the <see cref="ObjectDisposedException"/> if the object is disposed, does nothing if not.
        /// </summary>
        protected void DemandNotDisposed()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
        }

        /// <summary>
        /// Override to handle disposable objects.
        /// </summary>
        /// <param name="isDisposing">True if the call came from the explicit dispose, false if it came from a destructor.</param>
        protected virtual void Dispose(bool isDisposing)
        {
        }
        /// <summary>
        /// Implements <see cref="IDisposable"/> 
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                Dispose(true);
            }
            IsDisposed = true;
        }
   }
}