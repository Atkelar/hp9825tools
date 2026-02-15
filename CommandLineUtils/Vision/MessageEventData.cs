namespace CommandLineUtils.Visuals
{
    public class MessageEventData
        : EventData
    {
        internal MessageEventData(string code, Visual? sender, object? args)
            : base(EventType.Message)
        {
            Code = code;
            Args = args;
            Sender = sender;
        }

        public Visual? Sender {get;}
        public string Code { get; }
        public object? Args { get; }
        public bool Cancel { get; set; }

    }
}