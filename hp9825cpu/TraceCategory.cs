using System;

namespace HP9825CPU
{
    /// <summary>
    /// Indicates the category for an emulator diagnostic trace message.
    /// </summary>
    [Flags()]
    public enum TraceCategory
    {
        /// <summary>
        /// None - only useful for filtering. Any actual trace MUST declare a single category!
        /// </summary>
        None = 0,
        /// <summary>
        /// 
        /// </summary>
        Normal = 1,
        Performace = 2,
        Warning = 4,
        Diagnostics = 8,
        Error = 16,
        All = -1
    }
}