using System;
using System.Numerics;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class TrueType
    {
        public static void GetFontVMetrics(
            FontInfo info, out int ascent, out int descent, out int lineGap)
        {
            var data = info.data.Span.Slice(info.hhea);
            ascent = ReadInt16(data.Slice(4));
            descent = ReadInt16(data.Slice(6));
            lineGap = ReadInt16(data.Slice(8));
        }

        public static bool GetFontVMetricsOS2(
            FontInfo info, out int typoAscent, out int typoDescent, out int typoLineGap)
        {
            var data = info.data.Span;
            int table = (int)FindTable(data, info.fontstart, "OS/2");
            if (table == 0)
            {
                typoAscent = 0;
                typoDescent = 0;
                typoLineGap = 0;
                return false;
            }

            var tableData = data.Slice(table);
            typoAscent = ReadInt16(tableData.Slice(68));
            typoDescent = ReadInt16(tableData.Slice(70));
            typoLineGap = ReadInt16(tableData.Slice(72));
            return true;
        }

        public static void GetFontBoundingBox(FontInfo info, out Vector2 p0, out Vector2 p1)
        {
            var head = info.data.Span.Slice(info.head);
            p0.X = ReadInt16(head.Slice(36));
            p0.Y = ReadInt16(head.Slice(38));
            p1.X = ReadInt16(head.Slice(40));
            p1.Y = ReadInt16(head.Slice(42));
        }

        public static Vector2 ScaleForPixelHeight(FontInfo info, float height)
        {
            var data = info.data.Span.Slice(info.hhea);
            int fheight = ReadInt16(data.Slice(4)) - ReadInt16(data.Slice(6));
            return new Vector2(height / fheight);
        }

        public static Vector2 ScaleForMappingEmToPixels(FontInfo info, float pixels)
        {
            int unitsPerEm = ReadUInt16(info.data.Span.Slice(info.head + 18));
            return new Vector2(pixels / unitsPerEm);
        }
    }
}
