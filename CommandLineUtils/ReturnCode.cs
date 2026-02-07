using System;
namespace CommandLineUtils
{
    /// <summary>
    /// Encapsulates a single return code. Obtained from <see cref="ReturnCodeGroup{T}.For(T)"/> 
    /// </summary>
    public class ReturnCode
    {
        internal ReturnCode(int code, bool nonError, string errorTemplate, string? helpMessage)
        {
            HelpMessage = helpMessage;
            ErrorMessageTemplate = errorTemplate;
            IsNonError = nonError;
            Code = code;
        }

        /// <summary>
        /// A wrapper for "0" success exit code.
        /// </summary>
        public static readonly ReturnCode Success = new ReturnCode(0, true, "No error.", "Program completed successfully.");
        /// <summary>
        /// A wrapper for "-1" exit code, which is used to indicate unhandled "other" errors.
        /// </summary>
        public static readonly ReturnCode UhandledError = new ReturnCode(-1, false, "There was an unhandled error in the program: {0}", "Indicates an unhandled runtime error.");
        /// <summary>
        /// A wrapper for "-2" exit code, which is used by the command line parser to indicate syntax errors.
        /// </summary>
        public static readonly ReturnCode ParseError = new ReturnCode(-2, false, "Parsing error in argument: '{0}' {1}", "Indicates a syntax error in the command line parsing section. Check the arguments and try again!");
        /// <summary>
        /// A wrapper for "-3" exist code, which is used by the command line parser to indicate that a requested settings file was not found.
        /// </summary>
        public static readonly ReturnCode SettingsFileNotFound = new ReturnCode(-3, false, "Settings file '{0}' requested but not found!", "Indicates that the requested settings file via the command line was not found.");

        /// <summary>
        /// The code that goes with this result.
        /// </summary>
        public int Code { get; private set; }

        /// <summary>
        /// True if the exit condition is "early out" rather than an error.
        /// </summary>
        public bool IsNonError { get; private set; }

        /// <summary>
        /// The template for the error message.
        /// </summary>
        public string ErrorMessageTemplate {get; private set; }

        /// <summary>
        /// A help message for the exit condition.
        /// </summary>
        public string? HelpMessage { get; private set; }

        /// <summary>
        /// Throws the result code exception for the defined code.
        /// </summary>
        /// <param name="args">The parameters to add to the error message.</param>
        /// <exception cref="ReturnCodeException">At any case.</exception>
        public void Throw(params object?[] args)
        {
            throw Happened(args);
        }

        /// <summary>
        /// Returns a ready-to-go exception.
        /// </summary>
        /// <param name="args">The parameters to add to the error message.</param>
        /// <returns>The exception. Can be used to "thorw x.Happened(..)" to keep the compiler warnings at bay.</returns>
        public Exception Happened(params object?[] args)
        {
            string msg;
            try
            {
                msg = string.Format(ErrorMessageTemplate, args);
            }
            catch (FormatException)
            {
                // assume we had too little args and retry with plenty... but only once.
                // this is a fallback, since the message format string and the use of that string 
                // are rather disconnected from one another and might diverge over time.
                object?[] alt = new object[Math.Max(args.Length* 2, 128)];
                Array.Copy(args, 0, alt, 0, args.Length);
                for(int i = args.Length; i<alt.Length;i++)
                    alt[i]= $"<missing ph: {i}>";
                msg = string.Format(ErrorMessageTemplate, alt);
            }
            return new ReturnCodeException(Code, msg, IsNonError);
        }

    }
}