using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace StbSharp
{
    public partial class TrueType
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RPRect
        {
            public int x;
            public int y;
            public int w;
            public int h;

            public int id;
            public bool was_packed;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RPContext
        {
            public int width;
            public int height;
            public int x;
            public int y;
            public int bottom_y;

            public void Init(int pw, int ph)
            {
                width = pw;
                height = ph;
                x = 0;
                y = 0;
                bottom_y = 0;
            }

            public void PackRects(Span<RPRect> rects)
            {
                int i;
                for (i = 0; i < rects.Length; i++)
                {
                    if ((x + rects[i].w) > width)
                    {
                        x = 0;
                        y = bottom_y;
                    }

                    if ((y + rects[i].h) > height)
                        break;

                    rects[i].x = x;
                    rects[i].y = y;
                    rects[i].was_packed = true;

                    x += rects[i].w;
                    if ((y + rects[i].h) > bottom_y)
                        bottom_y = y + rects[i].h;
                }

                for (; i < rects.Length; i++)
                    rects[i].was_packed = false;
            }
        }

        public static int PackPrepare(
            PackContext context, bool skipMissing, int pw, int ph, int byteStride, int padding)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.width = pw;
            context.height = ph;
            context.pack_info = new RPContext();
            context.padding = padding;
            context.stride_in_bytes = byteStride != 0 ? byteStride : pw;
            context.oversample.X = 1;
            context.oversample.Y = 1;
            context.skip_missing = skipMissing;
            context.pack_info.Init(pw - padding, ph - padding);
            return 1;
        }

        public static void PackSetOversampling(PackContext context, IntPoint oversample)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (oversample.X <= 8)
                context.oversample.X = oversample.X;
            if (oversample.Y <= 8)
                context.oversample.Y = oversample.Y;
        }

        public static void PackSetSkipMissingCodepoints(PackContext context, bool skip)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.skip_missing = skip;
        }

        public static bool PackFontRangesRenderIntoRects(
            PackContext context,
            FontInfo info,
            float pixelFlatness,
            Span<byte> pixels,
            ReadOnlySpan<PackRange> ranges,
            Span<RPRect> rects)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            bool return_value = true;
            int old_h_over = context.oversample.X;
            int old_v_over = context.oversample.Y;
            int k = 0;
            int j = 0;
            int i = 0;
            int missing_glyph = -1;

            for (i = 0; i < ranges.Length; ++i)
            {
                Span<PackedChar> charData = ranges[i].chardata_for_range.Span;
                float fh = ranges[i].font_size;
                var scale = new Vector2(fh > 0
                    ? ScaleForPixelHeight(info, fh)
                    : ScaleForMappingEmToPixels(info, -fh));
                float recip_h = 0;
                float recip_v = 0;
                float sub_x = 0;
                float sub_y = 0;
                context.oversample.X = ranges[i].oversample_x;
                context.oversample.Y = ranges[i].oversample_y;
                recip_h = 1f / context.oversample.X;
                recip_v = 1f / context.oversample.Y;
                sub_x = OversampleShift(context.oversample.X);
                sub_y = OversampleShift(context.oversample.Y);

                for (j = 0; j < charData.Length; ++j)
                {
                    ref RPRect r = ref rects[k];
                    if (r.was_packed && r.w != 0 && r.h != 0)
                    {
                        int[]? codepointArray = ranges[i].array_of_unicode_codepoints;
                        int codepoint = codepointArray != null
                            ? codepointArray[j]
                            : ranges[i].first_unicode_codepoint_in_range + j;

                        int glyph = FindGlyphIndex(info, codepoint);
                        int pad = context.padding;
                        r.x += pad;
                        r.y += pad;
                        r.w -= pad;
                        r.h -= pad;

                        var pixelSlice = pixels[(r.x + r.y * context.stride_in_bytes)..];
                        var glyphBmp = new Bitmap(
                            pixelSlice,
                            r.w - context.oversample.X + 1,
                            r.h - context.oversample.Y + 1,
                            context.stride_in_bytes);

                        MakeGlyphBitmap(
                            info, glyphBmp, pixelFlatness, scale * context.oversample, Vector2.Zero, IntPoint.Zero, glyph);

                        var filterBmp = new Bitmap(pixelSlice, r.w, r.h, context.stride_in_bytes);
                        if (context.oversample.X > 1)
                            HorizontalPrefilter(filterBmp, context.oversample.X);
                        if (context.oversample.Y > 1)
                            VerticalPrefilter(filterBmp, context.oversample.Y);

                        charData[j].x0 = (ushort)r.x;
                        charData[j].y0 = (ushort)r.y;
                        charData[j].x1 = (ushort)(r.x + r.w);
                        charData[j].y1 = (ushort)(r.y + r.h);

                        GetGlyphHMetrics(info, glyph, out int advance, out _);
                        GetGlyphBitmapBox(
                            info, glyph, scale * context.oversample, out var glyphBox);

                        charData[j].xadvance = scale.X * advance;
                        charData[j].offset0.X = glyphBox.X * recip_h + sub_x;
                        charData[j].offset0.Y = glyphBox.Y * recip_v + sub_y;
                        charData[j].offset1.X = (glyphBox.X + r.w) * recip_h + sub_x;
                        charData[j].offset1.Y = (glyphBox.Y + r.h) * recip_v + sub_y;

                        if (glyph == 0)
                            missing_glyph = j;
                    }
                    else if (context.skip_missing)
                    {
                        return_value = false;
                    }
                    else if (r.was_packed && r.w == 0 && r.h == 0 && missing_glyph >= 0)
                    {
                        charData[j] = charData[missing_glyph];
                    }
                    else
                    {
                        return_value = false; // if any fail, report failure
                    }
                    k++;
                }
            }

            context.oversample.X = old_h_over;
            context.oversample.Y = old_v_over;
            return return_value;
        }

        public static int PackFontRangesGatherRects(
            PackContext context, FontInfo info,
            Span<PackRange> ranges, Span<RPRect> rects)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            int k = 0;
            bool missing_glyph_added = false;

            for (int i = 0; i < ranges.Length; ++i)
            {
                float fh = ranges[i].font_size;
                var scale = new Vector2(fh > 0
                    ? ScaleForPixelHeight(info, fh)
                    : ScaleForMappingEmToPixels(info, -fh));

                ranges[i].oversample_x = (byte)context.oversample.X;
                ranges[i].oversample_y = (byte)context.oversample.Y;

                for (int j = 0; j < ranges[i].chardata_for_range.Length; ++j)
                {
                    int[]? codepointArray = ranges[i].array_of_unicode_codepoints;
                    int codepoint = codepointArray != null
                        ? codepointArray[j]
                        : ranges[i].first_unicode_codepoint_in_range + j;

                    int glyph = FindGlyphIndex(info, codepoint);
                    if (glyph == 0 && (context.skip_missing || missing_glyph_added))
                    {
                        rects[k].w = rects[k].h = 0;
                    }
                    else
                    {
                        GetGlyphBitmapBoxSubpixel(
                            info, glyph, scale * context.oversample, Vector2.Zero, out var glyphBox);

                        rects[k].w = glyphBox.W + context.padding + context.oversample.X - 1;
                        rects[k].h = glyphBox.H + context.padding + context.oversample.Y - 1;

                        if (glyph == 0)
                            missing_glyph_added = true;
                    }
                    k++;
                }
            }
            return k;
        }

        public static bool PackFontRanges(
            PackContext context,
            float pixelFlatness,
            Span<byte> pixels,
            ReadOnlyMemory<byte> fontData,
            Span<PackRange> ranges)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            int count = 0;
            for (int i = 0; i < ranges.Length; ++i)
            {
                Span<PackedChar> charData = ranges[i].chardata_for_range.Span;
                for (int j = 0; j < charData.Length; ++j)
                    charData[j].x0 = charData[j].y0 = charData[j].x1 = charData[j].y1 = 0;

                count += charData.Length;
            }

            var info = new FontInfo();
            if (!InitFont(info, fontData[GetFontOffset(fontData.Span)..]))
                return false;

            var rectBuffer = new RPRect[count];
            var rects = rectBuffer.AsSpan(0, count);
            int packedCount = PackFontRangesGatherRects(context, info, ranges, rects);
            rects = rects.Slice(0, packedCount);

            context.pack_info.PackRects(rects);
            return PackFontRangesRenderIntoRects(context, info, pixelFlatness, pixels, ranges, rects);
        }

        public static bool PackFontRange(
            PackContext context,
            Span<byte> pixels,
            ReadOnlyMemory<byte> fontdata,
            float pixelFlatness,
            float fontSize,
            int firstUnicodeCodepointInRange,
            Memory<PackedChar> chardataForRange)
        {
            var range = new PackRange();
            range.first_unicode_codepoint_in_range = firstUnicodeCodepointInRange;
            range.array_of_unicode_codepoints = null;
            range.chardata_for_range = chardataForRange;
            range.font_size = fontSize;

            return PackFontRanges(context, pixelFlatness, pixels, fontdata, new PackRange[] { range });
        }

        public static void GetScaledFontVMetrics(
            ReadOnlyMemory<byte> fontData, float size,
            out float ascent, out float descent, out float lineGap)
        {
            var info = new FontInfo();
            InitFont(info, fontData[GetFontOffset(fontData.Span)..]);
            float scale = size > 0
                ? ScaleForPixelHeight(info, size)
                : ScaleForMappingEmToPixels(info, -size);

            GetFontVMetrics(info, out int i_ascent, out int i_descent, out int i_lineGap);
            ascent = i_ascent * scale;
            descent = i_descent * scale;
            lineGap = i_lineGap * scale;
        }

        public static void GetPackedQuad(
            ReadOnlySpan<PackedChar> chardata, int pw, int ph, int charIndex,
            ref float xpos, float ypos, ref AlignedQuad q, bool alignToInteger)
        {
            float ipw = 1f / pw;
            float iph = 1f / ph;
            ref readonly var b = ref chardata[charIndex];
            if (alignToInteger)
            {
                float x = (int)Math.Floor(xpos + b.offset0.X + 0.5f);
                float y = (int)Math.Floor(ypos + b.offset0.Y + 0.5f);
                q.pos0.X = x;
                q.pos0.Y = y;
                q.pos1.X = x + b.offset1.X - b.offset0.X;
                q.pos1.Y = y + b.offset1.Y - b.offset0.Y;
            }
            else
            {
                q.pos0.X = xpos + b.offset0.X;
                q.pos0.Y = ypos + b.offset0.Y;
                q.pos1.X = xpos + b.offset1.X;
                q.pos1.Y = ypos + b.offset1.Y;
            }

            q.st0.X = b.x0 * ipw;
            q.st0.Y = b.y0 * iph;
            q.st1.X = b.x1 * ipw;
            q.st1.Y = b.y1 * iph;
            xpos += b.xadvance;
        }
    }
}
