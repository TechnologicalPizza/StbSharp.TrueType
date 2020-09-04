using System;
using System.Numerics;
using static StbSharp.StbRectPack;

namespace StbSharp
{
    public partial class TrueType
    {
        public static int PackPrepare(
            PackContext spc, bool skipMissing, int pw, int ph, int byteStride, int padding)
        {
            spc.width = pw;
            spc.height = ph;
            spc.pack_info = new RPContext();
            spc.padding = padding;
            spc.stride_in_bytes = byteStride != 0 ? byteStride : pw;
            spc.oversample.X = 1;
            spc.oversample.Y = 1;
            spc.skip_missing = skipMissing;
            spc.pack_info.Init(pw - padding, ph - padding);
            return 1;
        }

        public static void PackSetOversampling(PackContext spc, IntPoint oversample)
        {
            if (oversample.X <= 8)
                spc.oversample.X = oversample.X;
            if (oversample.Y <= 8)
                spc.oversample.Y = oversample.Y;
        }

        public static void PackSetSkipMissingCodepoints(PackContext spc, bool skip)
        {
            spc.skip_missing = skip;
        }

        public static bool PackFontRangesRenderIntoRects(
            PackContext spc,
            FontInfo info,
            Span<byte> pixels,
            ReadOnlySpan<PackRange> ranges,
            Span<RPRect> rects)
        {
            bool return_value = true;
            int old_h_over = spc.oversample.X;
            int old_v_over = spc.oversample.Y;
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
                spc.oversample.X = ranges[i].oversample_x;
                spc.oversample.Y = ranges[i].oversample_y;
                recip_h = 1f / spc.oversample.X;
                recip_v = 1f / spc.oversample.Y;
                sub_x = OversampleShift(spc.oversample.X);
                sub_y = OversampleShift(spc.oversample.Y);

                for (j = 0; j < charData.Length; ++j)
                {
                    ref RPRect r = ref rects[k];
                    if (r.was_packed && r.w != 0 && r.h != 0)
                    {
                        int codepoint = ranges[i].array_of_unicode_codepoints == null
                            ? ranges[i].first_unicode_codepoint_in_range + j
                            : ranges[i].array_of_unicode_codepoints[j];
                        int glyph = FindGlyphIndex(info, codepoint);
                        int pad = spc.padding;
                        r.x += pad;
                        r.y += pad;
                        r.w -= pad;
                        r.h -= pad;

                        var pixelSlice = pixels.Slice(r.x + r.y * spc.stride_in_bytes);
                        MakeGlyphBitmapSubpixel(
                            info, pixelSlice,
                            r.w - spc.oversample.X + 1, r.h - spc.oversample.Y + 1, spc.stride_in_bytes,
                            scale * spc.oversample, Vector2.Zero, IntPoint.Zero, glyph);

                        if (spc.oversample.X > 1)
                            HorizontalPrefilter(pixelSlice, r.w, r.h, spc.stride_in_bytes, spc.oversample.X);
                        if (spc.oversample.Y > 1)
                            VerticalPrefilter(pixelSlice, r.w, r.h, spc.stride_in_bytes, spc.oversample.Y);

                        charData[j].x0 = (ushort)(short)r.x;
                        charData[j].y0 = (ushort)(short)r.y;
                        charData[j].x1 = (ushort)(short)(r.x + r.w);
                        charData[j].y1 = (ushort)(short)(r.y + r.h);

                        GetGlyphHMetrics(info, glyph, out int advance, out _);
                        GetGlyphBitmapBox(
                            info, glyph, scale * spc.oversample, out var glyphBox);

                        charData[j].xadvance = scale.X * advance;
                        charData[j].offset0.X = glyphBox.X * recip_h + sub_x;
                        charData[j].offset0.Y = glyphBox.Y * recip_v + sub_y;
                        charData[j].offset1.X = (glyphBox.X + r.w) * recip_h + sub_x;
                        charData[j].offset1.Y = (glyphBox.Y + r.h) * recip_v + sub_y;

                        if (glyph == 0)
                            missing_glyph = j;
                    }
                    else if (spc.skip_missing)
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

            spc.oversample.X = old_h_over;
            spc.oversample.Y = old_v_over;
            return return_value;
        }

        public static int PackFontRangesGatherRects(
            PackContext spc, FontInfo info,
            Span<PackRange> ranges, Span<RPRect> rects)
        {
            int k = 0;
            bool missing_glyph_added = false;

            for (int i = 0; i < ranges.Length; ++i)
            {
                float fh = ranges[i].font_size;
                var scale = new Vector2(fh > 0
                    ? ScaleForPixelHeight(info, fh)
                    : ScaleForMappingEmToPixels(info, -fh));

                ranges[i].oversample_x = (byte)spc.oversample.X;
                ranges[i].oversample_y = (byte)spc.oversample.Y;

                for (int j = 0; j < ranges[i].chardata_for_range.Length; ++j)
                {
                    int codepoint = ranges[i].array_of_unicode_codepoints == null
                        ? ranges[i].first_unicode_codepoint_in_range + j
                        : ranges[i].array_of_unicode_codepoints[j];

                    int glyph = FindGlyphIndex(info, codepoint);
                    if (glyph == 0 && (spc.skip_missing || missing_glyph_added))
                    {
                        rects[k].w = rects[k].h = 0;
                    }
                    else
                    {
                        GetGlyphBitmapBoxSubpixel(
                            info, glyph, scale * spc.oversample, Vector2.Zero, out var glyphBox);

                        rects[k].w = glyphBox.W + spc.padding + spc.oversample.X - 1;
                        rects[k].h = glyphBox.H + spc.padding + spc.oversample.Y - 1;

                        if (glyph == 0)
                            missing_glyph_added = true;
                    }
                    k++;
                }
            }
            return k;
        }

        public static bool PackFontRanges(
            PackContext spc,
            Span<byte> pixels,
            ReadOnlyMemory<byte> fontData,
            Span<PackRange> ranges)
        {
            int count = 0;
            for (int i = 0; i < ranges.Length; ++i)
            {
                Span<PackedChar> charData = ranges[i].chardata_for_range.Span;
                for (int j = 0; j < charData.Length; ++j)
                    charData[j].x0 = charData[j].y0 = charData[j].x1 = charData[j].y1 = 0;

                count += charData.Length;
            }

            var info = new FontInfo();
            if (!InitFont(info, fontData, GetFontOffset(fontData.Span, 0)))
                return false;

            var rectBuffer = new RPRect[count];
            var rects = rectBuffer.AsSpan(0, count);
            int packedCount = PackFontRangesGatherRects(spc, info, ranges, rects);
            rects = rects.Slice(0, packedCount);

            spc.pack_info.PackRects(rects);
            return PackFontRangesRenderIntoRects(spc, info, pixels, ranges, rects);
        }

        public static bool PackFontRange(
            PackContext spc,
            Span<byte> pixels,
            ReadOnlyMemory<byte> fontdata,
            float fontSize,
            int firstUnicodeCodepointInRange,
            Memory<PackedChar> chardataForRange)
        {
            var range = new PackRange();
            range.first_unicode_codepoint_in_range = firstUnicodeCodepointInRange;
            range.array_of_unicode_codepoints = null;
            range.chardata_for_range = chardataForRange;
            range.font_size = fontSize;

            // todo: remove this array alloc
            return PackFontRanges(spc, pixels, fontdata, new PackRange[] { range });
        }

        public static void GetScaledFontVMetrics(
            ReadOnlyMemory<byte> fontData, int index, float size,
            out float ascent, out float descent, out float lineGap)
        {
            var info = new FontInfo();
            InitFont(info, fontData, GetFontOffset(fontData.Span, index));

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
