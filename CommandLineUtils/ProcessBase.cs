using System;
using System.Threading.Tasks;

namespace CommandLineUtils
{
    /// <summary>
    /// Base class for "hosted processes" - i.e. multiplexed binaries. If the CLI application supports more than one "command" like "app set ..." and "app get ...", each of these commands gets implemented via one derived class.
    /// </summary>
    public abstract class ProcessBase
    {
        /// <summary>
        /// When created by the infrastructure, this property reflects the <see cref="MultiCommandHost.CommandName"/> of the 
        /// hosting object.
        /// </summary>
        public string CommandName { get; internal set; } = string.Empty;
        /// <summary>
        /// When created by the infrastructure, this property reflects the command name that was used to initialize the implementation 
        /// during the call. i.e. the first command line argument, lower cased for ignore case implementations.  You can use this to map different 
        /// commands (list, listall, ...) to the same implementing class. NOTE: will be set to the 
        /// one that was used by the caller, so could be long or short name including a prefix!
        /// </summary>
        public string ProcessName { get; internal set; } = string.Empty;
        /// <summary>
        /// When created by the infrastructure, this property reflects the <see cref="MultiCommandHost.IgnoreCase"/> of the hosting object.
        /// </summary>
        public bool IgnoreCase { get; internal set; }

        /// <summary>
        /// Append all requested command line parsing classes; See <see cref="ParameterHandler"/> for details.
        /// </summary>
        /// <param name="builder">The parameter builder to use for registration of the parameter parsing targets.</param>
        /// <remarks>
        /// <para>Use and store the returned parser object for reference; either in a property or in a field. It's used in the <see cref="RunNow"/> call.</para>
        /// </remarks>
        protected abstract void BuildArguments(ParameterHandler builder);

        /// <summary>
        /// Run the operation, according to the parsed arguments.
        /// </summary>
        protected abstract Task RunNow();

        /// <summary>
        /// Runs the command line parsing and backing command, according to the specs.
        /// </summary>
        /// <param name="args">The command line arguments for the command.</param>
        public async Task Run(string[] args)
        {
            if (_Parameters == null)
                throw new InvalidOperationException("Call sequence error; need to 'prepare' first!");
            await _Parameters.ParseFrom(args);
            if (_Parameters.HelpRequested)
                throw ReturnCode.ParseError.Happened(string.Format("Use '{0} help {1}' for help on {0}!", CommandName, ProcessName));
            if (!_Parameters.ParsedOk)  // the boolean option is to have the capability to "add more parameters" via multiple parsefrom calls...
                throw ReturnCode.ParseError.Happened("The parameters were valid on their own, but didn't yield a valid combination!");
            await RunNow();
        }

        private ParameterHandler? _Parameters;
        private ReturnCodeHandler? _ReturnCodes;

        /// <summary>
        /// Call this to bootstrap the object. Initialized commands and return codes.
        /// </summary>
        public void Prepare()
        {
            _Parameters = new ParameterHandler(CommandName, IgnoreCase);
            BuildArguments(_Parameters);
            var rc = new ReturnCodeHandler(true);
            if(BuildReturnCodes(rc))
                _ReturnCodes = rc;
        }

        /// <summary>
        /// Override in a derived class to provide individual return codes.
        /// </summary>
        /// <param name="reg">The return code registration.</param>
        /// <returns>True to use return codes, false to forgoe the return code management.</returns>
        protected virtual bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            return true;
        }

        /// <summary>
        /// Creates the parameter help output.
        /// </summary>
        /// <param name="width">Formatting width.</param>
        /// <returns>The formatted string, or null if there are no parameters defined.</returns>
        public string? GetParameterHelp(int width = 80)
        {
            if (_Parameters != null)
            {
                _Parameters.HelpRequested = true;
                return _Parameters.GetHelpText(width, ProcessName);
            }
            return null;
        }

        /// <summary>
        /// Creates the return code listing for a process.
        /// </summary>
        /// <param name="width">Formatting width.</param>
        /// <returns>The formatted string, or null if there are no return codes defined.</returns>
        public string? GetReturnCodeHelp(int width = 80)
        {
            return _ReturnCodes?.GetHelpText(width);
        }

        /// <summary>
        /// Get the extended help message for a specific command.
        /// </summary>
        /// <param name="page">If the user requested help via 'help command xyz' this will be the 'xyz' parameter. Null if ther was no additional parameter.</param>
        /// <returns>Null for "nu help" or a multi-line formatted plain text string to show.</returns>
        public virtual string? GetExtendedHelp(string? page)
        {
            return null;
        }
    }
}