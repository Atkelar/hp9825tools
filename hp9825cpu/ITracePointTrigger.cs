namespace HP9825CPU
{
    public interface ITracePointTrigger
    {
        void Invoke(params object?[] args);
    }
}