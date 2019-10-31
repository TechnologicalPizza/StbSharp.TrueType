using System;
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
        public unsafe class TTPackContext
        {
            public int h_oversample;
            public int v_oversample;
            public int height;
            public void* nodes;
            public StbRectPack.RPContext pack_info;
            public int padding;
            public int skip_missing;
            public int stride_in_bytes;
            public int width;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTBakedChar
        {
            public ushort x0;
            public ushort y0;
            public ushort x1;
            public ushort y1;
            public float xoff;
            public float yoff;
            public float xadvance;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTAlignedQuad
        {
            public float x0;
            public float y0;
            public float s0;
            public float t0;

            public float x1;
            public float y1;
            public float s1;
            public float t1;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTPackedChar
        {
            public ushort x0;
            public ushort y0;
            public ushort x1;
            public ushort y1;

            public float xoff;
            public float yoff;
            public float xadvance;
            public float xoff2;
            public float yoff2;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTPackRange
        {
            public float font_size;
            public int first_unicode_codepoint_in_range;
            public int* array_of_unicode_codepoints;
            public Memory<TTPackedChar> chardata_for_range;
            public byte h_oversample;
            public byte v_oversample;
        }

        public class TTFontInfo
        {
            public TTBuffer cff;
            public TTBuffer charstrings;
            public ReadOnlyMemory<byte> data;
            public TTBuffer fdselect;
            public TTBuffer fontdicts;
            public int fontstart;
            public int glyf;
            public int gpos;
            public TTBuffer gsubrs;
            public int head;
            public int hhea;
            public int hmtx;
            public int index_map;
            public int indexToLocFormat;
            public int kern;
            public int loca;
            public int numGlyphs;
            public TTBuffer subrs;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTVertex
        {
            public short x;
            public short y;
            public short cx;
            public short cy;
            public short cx1;
            public short cy1;
            public byte type;
            public byte padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        public ref struct TTBitmap
        {
            public int w;
            public int h;
            public int stride;
            public Span<byte> pixels;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTCharStringContext
        {
            public int bounds;
            public int started;
            public float first_x;
            public float first_y;
            public float x;
            public float y;
            public int min_x;
            public int max_x;
            public int min_y;
            public int max_y;
            public TTVertex* pvertices;
            public int num_vertices;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTEdge
        {
            public float x0;
            public float y0;
            public float x1;
            public float y1;
            public int invert;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTActiveEdge
        {
            public TTActiveEdge* next;

            public float fx;
            public float fdx;
            public float fdy;
            public float direction;
            public float sy;
            public float ey;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTPoint
        {
            public float X;
            public float Y;

            public static bool Equals(in TTPoint a, in TTPoint b)
            {
                return a.X == b.X && a.Y == b.Y;
            }
        }
    }
}
