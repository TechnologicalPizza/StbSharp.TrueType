﻿
namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class TrueType
    {
        public static byte[] GetCodepointBitmap(
            FontInfo info, Point scale, int codepoint,
            out int width, out int height, out IntPoint offset)
        {
            return GetCodepointBitmapSubpixel(
                info, scale, Point.Zero, codepoint, out width, out height, out offset);
        }

        public static byte[] GetCodepointBitmapSubpixel(
            FontInfo info, Point scale, Point shift, int codepoint,
            out int width, out int height, out IntPoint offset)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            return GetGlyphBitmapSubpixel(
                info, scale, shift, glyph, out width, out height, out offset);
        }

        public static bool GetCodepointBox(
            FontInfo info, int codepoint, out IntRect glyphBox)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            return GetGlyphBox(info, glyph, out glyphBox);
        }

        public static int GetCodepointShape(
            FontInfo info, int unicode_codepoint, out Vertex[] vertices)
        {
            return GetGlyphShape(info, FindGlyphIndex(info, unicode_codepoint), out vertices);
        }

        public static int GetCodepointKernAdvance(
            FontInfo info, int ch1, int ch2)
        {
            if ((info.kern == 0) && (info.gpos == 0))
                return 0;

            int chIndex1 = FindGlyphIndex(info, ch1);
            int chIndex2 = FindGlyphIndex(info, ch2);
            return GetGlyphKernAdvance(info, chIndex1, chIndex2);
        }

        public static void GetCodepointHMetrics(
            FontInfo info, int codepoint, 
            out int advanceWidth, out int leftSideBearing)
        {
            GetGlyphHMetrics(
                info, FindGlyphIndex(info, codepoint), 
                out advanceWidth, out leftSideBearing);
        }
    }
}