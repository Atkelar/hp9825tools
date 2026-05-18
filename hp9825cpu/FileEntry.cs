namespace HP9825CPU
{
    public class FileEntry
    {
        public int Index { get; internal set; }
        public int FileSize { get; internal set; }
        public int UsedSize { get; internal set; }
        public int Type { get; internal set; }
        public int Generation { get; internal set; }

        public int SecurityFlag {get; internal set;}
        public int ExtendedFlag {get; internal set;}

        public int Checksum { get; internal set; }

        public FileType TranslatedType { get => Type >= 0 && Type <= 6 ? (FileType)Type : FileType.Unknown; }
    }
}