using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HP9825CPU
{
    /// <summary>
    /// Encapsulates a "numeric" data file. This is file type "2" on the 9825.
    /// </summary>
    /// <remarks>
    /// <para>Note that the values of the file are stored in reverse order; i.e. if you do a "rcf A,B,C", the resulting file will have the variables as C-B-A. This class automatically flips that around again, to make access a bit easier.</para>
    /// <para>The array options provided are compatible with the arrays of the 9825; only it uses 1-based indexing, .NET uses 0-based indexing, so the indexes are "off by one" by design. But the order of rows and columns will match.
    /// </remarks>
    public class NumericFileContent
        : FileContent, IEnumerable<FloatingPointNumber>
    {
        private List<FloatingPointNumber> _Content = new List<FloatingPointNumber>();

        public NumericFileContent(int track, int fileIndex, int fileSize, int generation)
            : base(track, fileIndex, fileSize, FileType.NumericData, generation)
        {
        }

        /// <summary>
        /// Get the number of numbers stored in this file.
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
                _Content.Add(number);
            else
                throw new FileSizeExceededException("Numeric file overflow. File has {0} bytes limit, but adding new number pushes it to {1}!", SizeLimit, rSize);
        }

        /// <summary>
        /// Puts an array into the file.
        /// </summary>
        /// <param name="array">The source array.</param>
        /// <param name="targetIndex">The index of the first location for the array. Use null to append it to the end.</param>
        /// <param name="count">The number of elements in the array to put down. Null means: to the end.</param>
        /// <param name="startIndex">The first element of the array to put down.</param>
        public void PutArray(FloatingPointNumber[] array, int? targetIndex = null, int startIndex = 0, int? count = null)
        {

        }

        /// <summary>
        /// Puts an array into the file.
        /// </summary>
        /// <param name="array">The source array.</param>
        /// <param name="targetIndex">The index of the first location for the array. Use null to append it to the end.</param>
        public void PutArray(FloatingPointNumber[,] array, int? targetIndex = null)
        {
            
        }

        public override int UsedSize => _Content.Count * 8;

        public void GetArray(int startIndex, FloatingPointNumber[] target, int targetIndex = 0, int? count = null)
        {
            count ??= target.Length - targetIndex;
            if (targetIndex < 0 || targetIndex>= target.Length)
                throw new ArgumentOutOfRangeException(nameof(targetIndex), targetIndex, "The index is outside the available array size!");
            if (targetIndex + count >= target.Length || count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "The number of elements is negative or outside the array!");
            if (startIndex< 0 || startIndex + (count * 8) >= UsedSize)
                throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, "The requested range of the file is not possible!");
            for (int i = 0; i < count;i++)
            {
                target[i + targetIndex] = this[startIndex + i];
            }
        }

        public void GetArray(int startIndex, FloatingPointNumber[,] target)
        {
            int count = target.Length;
            if (startIndex< 0 || startIndex + (count * 8) >= UsedSize)
                throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, "The requested range of the file is not possible!");
            int arrayIndex = 0;
            // TODO: validate indexing rules...
            for (int line = 0; line < target.GetUpperBound(1);line++)
            {
                for(int col = 0; col < target.GetUpperBound(0); col++)
                {
                    target[col, line] = this[startIndex + arrayIndex];
                    arrayIndex++;
                }
            }
        }

        protected override async Task ExportContentNow(TextWriter f)
        {
            await f.WriteLineAsync(string.Format("#  {0} numbers", _Content.Count));
            await f.WriteLineAsync();
            for(int i=0;i<_Content.Count;i++)
            {
                if ((i % 5) == 0)
                    await f.WriteLineAsync(string.Format("#  {0}", i));
                await f.WriteLineAsync(_Content[i].ToString());
            }
        }

        // TODO: implement "n-dimensional" array with arbitrary bases. See O&P page 89: dim S[-3:0,4:6] for example...

        public void RemoveAt(int index)
        {
            _Content.RemoveAt(index);
        }

        public void Clear()
        {
            _Content.Clear();
        }

        public IEnumerator<FloatingPointNumber> GetEnumerator()
        {
            return _Content.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _Content.GetEnumerator();
        }

        protected override void ReadInputFile(ITapeFileReader input)
        {
            if (input.UsedSize % 4 != 0)
            {
                throw new InvalidOperationException("The file isn't a multiple of 4 words in length!");
            }
            while (!input.EndOfFile)
            {
                var a = input.ReadWord() ?? 0;
                var b = input.ReadWord() ?? 0;
                var c = input.ReadWord() ?? 0;
                var d = input.ReadWord() ?? 0;
                FloatingPointNumber num = FloatingPointNumber.FromParts(a,b,c,d);
                _Content.Add(num);
            }
            _Content.Reverse(); // file is stored back to front in original binary...
        }

        public FloatingPointNumber this[int index]
        {
            get
            {
                return _Content[index];
            }
            set
            {
                if(!value.IsValid)
                    throw new InvalidOperationException("The provided floating point value is invalid!");
                _Content[index] = value;
            }
        }
    }
}