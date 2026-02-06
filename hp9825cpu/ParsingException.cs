using System;
namespace HP9825CPU
{
    public class ParsingException
        : Exception
    {
        public ParsingException(int code, string sourceFile, int lineNumber, string message)
            : base($"Error {code:0000} in {sourceFile} ({lineNumber}): {message}")
        {
            
        }
    }
}