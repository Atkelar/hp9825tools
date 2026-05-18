using System;
using System.IO;

namespace CommandLineUtils
{
    public static class PathUtils
    {
        /// <summary>
        /// Expands the provided path - by applying environment variables and most critically "~/" or "~\" handling!
        /// </summary>
        /// <param name="input">The path to expand</param>
        /// <param name="noEnvironment">Prevent the environment variables to be expanded. Any %var% will remain "as is"!
        /// <returns>The expanded version of the path.</returns>
        public static string ExpandPath(string input, bool noEnvironment = false)
        {
            HomeFolderPatterns ??= MakeHomeFolderPatterns();
            if (!noEnvironment)
                input = Environment.ExpandEnvironmentVariables(input);
            foreach(var p in HomeFolderPatterns)
                if (input.StartsWith(p))
                {
                    input = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), input.Substring(p.Length));
                    break;
                }
            return input;
        }

        private static string[] MakeHomeFolderPatterns()
        {
            return new string[] {
                $"~{Path.DirectorySeparatorChar}",
                $"~{Path.AltDirectorySeparatorChar}"
            };
        }

        private static string[]? HomeFolderPatterns;
    }
}