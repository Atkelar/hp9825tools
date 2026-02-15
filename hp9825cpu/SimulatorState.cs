namespace HP9825CPU
{
    /// <summary>
    /// CPU Simulator - current state...
    /// </summary>
    public enum SimulatorState
    {
        /// <summary>
        /// The simulator has been crated, but is not yet ready to run.
        /// </summary>
        Created = 0,
        /// <summary>
        /// The simulator is currently in "reset" state; comparable to "pulling reset signal".
        /// </summary>
        Reset = 1,
        /// <summary>
        /// The simulator is normally executing code.
        /// </summary>
        Running = 2,
        /// <summary>
        /// The simulator has hit a breakpoint.
        /// </summary>
        BreakPointHit = 3,
        /// <summary>
        /// Whenever the simulator ended up in a state that is not valid; normal CPU would continue to run in circles probably, but the simulator aborts.
        /// </summary>
        FailedState = 4,
    }
}