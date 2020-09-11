using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace StbSharp
{
    public partial class TrueType
    {
        public class FontInfo
        {
            public ReadOnlyMemory<byte> data;
            public Buffer cff;
            public Buffer charstrings;
            public Buffer fdselect;
            public Buffer fontdicts;
            public Buffer gsubrs;
            public Buffer subrs;
            public int fontindex;
            public int glyf;
            public int gpos;
            public int svg;
            public int head;
            public int hhea;
            public int hmtx;
            public int index_map;
            public int indexToLocFormat;
            public int kern;
            public int loca;
            public int numGlyphs;
        }

        public class PackContext
        {
            public IntPoint oversample;
            public RPContext pack_info;
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
            public Vector2 off;
            public float xadvance;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AlignedQuad
        {
            public Vector2 pos0;
            public Vector2 st0;

            public Vector2 pos1;
            public Vector2 st1;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PackedChar
        {
            public ushort x0;
            public ushort y0;
            public ushort x1;
            public ushort y1;

            public Vector2 offset0;
            public float xadvance;
            public Vector2 offset1;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PackRange
        {
            public float font_size;
            public int first_unicode_codepoint_in_range;
            public int[]? array_of_unicode_codepoints;
            public Memory<PackedChar> chardata_for_range;
            public byte oversample_x;
            public byte oversample_y;
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
            public short X;
            public short Y;
            public short cx;
            public short cy;
            public short cx1;
            public short cy1;
            public VertexType type;
            public byte padding;

            public void Set(VertexType type, int x, int y, int cx, int cy)
            {
                X = (short)x;
                Y = (short)y;
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
        public struct Edge
        {
            public Vector2 p0;
            public Vector2 p1;
            public bool invert;
        }

        public class ActiveEdge
        {
            public ActiveEdge? next;

            public float fx;
            public Vector2 fd;
            public float direction;
            public float sy;
            public float ey;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IntRect
        {
            public static IntRect Zero => default;

            public int X;
            public int Y;
            public int W;
            public int H;

            public IntPoint Position
            {
                readonly get => new IntPoint(X, Y);
                set
                {
                    X = value.X;
                    Y = value.Y;
                }
            }

            public IntPoint BottomRight => new IntPoint(X + W, Y + H);

            public IntRect(int x, int y, int w, int h)
            {
                X = x;
                Y = y;
                W = w;
                H = h;
            }

            public static IntRect FromEdgePoints(int tlX, int tlY, int brX, int brY)
            {
                return new IntRect(
                    x: tlX,
                    y: tlY,
                    w: brX - tlX,
                    h: brY - tlY);
            }

            public static IntRect FromEdgePoints(IntPoint topLeft, IntPoint bottomRight)
            {
                return FromEdgePoints(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public static Rect Zero => default;

            public float X;
            public float Y;
            public float W;
            public float H;

            public Vector2 Position
            {
                readonly get => new Vector2(X, Y);
                set
                {
                    X = value.X;
                    Y = value.Y;
                }
            }

            public Vector2 BottomRight => new Vector2(X + W, Y + H);

            public Rect(float x, float y, float w, float h)
            {
                X = x;
                Y = y;
                W = w;
                H = h;
            }

            public static Rect FromEdgePoints(float tlX, float tlY, float brX, float brY)
            {
                return new Rect(
                    x: tlX,
                    y: tlY,
                    w: brX - tlX,
                    h: brY - tlY);
            }

            public static Rect FromEdgePoints(in Vector2 topLeft, in Vector2 bottomRight)
            {
                return FromEdgePoints(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IntPoint
        {
            public static IntPoint Zero => default;
            public static IntPoint One { get; } = new IntPoint(1, 1);

            public int X;
            public int Y;

            public IntPoint(int x, int y)
            {
                X = x;
                Y = y;
            }

            public IntPoint(int value) : this(value, value)
            {
            }

            public static implicit operator Vector2(IntPoint value)
            {
                return new Vector2(value.X, value.Y);
            }
        }
    }
}
