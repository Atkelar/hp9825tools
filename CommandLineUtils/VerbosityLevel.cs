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
        [AliasNames("q")]
        Quiet = -1,
        /// <summary>
        /// Errors only.
        /// </summary>
        [AliasNames("e")]
        Error = 0,
        /// <summary>
        /// Errors and warnings only.
        /// </summary>
        [AliasNames("w")]
        Warning = 1,
        /// <summary>
        /// Normal output. Includes banner messages.
        /// </summary>
        [AliasNames("n")]
        Normal = 2,
        /// <summary>
        /// Verbose output. Includes progress messages.
        /// </summary>
        [AliasNames("v")]
        Verbose = 3,
        /// <summary>
        /// Trace output. Maximum text!
        /// </summary>
        [AliasNames("t")]
        Trace = 4
    }
}