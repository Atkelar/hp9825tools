using System;

namespace CommandLineUtils
{
    /// <summary>
    /// Creates a new table definitin.
    /// </summary>
    public interface ITableBuilder
    {
        /// <summary>
        /// Adds a column to a table.
        /// </summary>
        /// <param name="width">The width in characters.</param>
        /// <param name="build">The column defnition.</param>
        /// <returns>The builder for chained calls.</returns>
        ITableBuilder Column(int width, Action<ITableColumnBuilder> build);
        /// <summary>
        /// Adds a column to a table.
        /// </summary>
        /// <param name="min">Minimum width in chacacters.</param>
        /// <param name="max">Maximum width in characters.</param>
        /// <param name="build">The column defnition.</param>
        /// <returns>The builder for chained calls.</returns>
        ITableBuilder Column(int min, int max, Action<ITableColumnBuilder> build);
        /// <summary>
        /// Enable header or footer separator for the table.
        /// </summary>
        /// <param name="headerSeparator">True to print a dashed line between the headline and the body.</param>
        /// <param name="footerSeparator">True to print a dashed line between the body and footer of the table.</param>
        /// <param name="useTicks">True to add + marks between columns.</param>
        /// <returns>The builder for chained calls.</returns>
        ITableBuilder Separators(bool headerSeparator, bool footerSeparator, bool useTicks = false);

        /// <summary>
        /// Sets a template string (with placeholder {0}) for a "summary row" to show number of rows in a line after the table.
        /// </summary>
        /// <param name="template">The template string.</param>
        /// <returns>The builder for chained calls.</returns>
        ITableBuilder RowCountTemplate(string template);
    }
}