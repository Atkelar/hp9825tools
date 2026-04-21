namespace HP9825CPU
{
    public interface ITracePointBuilder
    {
        /// <summary>
        /// Creates a new tracepoint maker;
        /// </summary>
        /// <param name="name">The name of the TP - must be unique within the context of the builder.</param>
        /// <param name="label">The display name for debugging.</param>
        /// <param name="category">The category to enable/disable tracing in a group; Also used as an indicator in the log.</param>
        /// <returns>A builder interface to configure the details and get a proxy for calling.</returns>
        /// <remarks>
        /// <para>The trace point spec is kept internally in a list and can be invoked via the hosting object's methods and the <paramref name="name"/>. But the proxy, created and stored by the <see cref="ITracePointDetailBuilder.Make"/> call will be faster as it avoids a list lookup.</para>
        /// </remarks>
        ITracePointDetailBuilder Create(string name, string label, TraceCategory category = TraceCategory.Normal);
    }
}