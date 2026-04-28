using System;

namespace CommandLineUtils
{
    /// <summary>
    /// Provides access to functions to add table rows and close off the table at the end.
    /// </summary>
    public interface ITableFormatter
        : IDisposable
    {
        /// <summary>
        /// Print a single line in a table.
        /// </summary>
        /// <param name="columns">The valus for the columns.</param>
        public void Line(params object?[] columns);
    }
}