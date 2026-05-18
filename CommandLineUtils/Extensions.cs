using System.Text;

namespace CommandLineUtils
{
    /// <summary>
    /// Some utility functions.
    /// </summary>
    public static class Extensions
    {
        public static void WriteLine(this IConditionalOutput obj, string format, params object[] args)
        {
            obj.WriteLine(string.Format(format, args));
        }
        public static void WriteLine(this IConditionalOutput obj, SplitMode split, string format, params object[] args)
        {
            obj.WriteLine(split, string.Format(format, args));
        }
        public static void Write(this IConditionalOutput obj, string format, params object[] args)
        {
            obj.Write(string.Format(format, args));
        }
        public static void Write(this IConditionalOutput obj, SplitMode split, string format, params object[] args)
        {
            obj.Write(split, string.Format(format, args));
        }
        public static void WriteLine(this ProcessBase obj)
        {
            obj.WriteLine(VerbosityLevel.Normal, SplitMode.Any, (string?)null);
        }
        public static void WriteLine(this ProcessBase obj, string? message)
        {
            obj.WriteLine(VerbosityLevel.Normal, SplitMode.Any, message);
        }
        public static void WriteLine(this ProcessBase obj, string format, params object?[] args)
        {
            obj.WriteLine(VerbosityLevel.Normal, SplitMode.Any, string.Format(format, args));
        }
        public static void WriteLine(this ProcessBase obj, VerbosityLevel level)
        {
            obj.WriteLine(level, SplitMode.Any, (string?)null);
        }
        public static void WriteLine(this ProcessBase obj, VerbosityLevel level, string format, params object?[] args)
        {
            obj.WriteLine(level, SplitMode.Any, string.Format(format, args));
        }
        public static void WriteLine(this OutputHandlerBase obj)
        {
            obj.WriteLine(VerbosityLevel.Normal, SplitMode.Any, (string?)null);
        }
        public static void WriteLine(this OutputHandlerBase obj, string? message)
        {
            obj.WriteLine(VerbosityLevel.Normal, SplitMode.Any, message);
        }
        public static void WriteLine(this OutputHandlerBase obj, string format, params object?[] args)
        {
            obj.WriteLine(VerbosityLevel.Normal, SplitMode.Any, string.Format(format, args));
        }
        public static void WriteLine(this OutputHandlerBase obj, VerbosityLevel level)
        {
            obj.WriteLine(level, SplitMode.Any, (string?)null);
        }
        public static void WriteLine(this OutputHandlerBase obj, VerbosityLevel level, string format, params object?[] args)
        {
            obj.WriteLine(level, SplitMode.Any, string.Format(format, args));
        }
        public static void WriteLine(this OutputHandlerBase obj, VerbosityLevel level, SplitMode split, string format, params object?[] args)
        {
            obj.WriteLine(level, split, string.Format(format, args));
        }
    }
}