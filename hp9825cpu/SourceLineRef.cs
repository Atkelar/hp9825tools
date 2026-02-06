using System;

namespace HP9825CPU
{
    public struct SourceLineRef
    {
        public SourceLineRef(string sourceFile, int lineNumber)
        {
            SourceFile = sourceFile;
            LineNumber = lineNumber;
        }

        public static readonly SourceLineRef Unknown = new SourceLineRef("?", 0);

        public readonly string SourceFile;
        public readonly int LineNumber;

        public Exception Error(AssemblerErrorCodes code, string message, params object?[] args)
        {
            return Error(code, string.Format(message, args));
        }

        public Exception Error(AssemblerErrorCodes code, string message)
        {
            return new ParsingException((int)code, SourceFile, LineNumber, message);
        }
    }
}