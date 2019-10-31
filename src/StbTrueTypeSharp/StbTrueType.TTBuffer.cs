﻿using System;
using System.Runtime.InteropServices;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
	internal
#endif
    unsafe partial class StbTrueType
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TTBuffer
        {
            public static readonly TTBuffer Empty = EmptyWithLength(0);

            public ReadOnlyMemory<byte> data;
            public int size;
            public int cursor;

            public TTBuffer(ReadOnlyMemory<byte> data, int size)
            {
                this.data = data;
                this.size = size;
                cursor = 0;
            }

            public static TTBuffer EmptyWithLength(int length)
            {
                return new TTBuffer(ReadOnlyMemory<byte>.Empty, length);
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

            public TTBuffer Slice(int start, int length)
            {
                TTBuffer r = Empty;
                if ((start < 0) || (length < 0) || (start > size) || (length > (size - start)))
                    return r;

                r.data = data.Slice(start);
                r.size = length;
                return r;
            }
        }
    }
}
