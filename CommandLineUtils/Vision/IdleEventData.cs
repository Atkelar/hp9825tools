namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// Indicates an idle event (app is running the idle loop and waiting for input.)
    /// </summary>
    public sealed class IdleEventData
        : EventData
    {
        internal IdleEventData()
            : base(EventType.Idle)
        {}
    }
}