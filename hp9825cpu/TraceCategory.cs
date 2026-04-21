using System;

namespace HP9825CPU
{
    [Flags()]
    public enum TraceCategory
    {
        None = 0,
        Normal = 1,
        Performace = 2,
        Warning = 4,
        Diagnostics = 8,
        Error = 16,
        All = -1
    }
}