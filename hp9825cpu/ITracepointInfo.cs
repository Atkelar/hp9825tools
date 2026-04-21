namespace HP9825CPU
{
    public interface ITracepointInfo
    {
        public string Name { get; }
        public string Label { get; }
        public TraceCategory Category { get; }
        public bool IsEnabled { get; set; }
        public bool ShowCallStats { get; set; }
        public int Count { get; }
        void ResetCount();
    }
}