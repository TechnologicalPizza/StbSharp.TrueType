using System;
using System.Numerics;

namespace StbSharp
{
    public partial class TrueType
    {
        public static void GetFontVMetrics(
            FontInfo info, out int ascent, out int descent, out int lineGap)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            var data = info.data.Span[info.hhea..];
            ascent = ReadInt16(data[4..]);
            descent = ReadInt16(data[6..]);
            lineGap = ReadInt16(data[8..]);
        }

        public static bool GetFontVMetricsOS2(
            FontInfo info, out int typoAscent, out int typoDescent, out int typoLineGap)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            var data = info.data.Span;
            int table = (int)FindTable(data, info.fontindex, "OS/2").GetValueOrDefault();
            if (table == 0)
            {
                typoAscent = 0;
                typoDescent = 0;
                typoLineGap = 0;
                return false;
            }

            var tableData = data[table..];
            typoAscent = ReadInt16(tableData[68..]);
            typoDescent = ReadInt16(tableData[70..]);
            typoLineGap = ReadInt16(tableData[72..]);
            return true;
        }

        public static void GetFontBoundingBox(FontInfo info, out Vector2 p0, out Vector2 p1)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            var head = info.data.Span[info.head..];
            p0.X = ReadInt16(head[36..]);
            p0.Y = ReadInt16(head[38..]);
            p1.X = ReadInt16(head[40..]);
            p1.Y = ReadInt16(head[42..]);
        }

        public static float ScaleForPixelHeight(FontInfo info, float height)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            var data = info.data.Span[info.hhea..];
            int fheight = ReadInt16(data[4..]) - ReadInt16(data[6..]);
            return height / fheight;
        }

        public static float ScaleForMappingEmToPixels(FontInfo info, float pixels)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            int unitsPerEm = ReadUInt16(info.data.Span[(info.head + 18)..]);
            return pixels / unitsPerEm;
        }
    }
}
