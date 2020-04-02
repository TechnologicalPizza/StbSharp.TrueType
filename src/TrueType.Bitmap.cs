﻿using System;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class TrueType
    {
        public static byte[] GetGlyphBitmap(
            FontInfo info, Point scale, int glyph,
            out int width, out int height, out IntPoint offset)
        {
            return GetGlyphBitmapSubpixel(
                info, scale, Point.Zero, glyph, out width, out height, out offset);
        }

        public static byte[] GetGlyphBitmapSubpixel(
            FontInfo info, Point scale, Point shift, int glyph, 
            out int width, out int height, out IntPoint offset)
        {
            if (scale.x == 0)
                scale.x = scale.y;
            if (scale.y == 0)
            {
                if (scale.x == 0)
                {
                    width = 0;
                    height = 0;
                    offset = IntPoint.Zero;
                    return null;
                }
                scale.y = scale.x;
            }

            GetGlyphBitmapBoxSubpixel(info, glyph, scale, shift, out var glyphBox);

            var gbm = new Bitmap();
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

                int num_verts = GetGlyphShape(info, glyph, out Vertex[] vertices);
                Rasterize(
                    gbm, 0.35f, vertices.AsSpan(0, num_verts), scale, shift,
                    glyphBox.Position, IntPoint.Zero, invert: true);

                return pixels;
            }
            return null;
        }

        public static bool GetGlyphBitmapBoxSubpixel(
            FontInfo font, int glyph, Point scale, Point shift, out IntRect glyphBox)
        {
            if (GetGlyphBox(font, glyph, out glyphBox))
            {
                var br = glyphBox.BottomRight;
                glyphBox = IntRect.FromEdgePoints(
                    tlX: (int)Math.Floor(glyphBox.x * scale.x + shift.x),
                    tlY: (int)Math.Floor(-br.y * scale.y + shift.y),
                    brX: (int)Math.Ceiling(br.x * scale.x + shift.x),
                    brY: (int)Math.Ceiling(-glyphBox.y * scale.y + shift.y));
                return true;
            }
            glyphBox = IntRect.Zero;
            return false;
        }

        public static bool GetGlyphBitmapBox(
            FontInfo font, int glyph, Point scale, out IntRect glyphBox)
        {
            return GetGlyphBitmapBoxSubpixel(font, glyph, scale, Point.Zero, out glyphBox);
        }

        public static bool GetCodepointBitmapBoxSubpixel(
            FontInfo font, int codepoint, Point scale, Point shift, out IntRect glyphBox)
        {
            int glyph = FindGlyphIndex(font, codepoint);
            return GetGlyphBitmapBoxSubpixel(font, glyph, scale, shift, out glyphBox);
        }

        public static bool GetCodepointBitmapBox(
            FontInfo font, int codepoint, Point scale, out IntRect glyphBox)
        {
            return GetCodepointBitmapBoxSubpixel(font, codepoint, scale, Point.Zero, out glyphBox);
        }

        public static void MakeGlyphBitmapSubpixel(
            FontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            Point scale, Point shift, IntPoint pixelOffset, int glyph)
        {
            int num_verts = GetGlyphShape(info, glyph, out Vertex[] vertices);
            GetGlyphBitmapBoxSubpixel(info, glyph, scale, shift, out var glyphBox);

            var gbm = new Bitmap();
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
            FontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            Point scale, IntPoint pixelOffset, int glyph)
        {
            MakeGlyphBitmapSubpixel(
                info, output, out_w, out_h, out_stride, scale, Point.Zero, pixelOffset, glyph);
        }

        public static void MakeCodepointBitmapSubpixelPrefilter(
            FontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            Point scale, Point shift, IntPoint pixelOffset,
            IntPoint oversample, out Point sub, int codepoint)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            MakeGlyphBitmapSubpixelPrefilter(
                info, output, out_w, out_h, out_stride,
                scale, shift, pixelOffset,
                oversample, out sub, glyph);
        }

        public static void MakeCodepointBitmapSubpixel(
            FontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            Point scale, Point shift, IntPoint pixelOffset, int codepoint)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            MakeGlyphBitmapSubpixel(
                info, output, out_w, out_h, out_stride, scale, shift, pixelOffset, glyph);
        }

        public static void MakeCodepointBitmap(
            FontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            Point scale, IntPoint pixelOffset, int codepoint)
        {
            MakeCodepointBitmapSubpixel(
                info, output, out_w, out_h, out_stride, scale, Point.Zero, pixelOffset, codepoint);
        }

        public static int BakeFontBitmap(
            ReadOnlyMemory<byte> fontData, int offset, float pixel_height, Span<byte> pixels,
            int pw, int ph, int first_char, Span<BakedChar> chardata)
        {
            var info = new FontInfo();
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
                    info, pixelsSlice, glyphBox.w, glyphBox.h, pw, scale, IntPoint.Zero, g);

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
            FontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            Point scale, Point shift, IntPoint pixelOffset,
            IntPoint prefilter, out Point sub, int glyph)
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