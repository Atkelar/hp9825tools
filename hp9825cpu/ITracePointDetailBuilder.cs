namespace HP9825CPU
{
    public interface ITracePointDetailBuilder
    {
        /// <summary>
        /// A more detailed description of when that trace point will be triggered; Should still be not more than a few sentences.
        /// </summary>
        /// <param name="text">The description</param>
        /// <returns>This builder again.</returns>
        ITracePointDetailBuilder Describe(string text);

        /// <summary>
        /// Creates a proxy object to use as a trigger point for convenience/porformance. Usually the last call in the chain.
        /// </summary>
        /// <returns>An object to provide a call point for the trace point.</returns>
        ITracePointTrigger Make();

        /// <summary>
        /// Provides the template for the message issued by this TP. Can contain [] placeholders for context specific names (registers, flags,...) and {} placeholders for parameters in the trace call.
        /// </summary>
        /// <param name="template">The template string.</param>
        /// <returns>This builder again.</returns>
        ITracePointDetailBuilder MessageTemplate(string template);

        /// <summary>
        /// Provides the default placeholder values for the {} placeholders in the message template. i.e. if the template string has {0}-{1}, you might use 0,1 for this.
        /// </summary>
        /// <param name="defaults">The default values; one for each {} placeholder.</param>
        /// <returns>This builder again.</returns>
        ITracePointDetailBuilder ParameterDefaults(params object?[] defaults);


    }
}