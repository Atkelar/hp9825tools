using System;

namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// Occures when something catastrophic "out of order" happened.
    /// </summary>
    public class ApplicationStateException 
        : Exception
    {
        public ApplicationStateException(string message)
            : base(message)
        {
            
        }
    }
}