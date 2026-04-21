using System.IO.Enumeration;
using CommandLineUtils;

namespace makefont
{
    internal class DefFileReader
        : IDisposable
    {
        private TextReader? _reader;

        public int LineNumber { get; private set; }
        public string Filename { get; private set; }

        private ReturnCodeGroup<FontError> _Errors;

        public DefFileReader(string fileName, ReturnCodeGroup<FontError> errors)
        {
            _reader = File.OpenText(fileName);
            LineNumber = 0;
            Filename = fileName;
            _Errors = errors;
        }

        public Exception Happened(FontError error, params object?[] args)
        {
            object?[] args2 = new object?[args.Length+2];
            args2[0] = Filename;
            args2[1] = LineNumber;
            Array.Copy(args, 0, args2, 2, args.Length);
            return _Errors.Happened(error, args2);
        }

        public async Task<string?> ReadLine(bool includeEmptyLines = false)
        {
            ObjectDisposedException.ThrowIf(_reader == null, this);
            while(true)
            {
                var s = await _reader.ReadLineAsync();
                if (s == null)  // forward EOF.
                    return null;
                LineNumber++;
                if (!string.IsNullOrWhiteSpace(s))
                {
                    if (!s.StartsWith('#')) // skip comment line...
                        return s;
                }
                else
                {
                    if (includeEmptyLines)
                        return string.Empty;
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_reader != null)
                    _reader.Dispose();
                _reader = null;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
    }
}