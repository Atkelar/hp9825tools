using System.IO;
using System.Threading.Tasks;

namespace HP9825CPU
{
    public class RawFileContent
        : FileContent
    {
        public RawFileContent(int? track, int? fileIndex, int fileSize, FileType type, int generation) 
            : base(track, fileIndex, fileSize, type, generation)
        {
        }

        protected override Task ExportContentNow(TextWriter f)
        {
            throw new System.NotImplementedException();
        }

        protected override void ReadInputFile(ITapeFileReader input)
        {
            throw new System.NotImplementedException();
        }
    }
}