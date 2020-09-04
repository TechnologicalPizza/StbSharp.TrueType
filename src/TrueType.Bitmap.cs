using System;
using System.Numerics;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class TrueType
    {
        public static byte[]? GetGlyphBitmap(
            FontInfo info, Vector2 scale, int glyph,
            out int width, out int height, out IntPoint offset)
        {
            return GetGlyphBitmapSubpixel(
                info, scale, Vector2.Zero, glyph, out width, out height, out offset);
        }

        public static byte[]? GetGlyphBitmapSubpixel(
            FontInfo info, Vector2 scale, Vector2 shift, int glyph,
            out int width, out int height, out IntPoint offset)
        {
            if (scale.X == 0)
                scale.X = scale.Y;
            if (scale.Y == 0)
            {
                if (scale.X == 0)
                {
                    width = 0;
                    height = 0;
                    offset = IntPoint.Zero;
                    return null;
                }
                scale.Y = scale.X;
            }

            if (!GetGlyphBitmapBoxSubpixel(info, glyph, scale, shift, out var glyphBox))
            {
                width = default;
                height = default;
                offset = default;
                return null;
            }

            var gbm = new Bitmap();
            gbm.w = glyphBox.W;
            gbm.h = glyphBox.H;

            width = gbm.w;
            height = gbm.h;
            offset = glyphBox.Position;

            if (gbm.w != 0 && gbm.h != 0)
            {
                int pixelBytes = gbm.w * gbm.h;
                var pixels = new byte[pixelBytes];

                gbm.pixels = pixels;
                gbm.pixels.Fill(0);
                gbm.stride = gbm.w;

                int num_verts = GetGlyphShape(info, glyph, out Vertex[]? vertices);
                Rasterize(
                    gbm, 0.35f, vertices.AsSpan(0, num_verts), scale, shift,
                    glyphBox.Position, IntPoint.Zero, invert: true);

                return pixels;
            }
            return null;
        }

        public static bool GetGlyphBitmapBoxSubpixel(
            FontInfo font, int glyph, Vector2 scale, Vector2 shift, out IntRect glyphBox)
        {
            if (GetGlyphBox(font, glyph, out var fBox))
            {
                var br = fBox.BottomRight;
                glyphBox = IntRect.FromEdgePoints(
                    tlX: (int)Math.Floor(fBox.X * scale.X + shift.X),
                    tlY: (int)Math.Floor(-br.Y * scale.Y + shift.Y),
                    brX: (int)Math.Ceiling(br.X * scale.X + shift.X),
                    brY: (int)Math.Ceiling(-fBox.Y * scale.Y + shift.Y));
                return true;
            }
            glyphBox = IntRect.Zero;
            return false;
        }

        public static bool GetGlyphBitmapBox(
            FontInfo font, int glyph, Vector2 scale, out IntRect glyphBox)
        {
            return GetGlyphBitmapBoxSubpixel(font, glyph, scale, Vector2.Zero, out glyphBox);
        }

        public static bool GetCodepointBitmapBoxSubpixel(
            FontInfo font, int codepoint, Vector2 scale, Vector2 shift, out IntRect glyphBox)
        {
            int glyph = FindGlyphIndex(font, codepoint);
            return GetGlyphBitmapBoxSubpixel(font, glyph, scale, shift, out glyphBox);
        }

        public static bool GetCodepointBitmapBox(
            FontInfo font, int codepoint, Vector2 scale, out IntRect glyphBox)
        {
            return GetCodepointBitmapBoxSubpixel(
                font, codepoint, scale, Vector2.Zero, out glyphBox);
        }

        public static void MakeGlyphBitmapSubpixel(
            FontInfo info, Span<byte> output, int width, int height, int stride,
            Vector2 scale, Vector2 shift, IntPoint pixelOffset, int glyph)
        {
            GetGlyphBitmapBoxSubpixel(info, glyph, scale, shift, out var glyphBox);

            var gbm = new Bitmap();
            gbm.pixels = output;
            gbm.w = width;
            gbm.h = height;
            gbm.stride = stride;

            if (gbm.w != 0 && gbm.h != 0)
            {
                int num_verts = GetGlyphShape(info, glyph, out Vertex[]? vertices);

                Rasterize(
                    gbm, 0.35f, vertices.AsSpan(0, num_verts), scale, shift,
                    glyphBox.Position, pixelOffset, true);
            }
        }

        public static void MakeGlyphBitmap(
            FontInfo info, Span<byte> output, int width, int height, int stride,
            Vector2 scale, IntPoint pixelOffset, int glyph)
        {
            MakeGlyphBitmapSubpixel(
                info, output, width, height, stride, scale, Vector2.Zero, pixelOffset, glyph);
        }

        public static void MakeCodepointBitmapSubpixelPrefilter(
            FontInfo info, Span<byte> output, int width, int height, int stride,
            Vector2 scale, Vector2 shift, IntPoint pixelOffset,
            IntPoint oversample, out Vector2 sub, int codepoint)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            MakeGlyphBitmapSubpixelPrefilter(
                info, output, width, height, stride,
                scale, shift, pixelOffset,
                oversample, out sub, glyph);
        }

        public static void MakeCodepointBitmapSubpixel(
            FontInfo info, Span<byte> output, int width, int height, int stride,
            Vector2 scale, Vector2 shift, IntPoint pixelOffset, int codepoint)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            MakeGlyphBitmapSubpixel(
                info, output, width, height, stride, scale, shift, pixelOffset, glyph);
        }

        public static void MakeCodepointBitmap(
            FontInfo info, Span<byte> output, int width, int height, int stride,
            Vector2 scale, IntPoint pixelOffset, int codepoint)
        {
            MakeCodepointBitmapSubpixel(
                info, output, width, height, stride, scale, Vector2.Zero, pixelOffset, codepoint);
        }

        public static int BakeFontBitmap(
            ReadOnlyMemory<byte> fontData, int offset, Vector2 scale, Span<byte> pixels,
            int pw, int ph, int firstChar, Span<BakedChar> chardata)
        {
            var info = new FontInfo();
            if (!InitFont(info, fontData, offset))
                return -1;

            int x = 1;
            int y = 1;
            int bottom_y = 1;
            
            for (int i = 0; i < chardata.Length; ++i)
            {
                int g = FindGlyphIndex(info, firstChar + i);
                GetGlyphHMetrics(info, g, out int advance, out _);
                GetGlyphBitmapBox(info, g, scale, out var glyphBox);
                if ((x + glyphBox.W + 1) >= pw)
                {
                    y = bottom_y;
                    x = 1;
                }

                if ((y + glyphBox.H + 1) >= ph)
                    return -i;

                var pixelsSlice = pixels.Slice(x + y * pw);
                MakeGlyphBitmap(
                    info, pixelsSlice, glyphBox.W, glyphBox.H, pw, scale, IntPoint.Zero, g);

                chardata[i].x0 = (ushort)(short)x;
                chardata[i].y0 = (ushort)(short)y;
                chardata[i].x1 = (ushort)(short)(x + glyphBox.W);
                chardata[i].y1 = (ushort)(short)(y + glyphBox.H);
                chardata[i].xadvance = scale.X * advance;
                chardata[i].off.X = glyphBox.X;
                chardata[i].off.Y = glyphBox.Y;
                x = x + glyphBox.W + 1;
                if ((y + glyphBox.H + 1) > bottom_y)
                    bottom_y = y + glyphBox.H + 1;
            }

            return bottom_y;
        }

        public static void MakeGlyphBitmapSubpixelPrefilter(
            FontInfo info, Span<byte> output, int width, int height, int stride,
            Vector2 scale, Vector2 shift, IntPoint pixelOffset,
            IntPoint prefilter, out Vector2 sub, int glyph)
        {
            int bw = width - (prefilter.X - 1);
            int bh = height - (prefilter.Y - 1);
            MakeGlyphBitmapSubpixel(
                info, output, bw, bh, stride, scale, shift, pixelOffset, glyph);

            if (prefilter.X > 1)
                HorizontalPrefilter(output, width, height, stride, prefilter.X);
            if (prefilter.Y > 1)
                VerticalPrefilter(output, width, height, stride, prefilter.Y);

            sub.X = OversampleShift(prefilter.X);
            sub.Y = OversampleShift(prefilter.Y);
        }
    }
}
