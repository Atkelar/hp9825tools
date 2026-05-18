namespace HP9825CPU
{
    /// <summary>
    /// Denotes an object that can (optionally) participate in an HPL compiler context.
    /// </summary>
    public interface ICompilerContextual
    {
        public HPLCompilerContext? HPLContext { get; set; }
    }
}