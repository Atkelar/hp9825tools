namespace HP9825CPU
{
    public interface ITapeFileReader
    {
        int ReservedSize { get; }
        int UsedSize { get; }
        int AbsoluteOffset {get; }
        bool EndOfFile {get;}
        int Generation { get; }
        int? ReadWord();
   }
}