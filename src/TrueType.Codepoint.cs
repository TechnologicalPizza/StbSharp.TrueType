
namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class StbTrueType
    {
        public static byte[] GetCodepointBitmap(
            TTFontInfo info, TTPoint scale, int codepoint,
            out int width, out int height, out TTIntPoint offset)
        {
            return GetCodepointBitmapSubpixel(
                info, scale, TTPoint.Zero, codepoint, out width, out height, out offset);
        }

        public static byte[] GetCodepointBitmapSubpixel(
            TTFontInfo info, TTPoint scale, TTPoint shift, int codepoint,
            out int width, out int height, out TTIntPoint offset)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            return GetGlyphBitmapSubpixel(
                info, scale, shift, glyph, out width, out height, out offset);
        }

        public static bool GetCodepointBox(
            TTFontInfo info, int codepoint, out TTIntRect glyphBox)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            return GetGlyphBox(info, glyph, out glyphBox);
        }

        public static int GetCodepointShape(
            TTFontInfo info, int unicode_codepoint, out TTVertex[] vertices)
        {
            return GetGlyphShape(info, FindGlyphIndex(info, unicode_codepoint), out vertices);
        }

        public static int GetCodepointKernAdvance(
            TTFontInfo info, int ch1, int ch2)
        {
            if ((info.kern == 0) && (info.gpos == 0))
                return 0;

            int chIndex1 = FindGlyphIndex(info, ch1);
            int chIndex2 = FindGlyphIndex(info, ch2);
            return GetGlyphKernAdvance(info, chIndex1, chIndex2);
        }

        public static void GetCodepointHMetrics(
            TTFontInfo info, int codepoint, 
            out int advanceWidth, out int leftSideBearing)
        {
            GetGlyphHMetrics(
                info, FindGlyphIndex(info, codepoint), 
                out advanceWidth, out leftSideBearing);
        }
    }
}
