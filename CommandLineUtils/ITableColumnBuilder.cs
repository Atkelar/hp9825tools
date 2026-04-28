using System;

namespace CommandLineUtils
{
    /// <summary>
    /// Creates a single table column.
    /// </summary>
    public interface ITableColumnBuilder
    {
        /// <summary>
        /// Sets the alignment and trimming mode for the column. Default would be left aligned, end trimming.
        /// </summary>
        /// <param name="align">The alignment.</param>
        /// <param name="trim">The trimming mode.</param>
        /// <returns>The builder for chaining calls.</returns>
        ITableColumnBuilder Align(HorizontalAlignment align, TextTrimming trim = TextTrimming.End);
        /// <summary>
        /// Sets an optional header string.
        /// </summary>
        /// <param name="header">The header text.</param>
        /// <returns>The builder for chaining calls.</returns>
        ITableColumnBuilder Head(string header);
        /// <summary>
        /// Sets a format string for formatting <see cref="IFormattable"/>  based objects in the column.
        /// </summary>
        /// <param name="formatString">The format string.</param>
        /// <returns>The builder for chaining calls.</returns>
        ITableColumnBuilder Format(string formatString);
        /// <summary>
        /// Sets a callback to fetch a footer value for the column.
        /// </summary>
        /// <param name="source">A function that will be called when the table is done. Any value returned here will be added as a footer line.</param>
        /// <returns>The builder for chaining calls.</returns>
        ITableColumnBuilder FooterFrom(Func<object?> source);
    }
}