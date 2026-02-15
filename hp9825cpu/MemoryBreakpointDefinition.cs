namespace HP9825CPU
{
    public class MemoryBreakpointDefinition
    {
        public bool OnRead { get; internal set; }
        public bool OnWrite { get; internal set; }
        public bool IsEnabled { get; internal set; }
    }
}