using System;

namespace HP9825CPU
{
    /// <summary>
    /// 
    /// </summary>
    [Flags()]
    public enum PinNumber
    {
        None = 0,
        Pin1 = 1,
        Pin2 = 2,
        Pin3 = 4,
        Pin4 = 8,
        All = 0xF,
    }
}