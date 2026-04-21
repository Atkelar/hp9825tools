using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.VisualBasic.FileIO;

namespace HP9825CPU
{
    /// <summary>
    /// Data structure to capture a "tape" cartridge for the simulated tape drive.
    /// </summary>
    public class TapeCartridge
    {
        /// <summary>
        /// The drive that the tape is currently residing in - null if the tape is "out".
        /// </summary>
        public TapeDrive? InDrive { get; internal set; }

        /// <summary>
        /// Last mount/dismount timestamp. For archival/reference purposes only.
        /// </summary>
        public DateTime LastUsed { get; internal set; }

        private string _Label = string.Empty;

        /// <summary>
        /// Cartridge label - for reference only.
        /// </summary>
        public string Label 
        { 
            get
            {
                return _Label;
            }
            set
            {
                if (!_Label.Equals(value))
                {
                    _Label = value;
                    Clean = false;
                }
            }
        }

        private string? _Comment = null;

        /// <summary>
        /// Comment - free text, multiline comment field. For reference only.
        /// </summary>
        public string? Comment 
        { 
            get
            {
                return _Comment;
            }
            set
            {
                if (_Comment != value)
                {
                    _Comment = value;
                    Clean = false;
                }
            }
        }

        /// <summary>
        /// Date/time the cartridge was first used.
        /// </summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// The date/time the last write operation occured on the cartridge. Null if "virgin".
        /// </summary>
        public DateTime? LastWritten { get; internal set; }

        private bool _Clean;
        /// <summary>
        /// Clean = unmodified. True if the content or metadata hasn't been changed since loading, false if it has. This tracks all propertie
        /// </summary>
        public bool Clean 
        { 
            get
            {
                return _Clean;
            }
            private set
            {
                _Clean = value;
                if (_Clean)
                    _MostlyClean = true;    // if we are fully clean, this indicates a "save", so also assume that all metadata is saved.
            }
        }

        private bool _MostlyClean;
        /// <summary>
        /// Similar to <see cref="Clean">; but for "irrelevant" metadata only: this only tracks the <see cref="Readonly"/> flag and the <see cref="LastUsed"/> values, 
        /// which can be saved but are considered uncritical when lost. So while Clean will go false on important changes, this one will only go false on all changes.
        /// </summary>
        public bool MostlyClean 
        { 
            get
            {
                return _MostlyClean;
            }
        }

        private double _Position = 0;
        /// <summary>
        /// The absolute tape position. In inch... since all speeds/distances are noted in inch, this makes it a bit easier to compute. 
        /// NOTE: -1 is where the first index hole is, i.e. "rewind" triggers the "hole indicator" here.
        /// </summary>
        public double Position 
        {
            get => _Position;
            set
            {
                if (_Position != value)
                {
                    _Position = value;
                    _MostlyClean = false;
                }
            }
        }


        private TapeCartridge()
        {

        }

        public static TapeCartridge Create(string? label = null, double length = DefaultLength)
        {
            var now = DateTime.UtcNow;
            return new TapeCartridge()
            {
                Label = label ?? string.Empty,
                Length = length,
                CreatedAt = now,
                Clean = true,
                LastUsed = now
            };
        }

        /*
        Index holes are at positions:
        the tape has the hole pattern:
            oo    oo    oo    o[content]o    ooo

        This is important to signal the drive for "BOT/EOT" markers. Actual R/W operatoins should only take place within the [content] section.
        As of now, I don't have "tail end" measurements as all my tapes are reasonably well contained, estimating... The patent shows a single
        triple marker at the end.
            in mm - [740 o 6 o 399 o 6 o 399 o 6 o 606 o][content][o ?? o ? o ? o]
            in inches (rounded) - [29.13 o 0.25 o 15.75 o 0.25 o 15.75 o 0.25 o 23.75 o][content][o 23.75 o 0.25 o 0.25 o]

        Tape format: Pat. page #245 shows a schematic section.

        Tape has two tracks, we use "0" and "1" here. These map to the head selection line in the drive.

        Each track has a completely linear list of files:
        [file 0][file 1][file 2]...

        A single file consists of:
        [iner file gap][file header][partitions]

        The partitions have gaps themselves, just much smaller than the file gaps.
        Each file has a fixed length.

        The "0" length file is an "end of directory" marker at the end of the file list. Valid files are thus at minimum one byte in length.

        */

        /// <summary>
        /// The length of the tape, between the "index holes" - i.e. the first "beginning" index hole is at position 0, the first ending one at "length".
        /// </summary>
        public double Length { get; private set; } = DefaultLength;

        /// <summary>
        /// Typical length based on programming manual page 5-3: 140ft
        /// </summary>
        public const double DefaultLength = 1680;


        private bool _Readonly = false;

        /// <summary>
        /// Enable read only mode (write protected); NOTE: only checked/updated by the drive during insertion!
        /// </summary>
        public bool Readonly 
        { 
            get
            {
                return _Readonly;
            }
            set
            {
                if (_Readonly != value)
                {
                    _Readonly = value;
                    _MostlyClean = false;
                }
            } 
        }

        /// <summary>
        /// Writes the tape to a target file.
        /// </summary>
        /// <param name="filename">The target filename.</param>
        /// <param name="packageFile">True to create a "single file" (=package), false to create a multi-file output with the manifest file as provided.</param>
        /// <returns>The task...</returns>
        /// <exception cref="InvalidOperationException">If the tape is currently "in" a running machine.</exception>
        public async Task Save(string filename, bool packageFile = false, bool includeDumpFile = false)
        {
            if (this.InDrive != null && (this.InDrive.System?.HostCpu?.IsFreeRunning).GetValueOrDefault())
                throw new InvalidOperationException("Cannot save a tape that is in use!");
            if (packageFile)
                await SaveToPackage(filename);
            else
                await SaveToFileStructure(filename);

            if (includeDumpFile)
            {
                using (var trk = File.Create(Path.ChangeExtension(filename, ".trk-a.dmp")))
                {
                    await DumpTrack(_Tracks[0], trk);
                }
                using (var trk = File.Create(Path.ChangeExtension(filename, ".trk-b.dmp")))
                {
                    await DumpTrack(_Tracks[1], trk);
                }
            }
        }

        private List<FileEntry> GetDirectory(LinkedBlock root)
        {
            List<FileEntry> result = new List<FileEntry>();
            var x = root;
            int fileIndex = 0;
            while (x!=null)
            {
                if (x.Block.Gap > 0.75 && x.Block.Data.Count>=9 && x.Block.Data[0] == 1) // usually, a 1" gap is the "IPG", file header is 10 words, but last is filler.
                {
                    // found next file...
                    result.Add(new FileEntry()
                        {
                            Index = fileIndex,
                            FileSize = x.Block.Data[2] * 2,
                            UsedSize = x.Block.Data[3] * 2,
                            Type = x.Block.Data[4],
                            Generation = x.Block.Data[5],   // generation number - incremented on each save, must be same for all partitions.
                            Checksum = x.Block.Data[8]
                        }
                    );
                    fileIndex++;
                    if (x.Block.Data[4] == 0 && x.Block.Data[2] == 0)
                        // found "null" file for end of directory.
                        break;
                }
                x = x._Next;
            }
            return result;
        }

        private async Task DumpFile(LinkedBlock? root, int fileNumber, TextWriter tw)
        {
            LinkedBlock? fileAt = FindFileByIndex(root, fileNumber);
            if (fileAt == null)
                return;
            int type = fileAt.Block.Data[4];
            int size = fileAt.Block.Data[3];
            int generation = fileAt.Block.Data[5];
            switch(type)
            {
                case 0:
                    await tw.WriteLineAsync(string.Format(" File #{0} is a null file.", fileNumber));
                    break;
                case 2:
                    await DumpNumericFile(fileAt, tw);
                    break;
                case 3:
                    await DumpMixedFile(fileAt, tw);
                    break;
                default:
                    await tw.WriteLineAsync(string.Format(" File #{0} is of type {1} - not yet implemented!", fileNumber, type));
                    break;
            }
        }

        public async Task DumpFile(int trackNumber, int fileNumber, TextWriter tw)
        {
            await DumpFile(_Tracks[trackNumber], fileNumber, tw);
        }

        public async Task DumpFile(int trackNumber, int fileNumber, string targetFile)
        {
            LinkedBlock? fileAt = FindFileByIndex(trackNumber, fileNumber);
            if (fileAt == null)
                throw new InvalidOperationException($"File {fileNumber} not found on {trackNumber}!");
            int type = fileAt.Block.Data[4];
            int size = fileAt.Block.Data[3];
            int generation = fileAt.Block.Data[5];
            switch(type)
            {
                case 0:
                    // null file...
                    break;
                case 2:
                    await DumpNumericFile(fileAt, targetFile);
                    break; 
                case 3:
                    await DumpMixedFile(fileAt, targetFile);
                    break; 
            }
        }

        private async Task DumpMixedFile(LinkedBlock fileAt, string targetFile)
        {
            using(var tw = File.CreateText(Path.ChangeExtension(targetFile, ".mixed.txt")))
            {
                await DumpMixedFile(fileAt, tw);
            }
        }


        private async Task DumpNumericFile(LinkedBlock fileAt, string targetFile)
        {
            using(var tw = File.CreateText(Path.ChangeExtension(targetFile, ".numeric.txt")))
            {
                await DumpNumericFile(fileAt, tw);
            }
        }

        /// <summary>
        /// advances through a tape partition list and treats a long-gap as a EOF marker.
        /// </summary>
        private class StreamInterpreter
        {
            private LinkedBlock? _Now;

            public int ReservedSize { get; private set; }
            public int UsedSize { get; private set; }
            public int AbsoluteOffset {get; private set; }

            public bool EndOfFile => _EndOfFile;

            private int Generation;
            private int LastPartitionIndex;

            public StreamInterpreter(LinkedBlock fileHeader, int startAtIndex=0)
            {
                _Now = fileHeader;
                ReservedSize = fileHeader.Block.Data[2];
                UsedSize = fileHeader.Block.Data[3];
                if (_Now.Block.Data[3] == 0 || _Now._Next == null)
                    _EndOfFile = true;
                else
                {
                    if (!ValidateCheckSum(_Now.Block.Data, 1,7,_Now.Block.Data[8]))
                        throw new InvalidOperationException("File header checksum doesn't add up!");
                    Generation = fileHeader.Block.Data[5];
                    LastPartitionIndex = -1;
                    MoveToNextPartition();
                }
            }

            private void MoveToNextPartition()
            {
                _Now = _Now?._Next;
                if (_Now == null || _Now.Block.Gap > 0.05 || _Now.Block.Data.Count < 5)
                    _EndOfFile = true;
                else
                {
                    // check up on partition header...
                    var x = _Now.Block.Data;
                    if (x[0] != 1 || x[5] != 1)
                        throw new InvalidOperationException("Format error: partition doesn't start with 1.");
                    if (!ValidateCheckSum(x, 1, 3, x[4]))
                        throw new InvalidOperationException("Partition header checksum error!");
                    int partNumber = x[1];
                    int partLength = x[2];
                    int partGen = x[3];
                    if (LastPartitionIndex + 1 != partNumber)
                        throw new InvalidOperationException("Partition index out of order!");
                    if (partGen != Generation)
                        throw new InvalidOperationException("Stale partition read?!");
                    if (x.Count < partLength + 8)
                        throw new InvalidOperationException("Partition data too short!");
                    if (!ValidateCheckSum(x, 6, 5+partLength, x[6+partLength]))
                        throw new InvalidOperationException("Partition body checksum error!");

                    PartitionOffset = 0;
                    PartitionLength = partLength;
                }
            }

            public int? ReadWord()
            {
                if(PartitionOffset >= this.PartitionLength)
                    _EndOfFile = true;
                if (_EndOfFile)
                    return null;
                // find next word...
                if (PartitionOffset >= PartitionLength)
                    MoveToNextPartition();
                if (_EndOfFile)
                    return null;
                var value = _Now?.Block.Data[PartitionOffset + 6];
                PartitionOffset++;
                return value;
            }

            private bool ValidateCheckSum(List<ushort> data, int fromOffset, int toOffset, int expected)
            {
                int counter =0;
                for(int i=fromOffset; i <= toOffset; i++)
                    counter = (counter + data[i]) & 0xFFFF;
                return counter == expected;
            }

            private bool _EndOfFile;
            private int PartitionOffset;
            private int PartitionLength;
        }

        private async Task DumpMixedFile(LinkedBlock fileAt, TextWriter tw)
        {
            await tw.WriteLineAsync(string.Format("#  Mixed file dump from {0} @ {1:0.000}\"", Label, fileAt.Block.Start));
            await tw.WriteLineAsync("#  NOT COMPLETELY TESTED!");
            await tw.WriteLineAsync();
            var si = new StreamInterpreter(fileAt);
            int index = 0;
            while (!si.EndOfFile)
            {
                var a = si.ReadWord() ?? 0;
                if (a == 9)
                {
                    // This seems to be a string...
                    var b = si.ReadWord() ?? 0;
                    if (b != 0)
                        throw new InvalidOperationException("Unexpected format in string?!");
                    var rLen = si.ReadWord() ?? 0;
                    var uLen = si.ReadWord() ?? 0;
                    await tw.WriteAsync(string.Format("{0,3}: $[{1}] \"", index, rLen));
                    if (rLen % 2 != 0)
                        rLen++;
                    rLen /= 2;
                    while (rLen>0)
                    {
                        var xChars = si.ReadWord();
                        if (!xChars.HasValue)
                        {
                            await tw.WriteLineAsync(">END OF RECORD!");
                            break;
                        }
                        var c1 = (char)((xChars.Value >> 8) & 0xFF);
                        if (uLen > 0)
                        {
                            uLen --;
                            await tw.WriteAsync(c1);
                        }
                        else
                            if (c1 != ' ')
                                Debug.WriteLine("Non-whitespace unused char: " + c1);
                        c1 = (char)(xChars.Value & 0xFF);
                        if (uLen > 0)
                        {
                            uLen --;
                            await tw.WriteAsync(c1);
                        }
                        else
                            if (c1 != ' ')
                                Debug.WriteLine("Non-whitespace unused char: " + c1);

                        rLen--;
                    }
                    if (rLen == 0)
                        await tw.WriteLineAsync("\"");
                }
                else
                {
                    var b = si.ReadWord() ?? 0;
                    var c = si.ReadWord() ?? 0;
                    var d = si.ReadWord() ?? 0;
                    FloatingPointNumber num = FloatingPointNumber.FromParts(a,b,c,d);
                    await tw.WriteLineAsync(string.Format("{0,3}: {1}", index, num));
                }
                index++;
            }
            await tw.WriteLineAsync();
            await tw.WriteLineAsync("#  NOTE: values are stored in reverse order!");
        }

        private async Task DumpNumericFile(LinkedBlock fileAt, TextWriter tw)
        {
            await tw.WriteLineAsync(string.Format("#  Numeric file dump from {0} @ {1:0.000}\"", Label, fileAt.Block.Start));
            await tw.WriteLineAsync();
            var si = new StreamInterpreter(fileAt);
            if(si.UsedSize % 4 != 0)
                throw new InvalidOperationException("Numeric files must be 8-byte groups!");
            int index = 0;
            while (!si.EndOfFile)
            {
                var a = si.ReadWord() ?? 0;
                var b = si.ReadWord() ?? 0;
                var c = si.ReadWord() ?? 0;
                var d = si.ReadWord() ?? 0;
                FloatingPointNumber num = FloatingPointNumber.FromParts(a,b,c,d);
                await tw.WriteLineAsync(string.Format("{0,3}: {1}", index, num));
                index++;
            }
            await tw.WriteLineAsync();
            await tw.WriteLineAsync("#  NOTE: numerics are stored in reverse order!");
        }

        private LinkedBlock? FindFileByIndex(int trackNumber, int fileNumber)
        {
            return FindFileByIndex(_Tracks[trackNumber], fileNumber);
        }

        private LinkedBlock? FindFileByIndex(LinkedBlock? root, int fileNumber)
        {
            while (fileNumber >= 0 && root != null)
            {
                while (root != null && !(root.Block.Gap >= 0.75 && root.Block.Data.Count >8 && root.Block.Data[0]== 1))
                    root = root._Next;

                if (fileNumber == 0)    
                    return root;

                fileNumber--;
                root = root?._Next;
            }
            return root;
        }

        private List<FileEntry> GetDirectory(int trackNumber)
        {
            return GetDirectory(_Tracks[trackNumber]);
        }

        private async Task DumpTrack(LinkedBlock? linkedBlock, FileStream trk)
        {
            using(var b = new StreamWriter(trk))
            {
                int count = 0;
                double sGapLen = Length, lGapLen = 0, tGapLen = 0;
                var root = linkedBlock;
                while (linkedBlock != null)
                {
                    b.WriteLine("Block from {0:0.000}\" - {1:0.000}\" with a gap of {2:0.000}\"", linkedBlock.Block.Start, linkedBlock.Block.End, linkedBlock.Block.Gap);
                    for (int i=0;i<linkedBlock.Block.Data.Count;i++)
                    {
                        int w = linkedBlock.Block.Data[i];
                        char c1 = ' ';
                        char c2 = ' ';
                        if ((w & 0xFF) >=32)
                            c1 = (char)(w & 0xFF);
                        if (((w >> 8) & 0xFF) >= 32)
                            c2 = (char)((w >> 8) & 0xFF);

                        b.WriteLine(" {0:000} {1:x4} {3}{2}", i, w, c1, c2);
                    }
                    b.WriteLine("   ---- total {0} words", linkedBlock.Block.Data.Count);
                    count++;
                    if (linkedBlock.Block.Gap < sGapLen)
                        sGapLen = linkedBlock.Block.Gap;
                    if (linkedBlock.Block.Gap > lGapLen)
                        lGapLen = linkedBlock.Block.Gap;
                    tGapLen += linkedBlock.Block.Gap;
                    linkedBlock = linkedBlock._Next;
                }
                b.WriteLine("Total: {0} blocks; Gaps are between {1} and {2}, totalling {3} average of {4} inches.", count, sGapLen, lGapLen, tGapLen, count > 0 ? tGapLen / count : 1);

                b.WriteLine();
                var directory = GetDirectory(root);
                foreach(var x in directory)
                {
                    b.WriteLine("#{0}", x.Index);
                    b.WriteLine(" {0}   {1,-6}{2,-6}  | {3} ", x.Type, x.UsedSize, x.FileSize, x.TranslatedType);
                }
                for(int i = 0;i<directory.Count;i++)
                    await DumpFile(root, i, b);
            }
        }


        private async Task SaveToPackage(string filename)
        {
            using(var pack = System.IO.Packaging.Package.Open(filename, FileMode.Create))
            {
                var mdf = pack.CreatePart(new Uri("/metadata", UriKind.Relative), "text/xml", System.IO.Packaging.CompressionOption.Normal);
                using (var tw = new StreamWriter(mdf.GetStream(FileMode.Create)))
                {
                    await WriteMetadata(tw);
                }
                using (var trk = pack.CreatePart(new Uri("/track-a", UriKind.Relative), "application/octet-stream", System.IO.Packaging.CompressionOption.Normal).GetStream(FileMode.Create))
                {
                    await WriteTrack(_Tracks[0], trk);
                }
                using (var trk = pack.CreatePart(new Uri("/track-b", UriKind.Relative), "application/octet-stream", System.IO.Packaging.CompressionOption.Normal).GetStream(FileMode.Create))
                {
                    await WriteTrack(_Tracks[1], trk);
                }
            }
            FixupMetadata();
        }

        private async Task SaveToFileStructure(string filename)
        {
            using(var tw = File.CreateText(filename))
            {
                await WriteMetadata(tw);
            }
            using (var trk = File.Create(Path.ChangeExtension(filename, ".trk-a")))
            {
                await WriteTrack(_Tracks[0], trk);
            }
            using (var trk = File.Create(Path.ChangeExtension(filename, ".trk-b")))
            {
                await WriteTrack(_Tracks[1], trk);
            }
            FixupMetadata();
        }

        private async Task WriteTrack(LinkedBlock? linkedBlock, Stream trk)
        {
            using(var b = new BinaryWriter(trk))
            {
                while (linkedBlock != null)
                {
                    b.Write((byte)1);
                    b.Write(linkedBlock.Block.Start);
                    b.Write(linkedBlock.Block.Gap);
                    b.Write(linkedBlock.Block.End);
                    b.Write(linkedBlock.Block.Data.Count);
                    for(int i=0;i<linkedBlock.Block.Data.Count;i++)
                        b.Write(linkedBlock.Block.Data[i]);
                    linkedBlock = linkedBlock._Next;
                }
                b.Write((byte)0);
            }
        }

        private void FixupMetadata()
        {
            this.Clean = true;
        }

        private string MetaNamespace = "https://schemas.atkelar.com/hp9825/tape/v1";
        private string PrefixTag = "tap";

        private async Task WriteMetadata(StreamWriter tw)
        {
            using (var x = XmlWriter.Create(tw, new XmlWriterSettings() { Async = true, Indent = true }))
            {
                await x.WriteStartDocumentAsync();
                await x.WriteStartElementAsync(PrefixTag, "tape", MetaNamespace);

                await x.WriteAttributeStringAsync(PrefixTag, "created", MetaNamespace, this.CreatedAt.ToString("O"));
                await x.WriteAttributeStringAsync(PrefixTag, "lastUsed", MetaNamespace, this.LastUsed.ToString("O"));
                if  (this.LastWritten.HasValue)
                    await x.WriteAttributeStringAsync(PrefixTag, "lastWritten", MetaNamespace, this.LastWritten.Value.ToString("O"));

                await x.WriteAttributeStringAsync(PrefixTag, "length", MetaNamespace, this.Length.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                await x.WriteAttributeStringAsync(PrefixTag, "position", MetaNamespace, this.Position.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                await x.WriteAttributeStringAsync(PrefixTag, "label", MetaNamespace, this.Label);

                if (!string.IsNullOrWhiteSpace(this.Comment))
                {
                    await x.WriteStartElementAsync(PrefixTag, "comment", MetaNamespace);
                    // TODO: maybe add "xml:space=preserve" instead of cdata?
                    await x.WriteCDataAsync(this.Comment);
                    await x.WriteEndElementAsync();
                }

                // TODO: write track content index?
 
                await x.WriteEndElementAsync();
            }
        }

        private class LinkedBlock
        {
            public LinkedBlock(DataStreak block)
            {
                Block = block;
            }
            public LinkedBlock? _Next, _Prev;

            /// <summary>
            /// Block is read only to avoid caching problem during modification. Create a new linked list block and attach it if changes are needed!
            /// </summary>
            public DataStreak Block {get; private set;}

            public override string ToString()
            {
                return string.Format("{1} {0} {2}", Block.ToString(), _Prev != null ? '<' :' ', _Next != null ?'>':' ');
            }
        }

        private LinkedBlock?[] _Tracks = new LinkedBlock?[2];

        internal void WriteBlock(int recordOnHead, DataStreak block)
        {
            LastWritten = DateTime.UtcNow;
            Clean = false;
            Debug.WriteLine("Received block from {0:0.000}-{1:0.000} with {2} words.", block.Start, block.End, block.Data.Count);
            // phase in the recorded block...
            // we want to be the structure of the blocks to be: start[gap][data]end as it is provided by the tape drive.
            // but we need to align the blocks one after another, so the "start" of the 2nd block is the "end" of the first... that might need gap-adjustments.
            // if a block is semi-overwritten, bail with an error. The blocks should ONLY ever "wobble" within the gaps, or overwrite from the start!
            var x = _Tracks[recordOnHead];
            if (x==null)
            {
                // first block on track, plop in where it is...
                _Tracks[recordOnHead] = new LinkedBlock(block);
            }
            else
            {
                // find place to put the new block...
                while (x._Next!=null && x.Block.End <= block.Start) x=x._Next;
                if (x.Block.End > block.Start)
                {
                    // need to "incorporate" the block here...
                    // what *can* happen...
                    // Existing files:   ---1---  ----2----   ---3----  ----4-----
                    // new files:   A:        ----A---
                    //              B:    --B--
                    //              C:  --C--
                    //              D:               -------D---------------
                    
                    // landed in block might be the first one for case "C"; case "B" needs to extend the gap from 1. Case A and D need to cut off the start of the block.
                    LinkedBlock landedInBlock = x;
                    while (x != null && x.Block.End <= block.End)
                        x = x._Next;
                    LinkedBlock? stretchesToBlock = x;

                    LinkedBlock correctedBlock;

                    // the starting block might be cut before the new block
                    // starting block cuts *should* only ever happen within gap regions.
                    // the ending block might be cut after the new block
                    if (landedInBlock.Block.Start + landedInBlock.Block.Gap + 16*TapeDrive.Bit0ClockEveryInch > block.Start)    // we also trim the starting word's 0 bits!
                    {
                        // we started in or before the existing gap.
                        var newStart = Math.Min(landedInBlock.Block.Start, block.Start);
                        var newGap = (block.Start + block.Gap) - newStart;
                        correctedBlock = new LinkedBlock(new DataStreak(newStart, block.End, newGap) { Data = block.Data });
                        ReplaceBlock(landedInBlock, correctedBlock);
                    }
                    else 
                    {
                        var replace = new LinkedBlock(new DataStreak(landedInBlock.Block.Start, block.Start, landedInBlock.Block.Gap));
                        int numWords = WordCountByDistance(landedInBlock.Block, block.Start);
                        for(int i=0;i<numWords;i++)
                            replace.Block.Data.Add(landedInBlock.Block.Data[i]);
                        ReplaceBlock(landedInBlock, replace);
                        landedInBlock = replace;
                        // we keep the "stretches to" reference for now, this should be fixed below...
                        correctedBlock = new LinkedBlock(block);
                        InsertAfter(landedInBlock, correctedBlock);
                    }

                    // by here, we have "landed in block" pointing to the original block, the existing "chain" 
                    // has been updated to have whatever the result of the start slice was and the "correctedBlock"
                    // is our new data block, linked into the chain at the startint position...

                    if (stretchesToBlock == null)
                    {
                        // we got to the end of the chain; make sure the new block reflects that
                        correctedBlock._Next = null;
                    }
                    else
                    {
                        stretchesToBlock._Prev = null;  // unhook so that list operations won't mess up the new chain.
                        if (stretchesToBlock.Block.End - TapeDrive.Bit0ClockEveryInch*16 < correctedBlock.Block.End)
                        {
                            // we can trim out the "stretches to" block by moving the follow-up block closer/extending its gap.
                            if (stretchesToBlock._Next != null)
                            {
                                var newFollowup = new LinkedBlock(new DataStreak(correctedBlock.Block.End, stretchesToBlock._Next.Block.End, (stretchesToBlock._Next.Block.Start + stretchesToBlock._Next.Block.Gap) - correctedBlock.Block.End) { Data = stretchesToBlock._Next.Block.Data });
                                newFollowup._Prev = correctedBlock;
                                correctedBlock._Next = newFollowup;
                                newFollowup._Next = stretchesToBlock._Next._Next;
                                if (newFollowup._Next != null)
                                    newFollowup._Next._Prev = newFollowup;
                            }
                            else
                                correctedBlock._Next = null;
                        }
                        else
                        {
                            // split the stretches to block by the end of the current block...
                            // to simplify the operation, we phace in the old "extends to" block into the new chain first...
                            correctedBlock._Next = stretchesToBlock;
                            stretchesToBlock._Prev = correctedBlock;
                            if (correctedBlock.Block.End < stretchesToBlock.Block.Start + stretchesToBlock.Block.Gap)
                            {
                                // we landed in the gap! Trim the gap to a new block...
                                var replace = new LinkedBlock(new DataStreak(correctedBlock.Block.End, stretchesToBlock.Block.End,(stretchesToBlock.Block.Start + stretchesToBlock.Block.Gap) - correctedBlock.Block.End ) { Data = stretchesToBlock.Block.Data });
                                ReplaceBlock(stretchesToBlock, replace);
                            }
                            else
                            {
                                // we landed in the data... new gap size = 0, trim data...
                                int numWordsToStrip = WordCountByDistance(stretchesToBlock.Block, correctedBlock.Block.End) + 1;  // ceiling instead of floor; better trim too much here.
                                var replace = new LinkedBlock(new DataStreak(correctedBlock.Block.End, stretchesToBlock.Block.End, 0));
                                for(int i = numWordsToStrip; i < stretchesToBlock.Block.Data.Count;i++)
                                    replace.Block.Data.Add(stretchesToBlock.Block.Data[i]);
                                ReplaceBlock(stretchesToBlock, replace);
                            }
                        }
                    }


                    // // check where we need to cut the starting block...
                    // if (landedInBlock.Block.Start + landedInBlock.Block.Gap > block.Start)
                    // {
                    //     // our new block gap is inside the existing gap...
                    //     // this should take care of case C and B
                    //     double newGapLength = (block.Start + block.Gap) - landedInBlock.Block.Start;
                    //     // oops...
                    //     var replacement = new LinkedBlock(new DataStreak(landedInBlock.Block.Start, block.End, newGapLength) { Data = block.Data });
                    //     if (landedInBlock.Block.End - TapeDrive.Bit0ClockEveryInch*16 < block.End)
                    //     {
                    //         // we have no leftover data? Good. This replaces the current block completely.
                    //         ReplaceBlock(landedInBlock, replacement);
                    //         stretchesToBlock = landedInBlock = replacement;
                    //     }
                    //     else
                    //     {
                    //         // old block has leftover content.
                    //         if(stretchesToBlock != landedInBlock)
                    //             throw new InvalidOperationException("This should never happen!");
                    //         int num = WordCountByDistance(landedInBlock.Block, block.End);
                    //         // split the current block
                    //         InsertBefore(stretchesToBlock, replacement);
                    //         landedInBlock = replacement;
                    //         if (num <= 0)
                    //         {
                    //             // and we even have a gap leftover...
                    //             num = 0;
                    //             newGapLength = Math.Max(0, (stretchesToBlock.Block.Start + stretchesToBlock.Block.Gap )-block.End);
                    //         }

                    //         replacement = new LinkedBlock(new DataStreak(block.End, stretchesToBlock.Block.End, newGapLength));
                    //         for(int i = num; i < stretchesToBlock.Block.Data.Count; i++)
                    //             replacement.Block.Data.Add(stretchesToBlock.Block.Data[i]);
                    //         ReplaceBlock(stretchesToBlock, replacement);
                    //         stretchesToBlock = landedInBlock;   // no cleanup needed here...
                    //     }
                    // }
                    // else
                    // {
                    //     // *maybe* in data section...
                    //     // if(block.Start + TapeDrive.Bit0ClockEveryInch > landedInBlock.Block.End)
                    //     // {
                    //     //     if (landedInBlock._Next==null)
                    //     //     {
                    //     //         // just append...
                    //     //         landedInBlock._Next = new LinkedBlock(block);
                    //     //         landedInBlock._Next._Prev = landedInBlock;
                    //     //         stretchesToBlock = landedInBlock;   // no cleanup...
                    //     //     }
                    //     //     else
                    //     //     {
                    //     //         // tolerance is good, this is just the next block... tweak the gap anyhow, just in case.
                    //     //         var replacement = new LinkedBlock(new DataStreak(landedInBlock.Block.End, block.End, block.Gap) { Data = block.Data});

                    //     //         landedInBlock.Block.End = block.Start;
                    //     //         // link to the next block as the new one. The landed block will be 
                    //     //         landedInBlock = landedInBlock._Next = new LinkedBlock() { _Prev = landedInBlock, Block = block };
                    //     //     }
                    //     // }
                    //     // else
                    //     {
                    //         // uh, oh... we landed in the data section...
                    //         // trim data...
                    //         int nWords = WordCountByDistance(landedInBlock.Block, block.Start);
                    //         if (nWords == 0)
                    //         {
                    //             // no words left, pretend we landet in gap anyhwo.
                    //             var newGap = (block.Start + block.Gap) - landedInBlock.Block.Start;

                    //             if (landedInBlock.Block.End < block.End)
                    //             {
                    //                 // fully replaces the block in question...
                    //                 if (stretchesToBlock == landedInBlock)
                    //                     throw new InvalidOperationException("This should never happen!");
                    //                 var replacement = new LinkedBlock(new DataStreak(landedInBlock.Block.Start, block.End, newGap) { Data = block.Data });
                    //                 ReplaceBlock(landedInBlock, replacement);
                    //                 landedInBlock = replacement;
                    //             }
                    //             else
                    //             {
                    //                 // only partially replaces the block, but still no words left; expand the gap of the following block if any.
                    //                 if(stretchesToBlock != landedInBlock)
                    //                     throw new InvalidOperationException("This should never happen!");
                    //                 // split the current block

                    //                 stretchesToBlock = landedInBlock._Next = new LinkedBlock()
                    //                 {
                    //                     _Next = landedInBlock._Next,
                    //                     _Prev = landedInBlock,
                    //                     Block = new DataStreak()
                    //                     {
                    //                         Start = block.End,
                    //                         Gap = 0,
                    //                         Data = new System.Collections.Generic.List<ushort>(),
                    //                         End = landedInBlock.Block.End
                    //                     }
                    //                 };
                    //                 if (stretchesToBlock._Next != null)
                    //                     stretchesToBlock._Next._Prev = stretchesToBlock;
                    //                 for(int i = num; i < landedInBlock.Block.Data.Count; i++)
                    //                     stretchesToBlock.Block.Data.Add(landedInBlock.Block.Data[i]);
                    //                 landedInBlock.Block.Data = block.Data;
                    //             }
                    //         }
                    //         else
                    //         {
                    //             // cut out beginning of block...
                    //             landedInBlock.Block.Data.RemoveRange(nWords, landedInBlock.Block.Data.Count - nWords);
                    //             landedInBlock.Block.End = block.Start;
                    //             // link to the next block as the new one. The landed block will be 
                    //             landedInBlock = landedInBlock._Next = new LinkedBlock() { _Prev = landedInBlock, Block = block };
                    //         }
                    //     }
                    // }
                    // // now fix up the "next" block...
                    // if (stretchesToBlock == null)
                    //     landedInBlock._Next = null; // easy...
                    // else
                    // {
                    //     if(landedInBlock != stretchesToBlock)
                    //     {
                    //         // link up the two ends of the string, snipping out any intermediates...
                    //         landedInBlock._Next = stretchesToBlock;
                    //         stretchesToBlock._Prev = landedInBlock;
                    //         // did we land in the gap of the target block?
                    //         if(landedInBlock.Block.End < stretchesToBlock.Block.Start + stretchesToBlock.Block.Gap)
                    //         {
                    //             // yes. Update gap length...
                    //             ReplaceBlock(stretchesToBlock, new LinkedBlock(new DataStreak(landedInBlock.Block.End, stretchesToBlock.Block.End, (stretchesToBlock.Block.Start + stretchesToBlock.Block.Gap) - landedInBlock.Block.End) { Data = landedInBlock.Block.Data}));
                    //             // Data length is the same, just trimmed the gap...
                    //         }
                    //         else
                    //         {
                    //             // we are in the data section. Remove gap and trim data...
                    //             int numWords = WordCountByDistance(stretchesToBlock.Block, landedInBlock.Block.End) + 1;    // make sure we use "ceiling" here...
                    //             if (numWords == 0)  // no more data left?!
                    //             {
                    //                 landedInBlock._Next = stretchesToBlock._Next;
                    //                 if (landedInBlock._Next != null)
                    //                 {
                    //                     landedInBlock._Next._Prev = landedInBlock;
                    //                     // rely on the optimization step to fix up the start/end marks...
                    //                 }
                    //             }
                    //             else
                    //             {
                    //                 block = new DataStreak(landedInBlock.Block.End, stretchesToBlock.Block.End, 0) { Data = stretchesToBlock.Block.Data };
                    //                 var replacement = new LinkedBlock(block);
                    //                 if (numWords > 0)
                    //                 {
                    //                     if (numWords >= block.Data.Count)
                    //                     {
                    //                         block.Data.Clear();
                    //                     }
                    //                     else
                    //                     {
                    //                         block.Data.RemoveRange(0,numWords);
                    //                     }
                    //                 }
                    //                 ReplaceBlock(stretchesToBlock, replacement);
                    //             }
                    //         }
                    //     }
                    // }
                }
                else
                {
                    // got a new block for the end of the chain. Link offsets too.
                    double delta = block.Start - x.Block.End;
                    if (delta != 0)
                    {
                        // stretch new beginning section..
                        block = new DataStreak(block.Start-delta, block.End, block.Gap + delta) { Data = block.Data };
                    }
                    x._Next = new LinkedBlock(block)
                    {
                        _Prev = x,
                    };
                }
            }
            _Tracks[recordOnHead] = OptimizeChain(_Tracks[recordOnHead]);
            // clear cached blocks because we might have modified the linked list and forgotten the ones we had...
            _CachedLastBlock[0] = null;
            _CachedLastBlock[1] = null;
            DumpTrackDebug(_Tracks[recordOnHead]);
        }

        private void InsertAfter(LinkedBlock landedInBlock, LinkedBlock newBlock)
        {
            throw new NotImplementedException();
        }

        private void InsertBefore(LinkedBlock target, LinkedBlock newBlock)
        {
            if (newBlock._Next != null || newBlock._Prev != null)
                throw new InvalidOperationException();
            newBlock._Next = target;
            newBlock._Prev = target._Prev;
            if (newBlock._Prev!=null)
                newBlock._Prev._Next = newBlock;
            if (newBlock._Next != null)
                newBlock._Next._Prev = newBlock;
            if (_Tracks[0] == target)
                _Tracks[0] = newBlock;
            if (_Tracks[1] == target)
                _Tracks[1] = newBlock;
        }

        private void ReplaceBlock(LinkedBlock thisBlock, LinkedBlock newBlock)
        {
            if (newBlock._Next != null || newBlock._Prev != null)
                throw new InvalidOperationException();
            newBlock._Prev = thisBlock._Prev;
            newBlock._Next = thisBlock._Next;
            if (newBlock._Prev!=null)
                newBlock._Prev._Next = newBlock;
            if (newBlock._Next != null)
                newBlock._Next._Prev = newBlock;
            if (_Tracks[0] == thisBlock)
                _Tracks[0] = newBlock;
            if (_Tracks[1] == thisBlock)
                _Tracks[1] = newBlock;
        }

        /// <summary>
        /// Optimizes (compresses) a block chain to have the unified "start/gap/data/end=start..." format.
        /// </summary>
        /// <param name="linkedBlock">The first element of the original chain.</param>
        /// <returns>A new sequence of linked blocks; null if only a "gap" remained.</returns>
        private LinkedBlock? OptimizeChain(LinkedBlock? root)
        {
            if (root == null)
                return null;

            var now = root;
            while (now != null)
            {
                if (now.Block.Data.Count == 0 && now._Next != null)  // gap only block, got a next one...
                {
                    // merge next gap+data into current block...
                    var block = new DataStreak(now.Block.Start, now._Next.Block.End, now.Block.End + now._Next.Block.Gap)
                    {
                        Data = now._Next.Block.Data
                    };
                    var newBlock = new LinkedBlock(block);
                    // jump over next block
                    newBlock._Next = now._Next._Next;
                    newBlock._Prev = now._Prev;
                    if (newBlock._Next != null)
                        newBlock._Next._Prev = newBlock;
                    if (newBlock._Prev != null)
                        newBlock._Prev._Next = newBlock;
                    else
                    {
                        // we are root!
                        root = newBlock;
                    }
                    now = newBlock;
                    continue;   // try again...
                }
                now = now._Next;
            }

            // last check...
            if (root._Next == null && root._Prev == null && root.Block.Data.Count == 0)
                return null;    
            return root;
        }

        private void DumpTrackDebug(LinkedBlock? linkedBlock)
        {
            StringBuilder sb = new StringBuilder();
            while (linkedBlock != null)
            {
                if (sb.Length>0)
                    sb.Append("->-");
                sb.AppendFormat("{0:0.0000} |{1:0.0000}| [{2}] {3:0.0000}", linkedBlock.Block.Start, linkedBlock.Block.Gap, linkedBlock.Block.Data.Count, linkedBlock.Block.End);
                linkedBlock = linkedBlock._Next;
            }
            Debug.WriteLine(sb.ToString());
        }

        private int WordCountByDistance(DataStreak block, double positionInBlock)
        {
            if (block.Data.Count== 0 || positionInBlock < (block.Start+block.Gap))
                return 0;
            double distancePerWord = (block.End - block.Start - block.Gap) / block.Data.Count;
            return (int)Math.Floor((positionInBlock - (block.Start+block.Gap)) / distancePerWord);
        }

        // for faster access: remember the last used block per track...
        private LinkedBlock?[] _CachedLastBlock = new LinkedBlock[2];

        internal DataStreak? GetBlockAt(int selectedHead, double posNow)
        {
            if (_Tracks[selectedHead] == null)  // do we have a block at all?
                return null;
            var temp = _CachedLastBlock[selectedHead];
            if (temp!= null && temp.Block.Start <= posNow && temp.Block.End > posNow)
                return temp.Block;    // cache hit.
            if (temp == null)
            {
                // start serach...
                temp = _Tracks[selectedHead];
                while (temp != null && (temp.Block.Start > posNow || temp.Block.End <= posNow))
                    temp =temp._Next;
                if (temp != null)
                {
                    return (_CachedLastBlock[selectedHead] = temp).Block;
                }
                return null;
            }
            if (temp.Block.Start > posNow)
            {
                while (temp != null && temp.Block.Start > posNow)
                {
                    temp=temp._Prev;
                    if (temp == null)
                    {
                        _CachedLastBlock[selectedHead] = null;
                        return null;
                    }
                }
                _CachedLastBlock[selectedHead] = temp;
                return temp?.Block;
            }
            while (temp != null && temp.Block.End <= posNow)
            {
                temp=temp._Next;
                if (temp == null)
                {
                    _CachedLastBlock[selectedHead] = null;
                    return null;
                }
            }
            _CachedLastBlock[selectedHead] = temp;
            return temp?.Block;
        }

        /// <summary>
        /// Reads a saved cartridge into memory.
        /// </summary>
        /// <param name="filename">The file (base) name to read.</param>
        /// <param name="package">Null to auto-detect, true to assume a single-file package, false to assume a multi-file tape.</param>
        /// <returns>The loaded tape. Throws on error.</returns>
        public static async Task<TapeCartridge> Load(string filename, bool? package = null)
        {
            if (!package.HasValue)
            {
                package = IsTapeFileArchive(filename);
            }
            var c = new TapeCartridge();
            if (package.Value)
                await c.LoadFromPackage(filename);
            else
                await c.LoadFromFileStructure(filename);
            return c;
        }

        private async Task LoadFromFileStructure(string filename)
        {
            using(var tr = File.OpenText(filename))
            {
                await ReadMetadata(tr);
            }
            var temp = Path.ChangeExtension(filename, ".trk-a");
            if (File.Exists(temp))
                using (var trk = File.OpenRead(temp))
                {
                    _Tracks[0] = await ReadTrack(trk);
                }
            else
                _Tracks[0] = null;

            temp = Path.ChangeExtension(filename, ".trk-b");
            if (File.Exists(temp))
                using (var trk = File.OpenRead(temp))
                {
                    _Tracks[1] = await ReadTrack(trk);
                }
            else
                _Tracks[1] = null;

            FixupMetadata();
        }

        private async Task<LinkedBlock?> ReadTrack(Stream trk)
        {
            LinkedBlock? first = null;
            LinkedBlock? last = null;
            using(var b = new BinaryReader(trk))
            {
                while(true)
                {
                    var temp = b.ReadByte();
                    switch(temp)
                    {
                        case 0:
                            return first;
                        case 1:
                            double start, gap, end;
                            start = b.ReadDouble();
                            gap = b.ReadDouble();
                            end = b.ReadDouble();
                            var block = new DataStreak(start, end, gap);

                            if (first == null)
                                first = new LinkedBlock(block);
                            var x = last;
                            if (last == null)
                                last = first;
                            else
                                last = last._Next = new LinkedBlock(block);
                            last._Prev = x;
                            int numWords = b.ReadInt32();
                            for(int i = 0; i< numWords;i++)
                            {
                                last.Block.Data.Add(b.ReadUInt16());
                            }
                            break;
                        default:
                            throw new InvalidOperationException("Unknown marker byte in tape stream!");
                    }
                }
            }
        }

        private async Task LoadFromPackage(string filename)
        {
            using(var pack = System.IO.Packaging.Package.Open(filename, FileMode.Open))
            {
                var mdf = pack.GetPart(new Uri("/metadata", UriKind.Relative));
                using (var tr = new StreamReader(mdf.GetStream(FileMode.Open)))
                {
                    await ReadMetadata(tr);
                }
                using (var trk = pack.GetPart(new Uri("/track-a", UriKind.Relative)).GetStream(FileMode.Open))
                {
                    _Tracks[0] = await ReadTrack(trk);
                }
                using (var trk = pack.GetPart(new Uri("/track-b", UriKind.Relative)).GetStream(FileMode.Open))
                {
                    _Tracks[1] = await ReadTrack(trk);
                }
            }
            FixupMetadata();
        }

        private async Task ReadMetadata(StreamReader tr)
        {
            using (var x = XmlReader.Create(tr, new XmlReaderSettings() { Async = true }))
            {
                while (x.NodeType != XmlNodeType.Element && await x.ReadAsync()) ;
                if (x.NodeType != XmlNodeType.Element)
                    throw new InvalidOperationException("XML empty?!");
                if (x.NamespaceURI != MetaNamespace)
                    throw new InvalidOperationException($"The provided metadata is in the wrong namespace. Expected {MetaNamespace}, got {x.NamespaceURI}");
                if (x.LocalName != "tape")
                    throw new InvalidOperationException($"Invalid root element in metadata. Expected 'tape', got '{x.Name}'");

                var temp = x.GetAttribute("craeted", MetaNamespace);
                if (temp != null)
                    CreatedAt = DateTime.ParseExact(temp, "O", System.Globalization.CultureInfo.InvariantCulture);
                temp = x.GetAttribute("lastUsed", MetaNamespace);
                if (temp != null)
                    LastUsed = DateTime.ParseExact(temp, "O", System.Globalization.CultureInfo.InvariantCulture);
                temp = x.GetAttribute("lastWritten", MetaNamespace);
                if (temp != null)
                    LastWritten = DateTime.ParseExact(temp, "O", System.Globalization.CultureInfo.InvariantCulture);
                else
                    LastWritten = null;
                temp = x.GetAttribute("length", MetaNamespace);
                if (temp == null)
                    throw new InvalidOperationException("Missing 'length' attribute in 'tape' element!");
                Length = double.Parse(temp, System.Globalization.CultureInfo.InvariantCulture);

                temp = x.GetAttribute("position", MetaNamespace);
                if (temp != null)
                    Position = double.Parse(temp, System.Globalization.CultureInfo.InvariantCulture);
                else    
                    Position = 0;
                temp = x.GetAttribute("label", MetaNamespace);
                Label = temp ?? "?";
                int d = x.Depth;

                while ((await x.ReadAsync()) && !(x.NodeType == XmlNodeType.Element && x.NamespaceURI == MetaNamespace && x.LocalName == "comment" && x.Depth == d+1))
                    ;
                if (x.LocalName == "comment")
                    Comment = await x.ReadContentAsStringAsync();
                else
                    Comment = null;

                // TODO: read track content index?
            }
       }

        private static bool IsTapeFileArchive(string filename)
        {
            using(var br = new BinaryReader(File.OpenRead(filename)))
            {
                if (br.ReadByte()!=0x50)
                    return false;
                if (br.ReadByte()!=0x4b)
                    return false;
                if (br.ReadByte() != 0x3)
                    return false;
                if (br.ReadByte() != 0x4)
                    return false;
                return true;
            }
        }

        private class FileEntry
        {
            public int Index { get; internal set; }
            public int FileSize { get; internal set; }
            public int UsedSize { get; internal set; }
            public int Type { get; internal set; }
            public int Generation { get; internal set; }
            public int Checksum { get; internal set; }

            public FileType TranslatedType { get => Type >= 0 && Type <= 6 ? (FileType)Type : FileType.Unknown; }
        }
    }

    public enum FileType
    {
        Null = 0,
        BinaryProgram = 1,
        NumericData = 2,
        StringOrMixed = 3,
        MemoryFile= 4,
        KeyFile = 5,
        UserProgram = 6,
        Unknown = 7
    }
}