namespace CommandLineUtils
{
    /// <summary>
    /// Verbosity level for command output control.
    /// </summary>
    public enum VerbosityLevel
    {
        // NOTE: the values are important. "never-output" ones need to be negative, Errors needs to be the lowest (=0) and Trace the highest!
        /// <summary>
        /// No output from the built in functions.
        /// </summary>
        Quiet = -1,
        /// <summary>
        /// Errors only.
        /// </summary>
        Errors = 0,
        /// <summary>
        /// Errors and warnings only.
        /// </summary>
        Warnings = 1,
        /// <summary>
        /// Normal output. Includes banner messages.
        /// </summary>
        Normal = 2,
        /// <summary>
        /// Verbose output. Includes progress messages.
        /// </summary>
        Verbose = 3,
        /// <summary>
        /// Trace output. Maximum text!
        /// </summary>
        Trace = 4
    }
}