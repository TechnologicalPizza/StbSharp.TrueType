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

            if (info.hhea == null)
            {
                ascent = 0;
                descent = 0;
                lineGap = 0;
                return;
            }

            ReadOnlySpan<byte> data = info.data.Span[info.hhea.GetValueOrDefault()..];
            ascent = ReadInt16(data[4..]);
            descent = ReadInt16(data[6..]);
            lineGap = ReadInt16(data[8..]);
        }

        public static bool GetFontVMetricsOS2(
            FontInfo info, out int typoAscent, out int typoDescent, out int typoLineGap)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            ReadOnlySpan<byte> data = info.data.Span;
            int? table = FindTable(data, "OS/2");
            if (table == null)
            {
                typoAscent = 0;
                typoDescent = 0;
                typoLineGap = 0;
                return false;
            }

            ReadOnlySpan<byte> tableData = data[table.GetValueOrDefault()..];
            typoAscent = ReadInt16(tableData[68..]);
            typoDescent = ReadInt16(tableData[70..]);
            typoLineGap = ReadInt16(tableData[72..]);
            return true;
        }

        public static void GetFontBoundingBox(FontInfo info, out Vector2 p0, out Vector2 p1)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            if (info.head == null)
            {
                p0 = default;
                p1 = default;
                return;
            }

            ReadOnlySpan<byte> head = info.data.Span[info.head.GetValueOrDefault()..];
            p0.X = ReadInt16(head[36..]);
            p0.Y = ReadInt16(head[38..]);
            p1.X = ReadInt16(head[40..]);
            p1.Y = ReadInt16(head[42..]);
        }

        public static float ScaleForPixelHeight(FontInfo info, float height)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            if (info.hhea == null)
                return 0;

            ReadOnlySpan<byte> data = info.data.Span[info.hhea.GetValueOrDefault()..];
            int fheight = ReadInt16(data[4..]) - ReadInt16(data[6..]);
            return height / fheight;
        }

        public static float ScaleForMappingEmToPixels(FontInfo info, float pixels)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            if (info.head == null)
                return 0;

            ReadOnlySpan<byte> data = info.data.Span[info.head.GetValueOrDefault()..];
            int unitsPerEm = ReadUInt16(data[18..]);
            return pixels / unitsPerEm;
        }
    }
}
