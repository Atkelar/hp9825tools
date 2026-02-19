using System;

namespace HP9825CPU
{
    public class MappingFileFormatError
        : Exception
    {
        public MappingFileFormatError(MappingFileErrorCode code, int lineNumber, string message)
            : base($"MF{(int)code:000} in line {lineNumber}: {message}")
        {
            Code = code;
            LineNumber = lineNumber;
            BaseMessage = message;
        }

        public MappingFileErrorCode Code { get; }
        public int LineNumber { get; }
        public string BaseMessage { get; }
    }
}