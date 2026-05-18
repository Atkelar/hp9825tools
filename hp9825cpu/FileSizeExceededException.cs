using System;

namespace HP9825CPU
{
    public class FileSizeExceededException 
        : Exception
    {
        public FileSizeExceededException(string message, params object?[] args) 
            : base(string.Format(message, args))
        {
        }
    }
}