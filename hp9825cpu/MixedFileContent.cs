using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HP9825CPU
{
    public class MixedFileContent
        : FileContent, IEnumerable<MixedContentPart>
    {
        public MixedFileContent(int? track, int? fileIndex, int fileSize, int generation) 
            : base(track, fileIndex, fileSize, FileType.StringOrMixed, generation)
        {
        }

        private List<MixedContentPart> _Content = new List<MixedContentPart>();

        /// <summary>
        /// Get the number of numbers or strings stored in this file.
        /// </summary>
        public int Count { get => _Content.Count; }

        /// <summary>
        /// Appends a new number to the end of the file.
        /// </summary>
        /// <param name="number">The number to add.</param>
        /// <exception cref="FileSizeExceededException">If the file size on tape is exceeded.</exception>
        public void Add(FloatingPointNumber number)
        {
            int rSize = (Count + 1) * 8;
            if (rSize <= this.SizeLimit)
                _Content.Add(new MixedContentPart(number));
            else
                throw new FileSizeExceededException("Numeric file overflow. File has {0} bytes limit, but adding new number pushes it to {1}!", SizeLimit, rSize);
        }

        /// <summary>
        /// Appends a new string to the end of the file.
        /// </summary>
        /// <param name="str">The string to add.</param>
        /// <param name="dimLength">The maximum (reserved) length for the string variable.</param>
        /// <exception cref="FileSizeExceededException">If the file size on tape is exceeded.</exception>
        public void Add(string str, int dimLength)
        {
            if (str.Length > dimLength)
                throw new ArgumentOutOfRangeException(nameof(dimLength), dimLength, "The requested maximum string length (dimLength) of the string is too small for the requested string!");
            
            var item = new MixedContentPart(str, dimLength);
            var rSize = UsedSize + item.StorageSize;

            if (rSize <= this.SizeLimit)
                _Content.Add(item);
            else
                throw new FileSizeExceededException("Mixed file overflow. File has {0} bytes limit, but adding new item pushes it to {1}!", SizeLimit, rSize);
        }

        public override int UsedSize => _Content.Sum(x=>x.StorageSize);

        protected override async Task ExportContentNow(TextWriter f)
        {
            await f.WriteLineAsync(string.Format("#  {0} entries", _Content.Count));
            await f.WriteLineAsync();
            for(int i=0;i<_Content.Count;i++)
            {
                if ((i % 5) == 0)
                    await f.WriteLineAsync(string.Format("#  {0}", i));
                var m=_Content[i];
                if (m.Number.HasValue)
                    await f.WriteLineAsync(m.Number.ToString());
                else
                {
                    await f.WriteAsync("\"");
                    await f.WriteAsync(m.String!.Replace("\"", "\"\""));
                    await f.WriteAsync("\"[");
                    await f.WriteAsync(m.DimLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    await f.WriteLineAsync("]");
                }
            }
        }

        protected override void ReadInputFile(ITapeFileReader input)
        {
            while (!input.EndOfFile)
            {
                var a = input.ReadWord() ?? 0;
                if(a == 9)
                {
                    // This seems to be a string...
                    var b = input.ReadWord() ?? 0;
                    if (b != 0)
                        throw new InvalidOperationException("Unexpected format in string?!");
                    var rLen = input.ReadWord() ?? 0;
                    var uLen = input.ReadWord() ?? 0;
                    int dimLength = rLen;

                    var sb = new StringBuilder(dimLength);

                    if (rLen % 2 != 0)
                        rLen++;
                    rLen /= 2;
                    while (rLen>0)
                    {
                        var xChars = input.ReadWord();
                        if (!xChars.HasValue)
                        {
                            throw new InvalidOperationException("End of record in string file!");
                        }
                        var c1 = (char)((xChars.Value >> 8) & 0xFF);
                        if (uLen > 0)
                        {
                            uLen --;
                            sb.Append(c1);
                        }
                        else
                            if (c1 != ' ')
                                Debug.WriteLine("Non-whitespace unused char: " + c1);
                        c1 = (char)(xChars.Value & 0xFF);
                        if (uLen > 0)
                        {
                            uLen --;
                            sb.Append(c1);
                        }
                        else
                            if (c1 != ' ')
                                Debug.WriteLine("Non-whitespace unused char: " + c1);

                        rLen--;
                    }
                    if (rLen != 0)
                        throw new InvalidOperationException("End of record in string file?!");
                    _Content.Add(new MixedContentPart(sb.ToString(), dimLength));
                }
                else
                {
                    var b = input.ReadWord() ?? 0;
                    var c = input.ReadWord() ?? 0;
                    var d = input.ReadWord() ?? 0;
                    FloatingPointNumber num = FloatingPointNumber.FromParts(a,b,c,d);
                    _Content.Add(new MixedContentPart(num));
                }
            }
            _Content.Reverse(); // file is stored back to front in original binary...
        }

        IEnumerator<MixedContentPart> IEnumerable<MixedContentPart>.GetEnumerator()
        {
            return _Content.GetEnumerator();
        }

        public IEnumerator GetEnumerator()
        {
            return _Content.GetEnumerator();
        }
    }
}