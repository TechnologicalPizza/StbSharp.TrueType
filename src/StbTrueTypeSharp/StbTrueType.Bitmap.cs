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

        public static void GetGlyphBitmapBoxSubpixel(
            TTFontInfo font, int glyph, float scale_x, float scale_y, float shift_x, float shift_y,
            out int ix0, out int iy0, out int ix1, out int iy1)
        {
            if (GetGlyphBox(font, glyph, out int x0, out int y0, out int x1, out int y1) == 0)
            {
                ix0 = 0;
                iy0 = 0;
                ix1 = 0;
                iy1 = 0;
            }
            else
            {
                ix0 = (int)Math.Floor(x0 * scale_x + shift_x);
                iy0 = (int)Math.Floor(-y1 * scale_y + shift_y);
                ix1 = (int)Math.Ceiling(x1 * scale_x + shift_x);
                iy1 = (int)Math.Ceiling(-y0 * scale_y + shift_y);
            }
        }

        public static void GetGlyphBitmapBox(
            TTFontInfo font, int glyph, float scale_x, float scale_y,
            out int ix0, out int iy0, out int ix1, out int iy1)
        {
            GetGlyphBitmapBoxSubpixel(
                font, glyph, scale_x, scale_y, 0f, 0f,
                out ix0, out iy0, out ix1, out iy1);
        }

        public static void GetCodepointBitmapBoxSubpixel(
            TTFontInfo font, int codepoint, float scale_x,
            float scale_y, float shift_x, float shift_y,
            out int ix0, out int iy0, out int ix1, out int iy1)
        {
            GetGlyphBitmapBoxSubpixel(
                font, FindGlyphIndex(font, codepoint),
                scale_x, scale_y, shift_x, shift_y,
                out ix0, out iy0, out ix1, out iy1);
        }

        public static void GetCodepointBitmapBox(
            TTFontInfo font, int codepoint, float scale_x, float scale_y,
            out int ix0, out int iy0, out int ix1, out int iy1)
        {
            GetCodepointBitmapBoxSubpixel(
                font, codepoint, scale_x, scale_y, 0f, 0f,
                out ix0, out iy0, out ix1, out iy1);
        }

        public static void FreeBitmap(byte* bitmap)
        {
            CRuntime.free(bitmap);
        }

        public static void MakeGlyphBitmapSubpixel(
            TTFontInfo info, Span<byte> output, int out_w, int out_h,
            int out_stride, float scale_x, float scale_y, float shift_x, float shift_y, int glyph)
        {
            int num_verts = GetGlyphShape(info, glyph, out TTVertex* vertices);
            try
            {
                GetGlyphBitmapBoxSubpixel(
                    info, glyph, scale_x, scale_y,
                    shift_x, shift_y, out int ix0, out int iy0, out _, out _);

                var gbm = new TTBitmap();
                gbm.pixels = output;
                gbm.w = out_w;
                gbm.h = out_h;
                gbm.stride = out_stride;
                if ((gbm.w != 0) && (gbm.h != 0))
                    Rasterize(
                        gbm, 0.35f, vertices, num_verts, scale_x,
                        scale_y, shift_x, shift_y, ix0, iy0, 1);
            }
            finally
            {
                if (vertices != null) 
                    FreeShape(vertices);
            }
        }

        public static void MakeGlyphBitmap(
            TTFontInfo info, Span<byte> output, int out_w, int out_h,
            int out_stride, float scale_x, float scale_y, int glyph)
        {
            MakeGlyphBitmapSubpixel(info, output, out_w, out_h, out_stride,
                scale_x, scale_y, 0f, 0f, glyph);
        }

        public static void MakeCodepointBitmapSubpixelPrefilter(
            TTFontInfo info, Span<byte> output, int out_w,
            int out_h, int out_stride, float scale_x, float scale_y, float shift_x, float shift_y,
            int oversample_x, int oversample_y, out float sub_x, out float sub_y, int codepoint)
        {
            MakeGlyphBitmapSubpixelPrefilter(
                info, output, out_w, out_h, out_stride,
                scale_x, scale_y, shift_x, shift_y, oversample_x,
                oversample_y, out sub_x, out sub_y, FindGlyphIndex(info, codepoint));
        }

        public static void MakeCodepointBitmapSubpixel(
            TTFontInfo info, Span<byte> output, int out_w, int out_h,
            int out_stride, float scale_x, float scale_y, float shift_x, float shift_y, int codepoint)
        {
            MakeGlyphBitmapSubpixel(info, output, out_w, out_h, out_stride,
                scale_x, scale_y, shift_x, shift_y,
                FindGlyphIndex(info, codepoint));
        }

        public static void MakeCodepointBitmap(
            TTFontInfo info, Span<byte> output, int out_w, int out_h,
            int out_stride, float scale_x, float scale_y, int codepoint)
        {
            MakeCodepointBitmapSubpixel(
                info, output, out_w, out_h, out_stride, scale_x, scale_y, 0f, 0f, codepoint);
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
            float scale = ScaleForPixelHeight(info, pixel_height);

            for (int i = 0; i < chardata.Length; ++i)
            {
                int g = FindGlyphIndex(info, first_char + i);
                GetGlyphHMetrics(info, g, out int advance, out _);
                GetGlyphBitmapBox(info, g, scale, scale, out int x0, out int y0, out int x1, out int y1);
                int gw = x1 - x0;
                int gh = y1 - y0;
                if ((x + gw + 1) >= pw)
                {
                    y = bottom_y;
                    x = 1;
                }

                if ((y + gh + 1) >= ph)
                    return -i;

                MakeGlyphBitmap(info, pixels.Slice(x + y * pw), gw, gh, pw, scale, scale, g);
                chardata[i].x0 = (ushort)(short)x;
                chardata[i].y0 = (ushort)(short)y;
                chardata[i].x1 = (ushort)(short)(x + gw);
                chardata[i].y1 = (ushort)(short)(y + gh);
                chardata[i].xadvance = scale * advance;
                chardata[i].xoff = x0;
                chardata[i].yoff = y0;
                x = x + gw + 1;
                if ((y + gh + 1) > bottom_y)
                    bottom_y = y + gh + 1;
            }

            return bottom_y;
        }

        public static void MakeGlyphBitmapSubpixelPrefilter(
            TTFontInfo info, Span<byte> output, int out_w, int out_h, int out_stride,
            float scale_x, float scale_y,
            float shift_x, float shift_y,
            int prefilter_x, int prefilter_y,
            out float sub_x, out float sub_y,
            int glyph)
        {
            MakeGlyphBitmapSubpixel(info, output, out_w - (prefilter_x - 1),
                out_h - (prefilter_y - 1), out_stride, scale_x, scale_y,
                shift_x, shift_y, glyph);
            if (prefilter_x > 1)
                HorizontalPrefilter(output, out_w, out_h, out_stride, prefilter_x);
            if (prefilter_y > 1)
                VerticalPrefilter(output, out_w, out_h, out_stride, prefilter_y);
            sub_x = OversampleShift(prefilter_x);
            sub_y = OversampleShift(prefilter_y);
        }
    }
}
