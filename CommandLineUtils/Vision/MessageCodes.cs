namespace CommandLineUtils.Visuals
{
    public static class MessageCodes
    {
        /// <summary>
        /// Quit -> Request to terminate the application. The program can react by cancelling the operation. If the "quit" message is not cancelled during event handling, the <see cref="Exit"/> will follow.
        /// </summary>
        public const string Quit = "quit";

        /// <summary>
        /// Exit -> Terminate the application. This event doesn't support cancellation.
        /// </summary>
        public const string Exit = "exit";

        /// <summary>
        /// Sends a "we need to repaint somethign" message to the application.
        /// </summary>
        public const string Paint = "paint";

        /// <summary>
        /// Handled by the output driver, causes a "beep".
        /// </summary>
        public const string Beep = "beep";
    }
}