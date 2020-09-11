using System;
using System.Runtime.InteropServices;

namespace StbSharp
{
    public partial class TrueType
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Buffer
        {
            public static Buffer Empty => default;

            public ReadOnlyMemory<byte> Data;
            public int Cursor;

            public int Size => Data.Length;

            public Buffer(ReadOnlyMemory<byte> data)
            {
                Data = data;
                Cursor = 0;
            }

            public byte PeekByte()
            {
                if (Cursor >= Size)
                    return 0;
                return Data.Span[Cursor];
            }

            public byte GetByte()
            {
                if (Cursor >= Size)
                    return 0;
                return Data.Span[Cursor++];
            }

            [CLSCompliant(false)]
            public uint Get(int n)
            {
                uint v = 0;
                for (int i = 0; i < n; i++)
                    v = (v << 8) | GetByte();
                return v;
            }

            public void Seek(int o)
            {
                Cursor = ((o > Size) || (o < 0)) ? Size : o;
            }

            public void Skip(int o)
            {
                Seek(Cursor + o);
            }

            public Buffer Slice(int start, int length)
            {
                return new Buffer(Data.Slice(start, length));
            }
        }
    }
}
