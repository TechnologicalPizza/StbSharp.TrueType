using System;
using System.Runtime.InteropServices;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class TrueType
    {
        public unsafe class PackContext
        {
            public IntPoint oversample;
            public StbRectPack.RPContext pack_info;
            public bool skip_missing;
            public int padding;
            public int stride_in_bytes;
            public int width;
            public int height;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BakedChar
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
        public struct AlignedQuad
        {
            public Point pos0;
            public float s0;
            public float t0;

            public Point pos1;
            public float s1;
            public float t1;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PackedChar
        {
            public ushort x0;
            public ushort y0;
            public ushort x1;
            public ushort y1;

            public Point offset0;
            public float xadvance;
            public Point offset1;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PackRange
        {
            public float font_size;
            public int first_unicode_codepoint_in_range;
            public int* array_of_unicode_codepoints;
            public Memory<PackedChar> chardata_for_range;
            public byte oversample_x;
            public byte oversample_y;
        }

        public class FontInfo
        {
            public Buffer cff;
            public Buffer charstrings;
            public ReadOnlyMemory<byte> data;
            public Buffer fdselect;
            public Buffer fontdicts;
            public int fontstart;
            public int glyf;
            public int gpos;
            public int svg;
            public Buffer gsubrs;
            public int head;
            public int hhea;
            public int hmtx;
            public int index_map;
            public int indexToLocFormat;
            public int kern;
            public int loca;
            public int numGlyphs;
            public Buffer subrs;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KerningEntry
        {
            public int glyph1;
            public int glyph2;
            public int advance;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Vertex
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
        public ref struct Bitmap
        {
            public int w;
            public int h;
            public int stride;
            public Span<byte> pixels;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CharStringContext
        {
            public int bounds;
            public int started;
            public Point firstPos;
            public Point pos;
            public IntPoint min;
            public IntPoint max;
            public Vertex[] pvertices;
            public int num_vertices;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Edge
        {
            public Point p0;
            public Point p1;
            public bool invert;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ActiveEdge
        {
            public ActiveEdge* next;

            public float fx;
            public float fdx;
            public float fdy;
            public float direction;
            public float sy;
            public float ey;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IntRect
        {
            public static readonly IntRect Zero = new IntRect(0, 0, 0, 0);

            public int x;
            public int y;
            public int w;
            public int h;

            public IntPoint Position
            {
                get => new IntPoint(x, y);
                set
                {
                    x = value.x;
                    y = value.y;
                }
            }

            public IntPoint BottomRight => new IntPoint(x + w, y + h);

            public IntRect(int x, int y, int w, int h)
            {
                this.x = x;
                this.y = y;
                this.w = w;
                this.h = h;
            }

            public static IntRect FromEdgePoints(int tlX, int tlY, int brX, int brY)
            {
                return new IntRect(
                    x: tlX, 
                    y: tlY,
                    w: brX - tlX, 
                    h: brY - tlY);
            }

            public static IntRect FromEdgePoints(
                IntPoint topLeft, IntPoint bottomRight)
            {
                return FromEdgePoints(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public static readonly Point Zero = new Point(0, 0);
            public static readonly Point One = new Point(1, 1);

            public float x;
            public float y;

            public Point(float x, float y)
            {
                this.x = x;
                this.y = y;
            }

            public Point(float value) : this(value, value)
            {
            }

            public static bool Equals(in Point a, in Point b) => a.x == b.x && a.y == b.y;

            public static Point operator *(Point a, Point b) => new Point(a.x * b.x, a.y * b.y);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IntPoint
        {
            public static readonly IntPoint Zero = new IntPoint(0, 0);
            public static readonly IntPoint One = new IntPoint(1, 1);

            public int x;
            public int y;

            public IntPoint(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public IntPoint(int value) : this(value, value)
            {
            }

            public static bool Equals(in Point a, in Point b) => a.x == b.x && a.y == b.y;

            public static implicit operator Point(IntPoint value) => new Point(value.x, value.y);
        }
    }
}
