using System.Collections;
using System.Collections.Generic;

namespace HP9825CPU
{
    public class HPLProgram
        : IEnumerable<HPLLine>
    {
        public HPLProgram()
        {
            
        }

        List<HPLLine> _Lines = new List<HPLLine>();

        public int Lines { get => _Lines.Count; }

        public HPLLine? Fetch(int lineIndex)
        {
            if (lineIndex < 0  || lineIndex >= _Lines.Count)
                return null;
            return _Lines[lineIndex];
        }
        
        internal void Store(HPLLine line)
        {
            _Lines.Add(line);
            line.Number = _Lines.Count - 1;
        }

        public IEnumerator<HPLLine> GetEnumerator()
        {
            return _Lines.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _Lines.GetEnumerator();
        }
    }
}