using System;

namespace HP9825CPU
{
    /// <summary>
    /// Stores a reference to the origin of a code element (filename and line number) to be used to create errors.
    /// </summary>
    public struct SourceLineRef
    {
        /// <summary>
        /// Wrap the provided source file name and line number.
        /// </summary>
        /// <param name="sourceFile">The source filename.</param>
        /// <param name="lineNumber">The line number (1 based) in the file.</param>
        public SourceLineRef(string sourceFile, int lineNumber)
        {
            SourceFile = sourceFile;
            LineNumber = lineNumber;
        }

        /// <summary>
        /// Static option for an unknown file location.
        /// </summary>
        public static readonly SourceLineRef Unknown = new SourceLineRef("?", 0);

        /// <summary>
        /// The file name.
        /// </summary>
        public readonly string SourceFile;
        /// <summary>
        /// The line number.
        /// </summary>
        public readonly int LineNumber;

        /// <summary>
        /// Creates an exception for the provided assembler error code.
        /// </summary>
        /// <param name="code">The error code, as per enum.</param>
        /// <param name="message">The error message, including format placeholders.</param>
        /// <param name="args">The formatting values.</param>
        /// <returns>An exception, ready to throw.</returns>
        public Exception Error(AssemblerErrorCodes code, string message, params object?[] args)
        {
            return Error(code, string.Format(message, args));
        }

        /// <summary>
        /// Creates an exception for the provided assembler error code.
        /// </summary>
        /// <param name="code">The error code, as per enum.</param>
        /// <param name="message">The error message</param>
        /// <returns>An exception, ready to throw.</returns>
        public Exception Error(AssemblerErrorCodes code, string message)
        {
            return new ParsingException((int)code, SourceFile, LineNumber, message);
        }
    }
}