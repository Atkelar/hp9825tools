using System;
namespace HP9825CPU
{
    /// <summary>
    /// A generic parsing error has happened!
    /// </summary>
    public class GenericException
        : Exception
    {
        internal GenericException(int code, string message)
            : base($"Error {code:0000}: {message}")
        { }
    }
}