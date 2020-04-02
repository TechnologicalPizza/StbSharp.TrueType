using System;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class StbTrueType
    {
        public static byte[] GetGlyphBitmap(
            TTFontInfo info, TTPoint scale, int glyph,
            out int width, out int height, out TTIntPoint offset)
        {
            return GetGlyphBitmapSubpixel(
                info, scale, TTPoint.Zero, glyph, out width, out height, out offset);
        }

        public static byte[] GetGlyphBitmapSubpixel(
            TTFontInfo info, TTPoint scale, TTPoint shift, int glyph, 
            out int width, out int height, out TTIntPoint offset)
        {
            if (scale.x == 0)
                scale.x = scale.y;
            if (scale.y == 0)
            {
                if (scale.x == 0)
                {
                    width = 0;
                    height = 0;
                    offset = TTIntPoint.Zero;
                    return null;
                }
                scale.y = scale.x;
            }

            GetGlyphBitmapBoxSubpixel(info, glyph, scale, shift, out var glyphBox);

            var gbm = new TTBitmap();
            gbm.w = glyphBox.w;
            gbm.h = glyphBox.h;

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

                int num_verts = GetGlyphShape(info, glyph, out TTVertex[] vertices);
                Rasterize(
                    gbm, 0.35f, vertices.AsSpan(0, num_verts), scale, shift,
                    glyphBox.Position, TTIntPoint.Zero, invert: true);

                return pixels;
            }
            return null;
        }

        public static bool GetGlyphBitmapBoxSubpixel(
            TTFontInfo font, int glyph, TTPoint scale, TTPoint shift, out TTIntRect glyphBox)
        {
            if (GetGlyphBox(font, glyph, out glyphBox))
            {
                var br = glyphBox.BottomRight;
                glyphBox = TTIntRect.FromEdgePoints(
                    tlX: (int)Math.Floor(glyphBox.x * scale.x + shift.x),
                    tlY: (int)Math.Floor(-br.y * scale.y + shift.y),
                    brX: (int)Math.Ceiling(br.x * scale.x + shift.x),
                    brY: (int)Math.Ceiling(-glyphBox.y * scale.y + shift.y));
                return true;
            }
            glyphBox = TTIntRect.Zero;
            return false;
        }

        public static bool GetGlyphBitmapBox(
            TTFontInfo font, int glyph, TTPoint scale, out TTIntRect glyphBox)
        {
            return GetGlyphBitmapBoxSubpixel(font, glyph, scale, TTPoint.Zero, out glyphBox);
        }

        public static bool GetCodepointBitmapBoxSubpixel(
            TTFontInfo font, int codepoint, TTPoint scale, TTPoint shift, out TTIntRect glyphBox)
        {
            int glyph = FindGlyphIndex(font, codepoint);
            return GetGlyphBitmapBoxSubpixel(font, glyph, scale, shift, out glyphBox);
        }

        public static bool GetCodepointBitmapBox(
            TTFontInfo font, int codepoint, TTPoint scale, out TTIntRect glyphBox)
        {
            return GetCodepointBitmapBoxSubpixel(font, codepoint, scale, TTPoint.Zero, out glyphBox);
        }

        public static void MakeGlyphBitmapSubpixel(
            TTFontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            TTPoint scale, TTPoint shift, TTIntPoint pixelOffset, int glyph)
        {
            int num_verts = GetGlyphShape(info, glyph, out TTVertex[] vertices);
            GetGlyphBitmapBoxSubpixel(info, glyph, scale, shift, out var glyphBox);

            var gbm = new TTBitmap();
            gbm.pixels = output;
            gbm.w = out_w;
            gbm.h = out_h;
            gbm.stride = out_stride;

            if (gbm.w != 0 && gbm.h != 0)
                Rasterize(
                    gbm, 0.35f, vertices.AsSpan(0, num_verts), scale, shift,
                    glyphBox.Position, pixelOffset, true);
        }

        public static void MakeGlyphBitmap(
            TTFontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            TTPoint scale, TTIntPoint pixelOffset, int glyph)
        {
            MakeGlyphBitmapSubpixel(
                info, output, out_w, out_h, out_stride, scale, TTPoint.Zero, pixelOffset, glyph);
        }

        public static void MakeCodepointBitmapSubpixelPrefilter(
            TTFontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            TTPoint scale, TTPoint shift, TTIntPoint pixelOffset,
            TTIntPoint oversample, out TTPoint sub, int codepoint)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            MakeGlyphBitmapSubpixelPrefilter(
                info, output, out_w, out_h, out_stride,
                scale, shift, pixelOffset,
                oversample, out sub, glyph);
        }

        public static void MakeCodepointBitmapSubpixel(
            TTFontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            TTPoint scale, TTPoint shift, TTIntPoint pixelOffset, int codepoint)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            MakeGlyphBitmapSubpixel(
                info, output, out_w, out_h, out_stride, scale, shift, pixelOffset, glyph);
        }

        public static void MakeCodepointBitmap(
            TTFontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            TTPoint scale, TTIntPoint pixelOffset, int codepoint)
        {
            MakeCodepointBitmapSubpixel(
                info, output, out_w, out_h, out_stride, scale, TTPoint.Zero, pixelOffset, codepoint);
        }

        public static int BakeFontBitmap(
            ReadOnlyMemory<byte> fontData, int offset, float pixel_height, Span<byte> pixels,
            int pw, int ph, int first_char, Span<TTBakedChar> chardata)
        {
            var info = new TTFontInfo();
            if (!InitFont(info, fontData, offset))
                return -1;

            int x = 1;
            int y = 1;

            int bottom_y = 1;
            var scale = ScaleForPixelHeight(info, pixel_height);

            for (int i = 0; i < chardata.Length; ++i)
            {
                int g = FindGlyphIndex(info, first_char + i);
                GetGlyphHMetrics(info, g, out int advance, out _);
                GetGlyphBitmapBox(info, g, scale, out var glyphBox);
                if ((x + glyphBox.w + 1) >= pw)
                {
                    y = bottom_y;
                    x = 1;
                }

                if ((y + glyphBox.h + 1) >= ph)
                    return -i;

                var pixelsSlice = pixels.Slice(x + y * pw);
                MakeGlyphBitmap(
                    info, pixelsSlice, glyphBox.w, glyphBox.h, pw, scale, TTIntPoint.Zero, g);

                chardata[i].x0 = (ushort)(short)x;
                chardata[i].y0 = (ushort)(short)y;
                chardata[i].x1 = (ushort)(short)(x + glyphBox.w);
                chardata[i].y1 = (ushort)(short)(y + glyphBox.h);
                chardata[i].xadvance = scale.x * advance;
                chardata[i].xoff = glyphBox.x;
                chardata[i].yoff = glyphBox.y;
                x = x + glyphBox.w + 1;
                if ((y + glyphBox.h + 1) > bottom_y)
                    bottom_y = y + glyphBox.h + 1;
            }

            return bottom_y;
        }

        public static void MakeGlyphBitmapSubpixelPrefilter(
            TTFontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            TTPoint scale, TTPoint shift, TTIntPoint pixelOffset,
            TTIntPoint prefilter, out TTPoint sub, int glyph)
        {
            int bw = out_w - (prefilter.x - 1);
            int bh = out_h - (prefilter.y - 1);
            MakeGlyphBitmapSubpixel(
                info, output, bw, bh, out_stride, scale, shift, pixelOffset, glyph);

            if (prefilter.x > 1)
                HorizontalPrefilter(output, out_w, out_h, out_stride, prefilter.x);
            if (prefilter.y > 1)
                VerticalPrefilter(output, out_w, out_h, out_stride, prefilter.y);

            sub.x = OversampleShift(prefilter.x);
            sub.y = OversampleShift(prefilter.y);
        }
    }
}
