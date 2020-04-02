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
            public TTIntPoint oversample;
            public StbRectPack.RPContext pack_info;
            public bool skip_missing;
            public int padding;
            public int stride_in_bytes;
            public int width;
            public int height;
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
            public TTPoint pos0;
            public float s0;
            public float t0;

            public TTPoint pos1;
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

            public TTPoint offset0;
            public float xadvance;
            public TTPoint offset1;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTPackRange
        {
            public float font_size;
            public int first_unicode_codepoint_in_range;
            public int* array_of_unicode_codepoints;
            public Memory<TTPackedChar> chardata_for_range;
            public byte oversample_x;
            public byte oversample_y;
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
            public int svg;
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
        public struct TTKerningEntry
        {
            public int glyph1;
            public int glyph2;
            public int advance;
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

            public void Set(byte type, int x, int y, int cx, int cy)
            {
                this.x = (short)x;
                this.y = (short)y;
                this.cx = (short)cx;
                this.cy = (short)cy;
                this.type = type;
            }
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
            public TTPoint firstPos;
            public TTPoint pos;
            public TTIntPoint min;
            public TTIntPoint max;
            public TTVertex[] pvertices;
            public int num_vertices;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTEdge
        {
            public TTPoint p0;
            public TTPoint p1;
            public bool invert;
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
        public struct TTIntRect
        {
            public static readonly TTIntRect Zero = new TTIntRect(0, 0, 0, 0);

            public int x;
            public int y;
            public int w;
            public int h;

            public TTIntPoint Position
            {
                get => new TTIntPoint(x, y);
                set
                {
                    x = value.x;
                    y = value.y;
                }
            }

            public TTIntPoint BottomRight => new TTIntPoint(x + w, y + h);

            public TTIntRect(int x, int y, int w, int h)
            {
                this.x = x;
                this.y = y;
                this.w = w;
                this.h = h;
            }

            public static TTIntRect FromEdgePoints(int tlX, int tlY, int brX, int brY)
            {
                return new TTIntRect(
                    x: tlX, 
                    y: tlY,
                    w: brX - tlX, 
                    h: brY - tlY);
            }

            public static TTIntRect FromEdgePoints(
                TTIntPoint topLeft, TTIntPoint bottomRight)
            {
                return FromEdgePoints(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTPoint
        {
            public static readonly TTPoint Zero = new TTPoint(0, 0);
            public static readonly TTPoint One = new TTPoint(1, 1);

            public float x;
            public float y;

            public TTPoint(float x, float y)
            {
                this.x = x;
                this.y = y;
            }

            public TTPoint(float value) : this(value, value)
            {
            }

            public static bool Equals(in TTPoint a, in TTPoint b) => a.x == b.x && a.y == b.y;

            public static TTPoint operator *(TTPoint a, TTPoint b) => new TTPoint(a.x * b.x, a.y * b.y);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTIntPoint
        {
            public static readonly TTIntPoint Zero = new TTIntPoint(0, 0);
            public static readonly TTIntPoint One = new TTIntPoint(1, 1);

            public int x;
            public int y;

            public TTIntPoint(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public TTIntPoint(int value) : this(value, value)
            {
            }

            public static bool Equals(in TTPoint a, in TTPoint b) => a.x == b.x && a.y == b.y;

            public static implicit operator TTPoint(TTIntPoint value) => new TTPoint(value.x, value.y);
        }
    }
}
