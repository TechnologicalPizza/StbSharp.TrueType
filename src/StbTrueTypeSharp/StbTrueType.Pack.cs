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
            TTPackContext spc, int pw, int ph, int stride_in_bytes, int padding)
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
            spc.h_oversample = 1;
            spc.v_oversample = 1;
            spc.skip_missing = 0;
            spc.pack_info.Init(pw - padding, ph - padding);
            return 1;
        }

        public static void PackEnd(TTPackContext spc)
        {
            CRuntime.free(spc.nodes);
        }

        public static void PackSetOversampling(TTPackContext spc, int h_oversample, int v_oversample)
        {
            if (h_oversample <= 8)
                spc.h_oversample = h_oversample;
            if (v_oversample <= 8)
                spc.v_oversample = v_oversample;
        }

        public static void PackSetSkipMissingCodepoints(TTPackContext spc, int skip)
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
            int old_h_over = spc.h_oversample;
            int old_v_over = spc.v_oversample;
            int k = 0;
            int j = 0;
            int i = 0;
            for (i = 0; i < ranges.Length; ++i)
            {
                Span<TTPackedChar> charData = ranges[i].chardata_for_range.Span;
                float fh = ranges[i].font_size;
                float scale = fh > 0
                    ? ScaleForPixelHeight(info, fh)
                    : ScaleForMappingEmToPixels(info, -fh);
                float recip_h = 0;
                float recip_v = 0;
                float sub_x = 0;
                float sub_y = 0;
                spc.h_oversample = ranges[i].h_oversample;
                spc.v_oversample = ranges[i].v_oversample;
                recip_h = 1f / spc.h_oversample;
                recip_v = 1f / spc.v_oversample;
                sub_x = OversampleShift(spc.h_oversample);
                sub_y = OversampleShift(spc.v_oversample);
                for (j = 0; j < charData.Length; ++j)
                {
                    ref RPRect r = ref rects[k];
                    if (r.was_packed && (r.w != 0) && (r.h != 0))
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
                        GetGlyphHMetrics(info, glyph, out int advance, out int lsb);
                        GetGlyphBitmapBox(
                            info, glyph, scale * spc.h_oversample, scale * spc.v_oversample,
                            out int x0, out int y0, out int x1, out int y1);

                        var pixelSlice = pixels.Slice(r.x + r.y * spc.stride_in_bytes);
                        MakeGlyphBitmapSubpixel(info, pixelSlice,
                            r.w - spc.h_oversample + 1, r.h - spc.v_oversample + 1,
                            spc.stride_in_bytes, scale * spc.h_oversample,
                            scale * spc.v_oversample, 0f, 0f, glyph);

                        if (spc.h_oversample > 1)
                            HorizontalPrefilter(pixelSlice, r.w, r.h, spc.stride_in_bytes, spc.h_oversample);
                        if (spc.v_oversample > 1)
                            VerticalPrefilter(pixelSlice, r.w, r.h, spc.stride_in_bytes, spc.v_oversample);

                        ref TTPackedChar bc = ref charData[j];
                        bc.x0 = (ushort)(short)r.x;
                        bc.y0 = (ushort)(short)r.y;
                        bc.x1 = (ushort)(short)(r.x + r.w);
                        bc.y1 = (ushort)(short)(r.y + r.h);
                        bc.xadvance = scale * advance;
                        bc.xoff = x0 * recip_h + sub_x;
                        bc.yoff = y0 * recip_v + sub_y;
                        bc.xoff2 = (x0 + r.w) * recip_h + sub_x;
                        bc.yoff2 = (y0 + r.h) * recip_v + sub_y;
                    }
                    else
                    {
                        return_value = false;
                    }
                    k++;
                }
            }

            spc.h_oversample = old_h_over;
            spc.v_oversample = old_v_over;
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
                float scale = fh > 0
                    ? ScaleForPixelHeight(info, fh)
                    : ScaleForMappingEmToPixels(info, -fh);
                ranges[i].h_oversample = (byte)spc.h_oversample;
                ranges[i].v_oversample = (byte)spc.v_oversample;

                for (int j = 0; j < ranges[i].chardata_for_range.Length; ++j)
                {
                    int codepoint = ranges[i].array_of_unicode_codepoints == null
                        ? ranges[i].first_unicode_codepoint_in_range + j
                        : ranges[i].array_of_unicode_codepoints[j];

                    int glyph = FindGlyphIndex(info, codepoint);
                    if ((glyph == 0) && (spc.skip_missing != 0))
                    {
                        rects[k].w = rects[k].h = 0;
                    }
                    else
                    {
                        GetGlyphBitmapBoxSubpixel(
                            info, glyph, scale * spc.h_oversample,
                            scale * spc.v_oversample, 0, 0, out int x0, out int y0, out int x1, out int y1);
                        rects[k].w = x1 - x0 + spc.padding + spc.h_oversample - 1;
                        rects[k].h = y1 - y0 + spc.padding + spc.v_oversample - 1;
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
            float scale = size > 0
    ? ScaleForPixelHeight(info, size)
    : ScaleForMappingEmToPixels(info, -size);
            GetFontVMetrics(info, out int i_ascent, out int i_descent, out int i_lineGap);
            ascent = i_ascent * scale;
            descent = i_descent * scale;
            lineGap = i_lineGap * scale;
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
                float x = (int)Math.Floor(xpos + b.xoff + 0.5f);
                float y = (int)Math.Floor(ypos + b.yoff + 0.5f);
                q.x0 = x;
                q.y0 = y;
                q.x1 = x + b.xoff2 - b.xoff;
                q.y1 = y + b.yoff2 - b.yoff;
            }
            else
            {
                q.x0 = xpos + b.xoff;
                q.y0 = ypos + b.yoff;
                q.x1 = xpos + b.xoff2;
                q.y1 = ypos + b.yoff2;
            }

            q.s0 = b.x0 * ipw;
            q.t0 = b.y0 * iph;
            q.s1 = b.x1 * ipw;
            q.t1 = b.y1 * iph;
            xpos += b.xadvance;
        }
    }
}
