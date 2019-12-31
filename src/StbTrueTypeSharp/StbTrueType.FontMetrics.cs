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
        public static void GetFontVMetrics(
            TTFontInfo info, out int ascent, out int descent, out int lineGap)
        {
            ascent = ReadInt16(info.data.Span.Slice(info.hhea + 4));
            descent = ReadInt16(info.data.Span.Slice(info.hhea + 6));
            lineGap = ReadInt16(info.data.Span.Slice(info.hhea + 8));
        }

        public static bool GetFontVMetricsOS2(
            TTFontInfo info, out int typoAscent, out int typoDescent, out int typoLineGap)
        {
            var data = info.data.Span;
            int tab = (int)FindTable(data, info.fontstart, "OS/2");
            if (tab == 0)
            {
                typoAscent = 0;
                typoDescent = 0;
                typoLineGap = 0;
                return false;
            }
            typoAscent = ReadInt16(data.Slice(tab + 68));
            typoDescent = ReadInt16(data.Slice(tab + 70));
            typoLineGap = ReadInt16(data.Slice(tab + 72));
            return true;
        }

        public static void GetFontBoundingBox(TTFontInfo info, out TTPoint p0, out TTPoint p1)
        {
            var data = info.data.Span;
            p0.x = ReadInt16(data.Slice(info.head + 36));
            p0.y = ReadInt16(data.Slice(info.head + 38));
            p1.x = ReadInt16(data.Slice(info.head + 40));
            p1.y = ReadInt16(data.Slice(info.head + 42));
        }

        public static TTPoint ScaleForPixelHeight(TTFontInfo info, float height)
        {
            int fheight = ReadInt16(info.data.Span.Slice(info.hhea + 4)) - ReadInt16(info.data.Span.Slice(info.hhea + 6));
            return new TTPoint(height / fheight);
        }

        public static TTPoint ScaleForMappingEmToPixels(TTFontInfo info, float pixels)
        {
            int unitsPerEm = ReadUInt16(info.data.Span.Slice(info.head + 18));
            return new TTPoint(pixels / unitsPerEm);
        }
    }
}
