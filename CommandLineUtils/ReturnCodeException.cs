using System;

namespace CommandLineUtils
{
    /// <summary>
    /// Tells the runtime infrastructure that an error(?) has occured and that the hosting process should terminate with the provided result code.
    /// </summary>
    public class ReturnCodeException
        : Exception
    {
        /// <summary>
        /// Creates a new exception....
        /// </summary>
        /// <param name="code">The result code for the OS.</param>
        /// <param name="message"></param>
        /// <param name="nonError"></param>
        public ReturnCodeException(int code, string message, bool nonError)
            : base(message)
        {
            if (code == 0 && !nonError)
                throw new ArgumentOutOfRangeException(nameof(code), code, "Cannot use 0 to indicate an error condition!");
            Code = code;
            IsNonError = nonError;
        }

        /// <summary>
        /// The return code.
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// True to indicate that this is just an early-out and not an error condition.
        /// </summary>
        public bool IsNonError { get; set; }
    }
}