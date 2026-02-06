using System;
using System.Threading.Tasks;

namespace CommandLineUtils
{
    public abstract class ProcessBase
    {
        /// <summary>
        /// When created by the infrastructure, this property reflects the <see cref="MultiCommandHost.CommandName"/> of the hosting object.
        /// </summary>
        public string CommandName { get; internal set; }
        /// <summary>
        /// When created by the infrastructure, this property reflects the command name that was used to initialize the implementation during the call. i.e. the first command line argument, lower cased for ignore case implementations.
        /// </summary>
        public string ProcessName { get; internal set; }
        /// <summary>
        /// When created by the infrastructure, this property reflects the <see cref="MultiCommandHost.IgnoreCase"/> of the hosting object.
        /// </summary>
        public bool IgnoreCase { get; internal set; }

        /// <summary>
        /// Append all defined. 
        /// </summary>
        /// <param name="builder"></param>
        protected abstract void BuildArguments(ParameterBuilder builder);

        /// <summary>
        /// Run the operation, according to the parsed arguments.
        /// </summary>
        /// <returns>The exist code for the main process.</returns>
        protected abstract Task<int> RunNow();

        /// <summary>
        /// 
        /// </summary>
        /// <param name=""></param>
        public async Task<int> Run(string[] args)
        {
            if (_Parameters == null)
                throw new InvalidOperationException("Call sequence error; need to 'prepare' first!");
            await _Parameters.ParseFrom(args);
            if (_Parameters.HelpRequested)
            {
                Console.WriteLine("Use {0} help {1} for help!", CommandName, ProcessName);
                return -5;
            }
            if (_Parameters.ParsedOk)
            {
                return await RunNow();
            }
            return -6;
        }

        private ParameterBuilder _Parameters;

        public void Prepare()
        {
            _Parameters = new ParameterBuilder(CommandName, IgnoreCase);
            BuildArguments(_Parameters);
        }

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