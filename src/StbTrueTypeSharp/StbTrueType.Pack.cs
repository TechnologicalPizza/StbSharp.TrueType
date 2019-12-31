using System;
using static StbSharp.StbRectPack;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class StbTrueType
    {
        public static int PackBegin(
            TTPackContext spc, bool skipMissing, int pw, int ph, int stride_in_bytes, int padding)
        {
            int num_nodes = pw - padding;
            var nodes = (byte*)CRuntime.malloc(num_nodes);
            if (nodes == null)
                return 0;

            spc.width = pw;
            spc.height = ph;
            spc.pack_info = new RPContext();
            spc.nodes = nodes;
            spc.padding = padding;
            spc.stride_in_bytes = stride_in_bytes != 0 ? stride_in_bytes : pw;
            spc.oversample.x = 1;
            spc.oversample.y = 1;
            spc.skip_missing = skipMissing;
            spc.pack_info.Init(pw - padding, ph - padding);
            return 1;
        }

        public static void PackEnd(TTPackContext spc)
        {
            CRuntime.free(spc.nodes);
        }

        public static void PackSetOversampling(TTPackContext spc, TTIntPoint oversample)
        {
            if (oversample.x <= 8)
                spc.oversample.x = oversample.x;
            if (oversample.y <= 8)
                spc.oversample.y = oversample.y;
        }

        public static void PackSetSkipMissingCodepoints(TTPackContext spc, bool skip)
        {
            spc.skip_missing = skip;
        }


        public static bool PackFontRangesRenderIntoRects(
            TTPackContext spc,
            TTFontInfo info,
            Span<byte> pixels,
            ReadOnlySpan<TTPackRange> ranges,
            Span<RPRect> rects)
        {
            bool return_value = true;
            int old_h_over = spc.oversample.x;
            int old_v_over = spc.oversample.y;
            int k = 0;
            int j = 0;
            int i = 0;
            for (i = 0; i < ranges.Length; ++i)
            {
                Span<TTPackedChar> charData = ranges[i].chardata_for_range.Span;
                float fh = ranges[i].font_size;
                var scale = fh > 0
                    ? ScaleForPixelHeight(info, fh)
                    : ScaleForMappingEmToPixels(info, -fh);
                float recip_h = 0;
                float recip_v = 0;
                float sub_x = 0;
                float sub_y = 0;
                spc.oversample.x = ranges[i].oversample_x;
                spc.oversample.y = ranges[i].oversample_y;
                recip_h = 1f / spc.oversample.x;
                recip_v = 1f / spc.oversample.y;
                sub_x = OversampleShift(spc.oversample.x);
                sub_y = OversampleShift(spc.oversample.y);
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
                            r.w - spc.oversample.x + 1, r.h - spc.oversample.y + 1, spc.stride_in_bytes,
                            scale * spc.oversample, TTPoint.Zero, TTIntPoint.Zero, glyph);

                        if (spc.oversample.x > 1)
                            HorizontalPrefilter(pixelSlice, r.w, r.h, spc.stride_in_bytes, spc.oversample.x);
                        if (spc.oversample.y > 1)
                            VerticalPrefilter(pixelSlice, r.w, r.h, spc.stride_in_bytes, spc.oversample.y);

                        charData[j].x0 = (ushort)(short)r.x;
                        charData[j].y0 = (ushort)(short)r.y;
                        charData[j].x1 = (ushort)(short)(r.x + r.w);
                        charData[j].y1 = (ushort)(short)(r.y + r.h);

                        GetGlyphHMetrics(info, glyph, out int advance, out _);
                        GetGlyphBitmapBox(
                            info, glyph, scale * spc.oversample, out var glyphBox);

                        charData[j].xadvance = scale.x * advance;
                        charData[j].offset0.x = glyphBox.x * recip_h + sub_x;
                        charData[j].offset0.y = glyphBox.y * recip_v + sub_y;
                        charData[j].offset1.x = (glyphBox.x + r.w) * recip_h + sub_x;
                        charData[j].offset1.y = (glyphBox.y + r.h) * recip_v + sub_y;
                    }
                    else
                    {
                        return_value = false;
                    }
                    k++;
                }
            }

            spc.oversample.x = old_h_over;
            spc.oversample.y = old_v_over;
            return return_value;
        }

        public static int PackFontRangesGatherRects(
            TTPackContext spc, TTFontInfo info,
            Span<TTPackRange> ranges, Span<RPRect> rects)
        {
            int k = 0;
            for (int i = 0; i < ranges.Length; ++i)
            {
                float fh = ranges[i].font_size;
                var scale = fh > 0
                    ? ScaleForPixelHeight(info, fh)
                    : ScaleForMappingEmToPixels(info, -fh);

                ranges[i].oversample_x = (byte)spc.oversample.x;
                ranges[i].oversample_y = (byte)spc.oversample.y;

                for (int j = 0; j < ranges[i].chardata_for_range.Length; ++j)
                {
                    int codepoint = ranges[i].array_of_unicode_codepoints == null
                        ? ranges[i].first_unicode_codepoint_in_range + j
                        : ranges[i].array_of_unicode_codepoints[j];

                    int glyph = FindGlyphIndex(info, codepoint);
                    if (glyph == 0 && spc.skip_missing)
                    {
                        rects[k].w = rects[k].h = 0;
                    }
                    else
                    {
                        GetGlyphBitmapBoxSubpixel(
                            info, glyph, scale * spc.oversample, TTPoint.Zero, out var glyphBox);
                        rects[k].w = glyphBox.w + spc.padding + spc.oversample.x - 1;
                        rects[k].h = glyphBox.h + spc.padding + spc.oversample.y - 1;
                    }
                    k++;
                }
            }
            return k;
        }

        public static bool PackFontRanges(
            TTPackContext spc,
            Span<byte> pixels,
            ReadOnlyMemory<byte> fontData,
            Span<TTPackRange> ranges)
        {
            int n = 0;
            for (int i = 0; i < ranges.Length; ++i)
            {
                Span<TTPackedChar> charData = ranges[i].chardata_for_range.Span;
                for (int j = 0; j < charData.Length; ++j)
                    charData[j].x0 = charData[j].y0 = charData[j].x1 = charData[j].y1 = 0;

                n += charData.Length;
            }

            var rectPtr = CRuntime.malloc(sizeof(RPRect) * n);
            if (rectPtr == null)
                return false;

            try
            {
                var info = new TTFontInfo();
                if (!InitFont(info, fontData, GetFontOffset(fontData.Span, 0)))
                    return false;

                var rects = new Span<RPRect>(rectPtr, n);
                n = PackFontRangesGatherRects(spc, info, ranges, rects);
                spc.pack_info.PackRects(rects);
                return PackFontRangesRenderIntoRects(spc, info, pixels, ranges, rects);
            }
            finally
            {
                CRuntime.free(rectPtr);
            }
        }

        public static bool PackFontRange(
            TTPackContext spc,
            Span<byte> pixels,
            ReadOnlyMemory<byte> fontdata,
            float font_size,
            int first_unicode_codepoint_in_range,
            Memory<TTPackedChar> chardata_for_range)
        {
            var range = new TTPackRange();
            range.first_unicode_codepoint_in_range = first_unicode_codepoint_in_range;
            range.array_of_unicode_codepoints = null;
            range.chardata_for_range = chardata_for_range;
            range.font_size = font_size;

            // todo: remove this array alloc
            return PackFontRanges(spc, pixels, fontdata, new TTPackRange[] { range });
        }

        public static void GetScaledFontVMetrics(
            ReadOnlyMemory<byte> fontData, int index, float size,
            out float ascent, out float descent, out float lineGap)
        {
            var info = new TTFontInfo();
            InitFont(info, fontData, GetFontOffset(fontData.Span, index));

            var scale = size > 0
                ? ScaleForPixelHeight(info, size)
                : ScaleForMappingEmToPixels(info, -size);

            GetFontVMetrics(info, out int i_ascent, out int i_descent, out int i_lineGap);
            ascent = i_ascent * scale.y;
            descent = i_descent * scale.y;
            lineGap = i_lineGap * scale.y;
        }

        public static void GetPackedQuad(
            ReadOnlySpan<TTPackedChar> chardata, int pw, int ph, int char_index,
            ref float xpos, float ypos, ref TTAlignedQuad q, bool align_to_integer)
        {
            float ipw = 1f / pw;
            float iph = 1f / ph;
            ref readonly var b = ref chardata[char_index];
            if (align_to_integer)
            {
                float x = (int)Math.Floor(xpos + b.offset0.x + 0.5f);
                float y = (int)Math.Floor(ypos + b.offset0.y + 0.5f);
                q.pos0.x = x;
                q.pos0.y = y;
                q.pos1.x = x + b.offset1.x - b.offset0.x;
                q.pos1.y = y + b.offset1.y - b.offset0.y;
            }
            else
            {
                q.pos0.x = xpos + b.offset0.x;
                q.pos0.y = ypos + b.offset0.y;
                q.pos1.x = xpos + b.offset1.x;
                q.pos1.y = ypos + b.offset1.y;
            }

            q.s0 = b.x0 * ipw;
            q.t0 = b.y0 * iph;
            q.s1 = b.x1 * ipw;
            q.t1 = b.y1 * iph;
            xpos += b.xadvance;
        }
    }
}
