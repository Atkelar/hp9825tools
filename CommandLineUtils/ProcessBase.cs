using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CommandLineUtils
{
    /// <summary>
    /// Base class for "hosted processes" - i.e. multiplexed binaries. If the CLI application supports more than one "command" like "app set ..." and "app get ...", each of these commands gets implemented via one derived class.
    /// </summary>
    public abstract class ProcessBase
        : IDisposable
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
        /// Initializes the base core properties.
        /// </summary>
        protected ProcessBase()
        {
            Out  = new ConditionalOutputImpl(this, VerbosityLevel.Normal);
            Error = new ConditionalOutputImpl(this, VerbosityLevel.Error);
            Warning  = new ConditionalOutputImpl(this, VerbosityLevel.Warning);
            Verbose  = new ConditionalOutputImpl(this, VerbosityLevel.Verbose);
            Trace  = new ConditionalOutputImpl(this, VerbosityLevel.Trace);
        }

        public void WriteLine()
        {
            this.WriteLine(null);
        }

        public void WriteLine(string? message, params object?[] args)
        {
            if(message == null)
                Out.WriteLine(SplitMode.None, null);
            else
                Out.WriteLine(SplitMode.None, string.Format(message, args));
        }
        public void Write(string? message, params object?[] args)
        {
            if(message == null)
                Out.WriteLine(SplitMode.None, null);
            else
                Out.Write(SplitMode.None, string.Format(message, args));
        }

        public void WriteLine(string? message)
        {
            Out.WriteLine(SplitMode.None, message);
        }
        public void Write(string? message)
        {
            Out.WriteLine(SplitMode.None, message);
        }

        public void WriteLine(SplitMode split, string? message, params object?[] args)
        {
            if(message == null)
                Out.WriteLine(split, null);
            else
                Out.WriteLine(split, string.Format(message, args));
        }
        public void Write(SplitMode split, string? message, params object?[] args)
        {
            if(message == null)
                Out.Write(split, null);
            else
                Out.Write(split, string.Format(message, args));
        }

        public void WriteLine(SplitMode split, string? message)
        {
            Out.WriteLine(split, message);
        }
        public void Write(SplitMode split, string? message)
        {
            Out.Write(split, message);
        }

        public void WriteLine(VerbosityLevel level, SplitMode split, string? message)
        {
            if (Output != null)
                Output.WriteLine(level, split, message);
            else
            {
                switch (level)
                {
                    case VerbosityLevel.Error:
                        Console.Error.WriteLine(message);
                        break;
                    default:
                        Console.Out.WriteLine(message);
                        break;
                }
            }
        }

        public void Write(VerbosityLevel level, SplitMode split, string message)
        {
            if (Output != null)
                Output.Write(level, split, message);
            else
            {
                switch (level)
                {
                    case VerbosityLevel.Error:
                        Console.Error.Write(message);
                        break;
                    default:
                        Console.Out.Write(message);
                        break;
                }
            }
        }

        internal void ChangeOutput(OutputHandlerBase output)
        {
            var now = Output;
            Output = output;
            if (now is IDisposable disp)
            {   
                disp.Dispose();
            }
            Output.PostInit();
        }

        /// <summary>
        /// Access the output object for the running process.
        /// </summary>
        protected OutputHandlerBase Output {get;private set;} = NullOutput;

        /// <summary>
        /// Simplification wrapper for the "normal" output.
        /// </summary>
        protected IConditionalOutput Out {get;private set;}
        /// <summary>
        /// Simplification wrapper for the "error" output.
        /// </summary>
        protected IConditionalOutput Error {get;private set;} 
        /// <summary>
        /// Simplification wrapper for the "warning" output.
        /// </summary>
        protected IConditionalOutput Warning {get;private set;}
        /// <summary>
        /// Simplification wrapper for the "verbose" output.
        /// </summary>
        protected IConditionalOutput Verbose {get;private set;}
        /// <summary>
        /// Simplification wrapper for the "trace" output.
        /// </summary>
        protected IConditionalOutput Trace {get;private set;}
       
        /// <summary>
        /// Runs the command line parsing and backing command, according to the specs.
        /// </summary>
        public async Task Run()
        {
            if (_Parameters == null)
                throw new InvalidOperationException("Call sequence error; need to 'prepare' first!");
            if (!_Parameters.ParsedOk)  // the boolean option is to have the capability to "add more parameters" via multiple parsefrom calls...
                throw ReturnCode.ParseError.Happened("The parameters were valid on their own, but didn't yield a valid combination!");
            await RunNow();
        }

        /// <summary>
        /// Parse the command line args. If one of them was "help", the result is non-null; either the requested help page or an empty string.
        /// </summary>
        /// <param name="args">The command line args.</param>
        /// <returns>Null if the call can go ahead, non-null if help was requested.</returns>
        public async Task<string?> Parse(string[] args)
        {
            if (_Parameters == null)
                throw new InvalidOperationException("Call sequence error; need to 'prepare' first!");
            await _Parameters.ParseFrom(args);
            return _Parameters.HelpRequested;
        }

        public VerbosityLevel Verbosity => _Parameters?.Verbosity ?? VerbosityLevel.Normal;

        private ParameterHandler? _Parameters;
        private ReturnCodeHandler? _ReturnCodes;

        /// <summary>
        /// Call this to bootstrap the object. Initialized commands and return codes.
        /// </summary>
        public void Prepare(bool supportsHelp = true, IEnumerable<string>? additionalConfigFiles = null)
        {
            _Parameters = new ParameterHandler(CommandName, IgnoreCase);
            _Parameters.SupportsHelp = supportsHelp;
            if (additionalConfigFiles != null)
                foreach(var file in additionalConfigFiles)
                    _Parameters.AddOptionalDefault(file);
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
        /// <param name="output">The output target.</param>
        /// <returns>The formatted string, or null if there are no parameters defined.</returns>
        public void WriteHelpText(OutputHandlerBase? output)
        {
            output ??= Output;
            if (_Parameters != null && output != null)
            {
                _Parameters.HelpRequested = string.Empty;
                _Parameters.WriteHelpText(output, ProcessName);
            }
        }

        /// <summary>
        /// Creates the return code listing for a process.
        /// </summary>
        /// <param name="output">Target for writing.</param>
        public void WriteReturnCodeHelp(OutputHandlerBase output)
        {
            _ReturnCodes?.WriteHelpText(output);
        }

        /// <summary>
        /// Get the extended help message for a specific command.
        /// </summary>
        /// <param name="page">If the user requested help via 'help command xyz' this will be the 'xyz' parameter. Null if ther was no additional parameter.</param>
        /// <returns>Null for "nu help" or a multi-line formatted plain text string to show.</returns>
        public virtual void WriteExtendedHelp(OutputHandlerBase output, string? page)
        {
        }

        /// <summary>
        /// Throws the <see cref="ObjectDisposedException"/> if the object is disposed, does nothing if not.
        /// </summary>
        protected void DemandNotDisposed()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
        }

        private bool IsDisposed = false;

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
                Dispose();
            }
            IsDisposed = true;
        }

        /// <summary>
        /// Represents a "silent" (redirect to null) output device. The default <see cref="Output"/> property in the implementation!
        /// </summary>
        public static readonly OutputHandlerBase NullOutput = new NullOutputHandler();

        internal class NullOutputHandler
            : OutputHandlerBase
        {
            protected override DisplaySpec CreateFor(VerbosityLevel level)
            {
                return new NullDisplaySpec();
            }

            private class NullDisplaySpec
                : DisplaySpec
            {
                public override int? DisplayWidth => null;

                public override void WriteLine(string ouptut)
                {}
            }
        }

        private class ConditionalOutputImpl
            : IConditionalOutput
        {
            private readonly ProcessBase _Parent;
            private readonly VerbosityLevel _Level;

            public ConditionalOutputImpl(ProcessBase processBase, VerbosityLevel level)
            {
                _Parent = processBase;
                _Level = level;
            }

            public void EnsureNewLine(bool forceEmptyLine = false)
            {
                _Parent.Output.EnsureNewLine(_Level, forceEmptyLine);
            }

            public IDisposable Indent(int? numChars = null)
            {
                return _Parent.Output.Indent(_Level, numChars);
            }

            public IDisposable IndentFor(string header, int? numChars = null)
            {
                return _Parent.Output.IndentFor(_Level, header, numChars);
            }

            public ITableFormatter Table(Action<ITableBuilder> creator)
            {
                return _Parent.Output.Table(_Level, creator);
            }

            public void Write(SplitMode split, string? text)
            {
                _Parent.Output.Write(_Level, split, text);
            }

            public void Write(string? text)
            {
                _Parent.Output.Write(_Level, SplitMode.None, text);
            }

            public void WriteLine(SplitMode split, string? text)
            {
                _Parent.Output.WriteLine(_Level, split, text);
            }

            public void WriteLine(string? text)
            {
                _Parent.Output.WriteLine(_Level, SplitMode.None, text);
            }
        }
    }
}