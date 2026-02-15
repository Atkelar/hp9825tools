namespace CommandLineUtils.Visuals
{
    public enum VisualProcessError
    {
        [ReturnCode("The visual program doesn't support in/out redirections!", HelpMessage = "Occures when a visual based program is run with the standard console driver and is I/O redirected.")]
        RedirectionNotSupported = 1,
        [ReturnCode("Internal program error: {0}", HelpMessage = "Occurs whenever there is an unresolvable issue in the logical flow of the program. This is to prevent infinite loops and hanging applications and should not happen in a release version...")]
        InternalProcessingError = 2
    }
}