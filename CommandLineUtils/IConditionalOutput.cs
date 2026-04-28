using System;

namespace CommandLineUtils
{
    public interface IConditionalOutput
    {
        /// <summary>
        /// Starts an indented text part; ended with the disposal of the object returned.
        /// </summary>
        /// <param name="numChars">The number of characters to be indented. Maxes out at half the available width! If not set, will use the system (configured) default.</param>
        /// <returns>A tracker object. Use in a "using" block to undo-indent; Calling the <see cref="IDisposable.Dispose"/> method on this object will reset the indentation.</returns>
        public IDisposable Indent(int? numChars = null);

        /// <summary>
        /// Make sure we are now at the beginning of a new line, add line break if not.
        /// </summary>
        /// <param name="forceEmptyLine">True to force an empty line, false to stay in the current line if we are already on the beginning of a line.</param>
        public void EnsureNewLine(bool forceEmptyLine = false);

        /// <summary>
        /// Starts an indented text part, based on a headline; ended with the disposal of the object returned.
        /// </summary>
        /// <param name="header">The label text for the output; will be put into the first(current) line for the indented part.</param>
        /// <param name="numChars">The minimum number of characters to be indented. Maxes out at half the available width, will use the system (configured) default if null.</param>
        /// <returns>A tracker object. Use in a "using" block to undo-indent.</returns>
        public IDisposable IndentFor(string header, int? numChars = null);

        /// <summary>
        /// Creates a table formatter for tablularized output. Columns that don't fit to the current width, will be truncated! 1 char separator between columns, tables will be indented too!
        /// </summary>
        /// <param name="creator">Callback to create the table; will only get called if the level is actually requested.</param>
        /// <returns>A table formatter to use to create tabular output!</returns>
        public ITableFormatter Table(Action<ITableBuilder> creator);
    }
}