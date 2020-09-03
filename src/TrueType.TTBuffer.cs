using System;
using System.Runtime.InteropServices;

namespace StbSharp
{
    public partial class TrueType
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Buffer
        {
            public static Buffer Empty { get; } = EmptyWithLength(0);

            public ReadOnlyMemory<byte> data;
            public int size;
            public int cursor;

            public Buffer(ReadOnlyMemory<byte> data, int size)
            {
                this.data = data;
                this.size = size;
                cursor = 0;
            }

            public static Buffer EmptyWithLength(int length)
            {
                return new Buffer(ReadOnlyMemory<byte>.Empty, length);
            }

            public byte PeekByte()
            {
                if (cursor >= size)
                    return 0;
                return data.Span[cursor];
            }

            public byte GetByte()
            {
                if (cursor >= size)
                    return 0;
                return data.Span[cursor++];
            }

            public uint Get(int n)
            {
                uint v = 0;
                for (int i = 0; i < n; i++)
                    v = (v << 8) | GetByte();
                return v;
            }

            public void Seek(int o)
            {
                cursor = ((o > size) || (o < 0)) ? size : o;
            }

            public void Skip(int o)
            {
                Seek(cursor + o);
            }

            public Buffer Slice(int start, int length)
            {
                Buffer r = Empty;
                if ((start < 0) || (length < 0) || (start > size) || (length > (size - start)))
                    return r;

                r.data = data.Slice(start);
                r.size = length;
                return r;
            }
        }
    }
}
