using System;

namespace HP9825CPU
{
    public class Memory
    {
        private int[] _Contents;
        private int _Offset;

        private Memory(int offs, int len)
        {
            _Contents = new int[len];
            _Offset = offs;
        }

        public void LoadDual8Bit(System.IO.BinaryReader sourceLow, System.IO.BinaryReader sourceHigh, int from, int length)
        {
            while (length > 0 && from < _Contents.Length)
            {
                int value = (int)sourceLow.ReadByte() | (((int)sourceHigh.ReadByte()) << 8);
                this[from] = value;
                length--;
                from++;
            }
        }

        public void Load16Bit(System.IO.BinaryReader source, int from, int length, bool bigEndian = true)
        {
            int l, h;
            while (length > 0)
            {
                if (bigEndian)
                {
                    h = source.ReadByte();
                    l = source.ReadByte();
                }
                else
                {
                    l = source.ReadByte();
                    h = source.ReadByte();
                }
                int value = l | (h << 8);
                this[from] = value;
                length--;
                from++;
            }
        }


        public void DumpDual8Bit(System.IO.BinaryWriter targetLow, System.IO.BinaryWriter targetHigh, int from, int length)
        {
            while (from < _Offset && length > 0)
            {
                targetHigh.Write((byte)0);
                targetLow.Write((byte)0);
                length--;
                from++;
            }
            while (length > 0 && from < _Contents.Length)
            {
                targetLow.Write((byte)(_Contents[from] & 0xFF));
                targetHigh.Write((byte)((_Contents[from] >> 8) & 0xFF));
                length--;
                from++;
            }
            while (length > 0)
            {
                targetHigh.Write((byte)0);
                targetLow.Write((byte)0);
                length--;
            }
        }

        public void Dump16Bit(System.IO.BinaryWriter target, int from, int length, bool bigEndian = true)
        {
            //Console.WriteLine("Writing {0} words from {1} in buffer from {2} with {3} words", length, from, _Offset, _Contents.Length);
            while (from < _Offset && length > 0)
            {
                target.Write((ushort)0);
                length--;
                from++;
            }
            while (length > 0 && from < _Contents.Length)
            {
                if (bigEndian)
                {
                    target.Write((byte)((_Contents[from] >> 8) & 0xFF));
                    target.Write((byte)(_Contents[from] & 0xFF));
                }
                else
                {
                    target.Write((byte)(_Contents[from] & 0xFF));
                    target.Write((byte)((_Contents[from] >> 8) & 0xFF));
                }
                length--;
                from++;
            }
            while (length > 0)
            {
                target.Write((ushort)0);
                length--;
            }
        }

        public int Length
        {
            get
            {
                return _Contents.Length;
            }
        }

        public static Memory MakeMemory(bool use16Bit, int startOffset = 0, int? blockSize = null)
        {
            int maxSize = use16Bit ? 0x10000 : 0x8000;
            if (startOffset < 0 || startOffset + 1 >= maxSize)
                throw new ArgumentOutOfRangeException(nameof(startOffset), startOffset, "Lower bound out of range!");
            int size = blockSize.GetValueOrDefault(maxSize);
            if (startOffset + size > maxSize)
                throw new ArgumentOutOfRangeException(nameof(blockSize), blockSize, "Size too large!");
            return new Memory(startOffset, size);
        }

        public bool Contains(int address)
        {
            return address >= _Offset && (address - _Offset < _Contents.Length);
        }

        public int this[int index]
        {
            get { return _Contents[index - _Offset]; }
            set
            {
                if (value < 0 || value > 0xFFFF)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "16 bit only!");
                _Contents[index - _Offset] = value;
            }
        }

    }
}