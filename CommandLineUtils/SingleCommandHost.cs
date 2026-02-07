using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CommandLineUtils
{
    /// <summary>
    /// Generic version of the single command host; auto-setup for T.
    /// </summary>
    /// <typeparam name="T">The process to implement.</typeparam>
    public class SingleCommandHost<T>
        : SingleCommandHost
        where T : ProcessBase, new()
    {
        /// <summary>
        /// Initializes the host object.
        /// </summary>
        /// <param name="commandName">The command name - used for call syntax display.</param>
        /// <param name="ignoreCase">true if the command line parser will ignore upper/lower case differences.</param>
        /// <param name="name">The name of the process to use (short or long) as a tie breaker for multiple process attributes.</param>
        public SingleCommandHost(string commandName, bool ignoreCase = true, string? name=null)
            : base(commandName, ignoreCase)
        {
            base.SetupFor<T>(name);
        }
    }

    /// <summary>
    /// Hosts a single command line process object. Can be provided after creating the object.
    /// </summary>
    public class SingleCommandHost
        : IDisposable
    {
        /// <summary>
        /// Initializes the host object.
        /// </summary>
        /// <param name="commandName">The command name - used for call syntax display.</param>
        /// <param name="ignoreCase">true if the command line parser will ignore upper/lower case differences.</param>
        public SingleCommandHost(string commandName, bool ignoreCase = true)
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
            if (isDisposing)
            {
                Output?.Dispose();
            }
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

        /// <summary>
        /// Configures this instance of the host object to handle a specific command.
        /// </summary>
        /// <typeparam name="T">The process implementation to use, if the source class has more than one attribute.</typeparam>
        /// <param name="name">The name of the process to use (short or long) as a tie breaker for multiple process attributes.</param>
        /// <exception cref="InvalidOperationException">Configuration is invalid in a way.</exception>
        public void SetupFor<T>(string? name = null)
            where T : ProcessBase, new()
        {
            var t = typeof(T);
            var attr = t.GetCustomAttributes(typeof(ProcessAttribute), true);
            if (attr.Length == 0)
                throw new InvalidOperationException($"The type {t.FullName} is missing the Process attribute!");
            ProcessAttribute? a;
            if(attr.Length>1)
            {
                if (name == null)
                    throw new InvalidOperationException($"The type {t.FullName} has multiple process attributes, but on 'name' parameter was provided!");
                a = attr.Cast<ProcessAttribute>().FirstOrDefault(x=>x.ShortName == name || x.LongName== name);
                if (a == null)  
                    throw new InvalidOperationException($"The type {t.FullName} has multiple process attributes, but on 'name' didn't match any!");
            }
            else
            {
                a = (ProcessAttribute)attr[0];
            }
            string longName = a.LongName;
            string? shortName = a.ShortName;

            var reg = new CommandRegistration()
            {
                Prefix = string.Empty,  // we don't use prefixes here.
                ShortName = shortName,
                LongName = longName,
                HelpMessage = a.HelpMessage,
                Implementation = t
            };

            _Command = reg;
        }

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

        OutputHandlerBase? Output;

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

        private int ShowDetailedHelpFor(ProcessBase impl, string? page = null)
        {
            if(Output == null || _Command == null)
                return 0;
            Output.WriteLine();
            if (_Command?.HelpMessage != null)
            {
                using(Output.IndentFor( VerbosityLevel.Normal, "Summary: "))
                {
                    Output.WriteLine(_Command.HelpMessage);
                    Output.WriteLine();
                }
            }
            impl.WriteExtendedHelp(Output, page);
            impl?.WriteHelpText(Output);
            impl?.WriteReturnCodeHelp(Output);
            return 0;
        }

        public async Task<int> Run(string[] args)
        {
            if (_Command == null)
                throw new InvalidOperationException("Use Setup first!");
            try
            {
                // parse args for verbosity control...
                var cmd = _Command.CreateImplementation(CommandName, IgnoreCase, string.Empty, true);
                cmd.Prepare(true);

                string? helpPage = await cmd.Parse(args);
                if (Output == null)
                {
                    var x = new ConsoleBasedOutput();
                    x.Prepare(VerbosityLevel.Normal);
                    this.OutputTo(x);
                }
                cmd.SetOutput(Output!);
                if (BannerMessage != null)
                {
                    Output?.WriteLine(VerbosityLevel.Normal, BannerMessage);
                }
                if (helpPage != null)
                {
                    ShowDetailedHelpFor(cmd, helpPage);
                    return ReturnCode.Success.Code;
                }
                await cmd.Run();
            }
            catch (ReturnCodeException ex)
            {
                Output?.WriteLine(ex.IsNonError ? VerbosityLevel.Normal : VerbosityLevel.Errors, ex.Message);
                return ex.Code;
            }
            catch(Exception ex)
            {
                Output?.WriteLine(ReturnCode.UhandledError.ErrorMessageTemplate, ex.Message);
                
#if DEBUG
                Console.WriteLine(ex);
#endif
                return ReturnCode.UhandledError.Code;
            }
            return ReturnCode.Success.Code; 
        }

        private string? BannerMessage = null;

        private CommandRegistration? _Command;
    }
}