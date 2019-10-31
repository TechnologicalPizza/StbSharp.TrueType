
namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class StbTrueType
    {
        public static int GetCodepointBox(
            TTFontInfo info, int codepoint, out int x0, out int y0, out int x1, out int y1)
        {
            int index = FindGlyphIndex(info, codepoint);
            return GetGlyphBox(info, index, out x0, out y0, out x1, out y1);
        }

        public static int GetCodepointShape(TTFontInfo info, int unicode_codepoint, out TTVertex* vertices)
        {
            return GetGlyphShape(info, FindGlyphIndex(info, unicode_codepoint), out vertices);
        }

        public static int GetCodepointKernAdvance(TTFontInfo info, int ch1, int ch2)
        {
            if ((info.kern == 0) && (info.gpos == 0))
                return 0;

            return GetGlyphKernAdvance(info, FindGlyphIndex(info, ch1), FindGlyphIndex(info, ch2));
        }

        public static void GetCodepointHMetrics(
            TTFontInfo info, int codepoint, out int advanceWidth, out int leftSideBearing)
        {
            GetGlyphHMetrics(info, FindGlyphIndex(info, codepoint), out advanceWidth, out leftSideBearing);
        }
    }
}
