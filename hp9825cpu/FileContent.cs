using System;
using System.IO;
using System.Threading.Tasks;

namespace HP9825CPU
{
    public abstract class FileContent
    {
        public TapeTrack? TrackOnSourceTape { get; internal set; }
        public int? IndexOnSourceTape { get; internal set; }

        public FileType Type { get; protected internal set; }
        public int WriteGeneration { get; protected internal set; }

        /// <summary>
        /// The maximum number of bytes (always even!) for the file. This is either the limit of the tape length, numeric size limit (word-length) or existing file size, depending on context.
        /// </summary>
        public int SizeLimit { get; internal set; }

        /// <summary>
        /// The size of the file "in use" in bytes (multiple of two)
        /// </summary>
        public virtual int UsedSize { get; }

        protected FileContent(int? track, int? fileIndex, int fileSize, FileType type, int generation)
        {
            IndexOnSourceTape = fileIndex;
            TrackOnSourceTape = track.HasValue ? (TapeTrack)track.Value : null;
            SizeLimit = fileSize;
            Type = type;
            WriteGeneration = generation;
        }

        public async Task ExportTo(TextWriter f)
        {
            await f.WriteLineAsync(string.Format("# Export file for type {0}, created {1}", this.Type, System.DateTime.Now));
            await f.WriteLineAsync(string.Format("#  reserved size: {0} bytes, used size {1} bytes.", this.SizeLimit, this.UsedSize));
            if (this.IndexOnSourceTape.HasValue || this.TrackOnSourceTape.HasValue)
                await f.WriteLineAsync(string.Format("# Exported from file {0} on track {1}.", IndexOnSourceTape, TrackOnSourceTape));
            await f.WriteLineAsync();
            await ExportContentNow(f);
        }

        protected abstract Task ExportContentNow(TextWriter f);

        internal void ReadFrom(ITapeFileReader input)
        {
            ReadInputFile(input);
        }

        protected abstract void ReadInputFile(ITapeFileReader input);
    }
}