namespace CommandLineUtils.Visuals
{
    public abstract class EventData
    {
        protected EventData(EventType type)
        {
            this.Type = type;
        }
        public EventType Type { get; private set; }
    }
}