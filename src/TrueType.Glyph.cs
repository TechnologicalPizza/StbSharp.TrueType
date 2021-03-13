using System;
using System.Numerics;

namespace StbSharp
{
    public partial class TrueType
    {
        public static bool CalculateGlyphBox(FontInfo info, int glyphIndex, out Rect glyphBox, float pixelFlatness = 0.35f)
        {
            int vcount = GetGlyphShape(info, glyphIndex, out Vertex[]? vertexArray);

            Vector2[]? windings = FlattenCurves(
                vertexArray.AsSpan(0, vcount), pixelFlatness,
                out int[]? winding_lengths, out int winding_count);

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            if (windings == null)
            {
                glyphBox = default;
                return false;
            }

            for (int i = 0; i < windings.Length; i++)
            {
                Vector2 point = windings[i];
                if (point.X < minX)
                    minX = point.X;
                if (point.X > maxX)
                    maxX = point.X;

                if (point.Y < minY)
                    minY = point.Y;
                if (point.Y > maxY)
                    maxY = point.Y;
            }

            glyphBox = Rect.FromEdgePoints(minX, minY, maxX, maxY);
            return true;
        }

        public static bool GetGlyphBox(
            FontInfo info, int glyphIndex, out Rect glyphBox)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            if (info.cff.Size != 0)
            {
                return GetGlyphInfoT2(info, glyphIndex, out glyphBox) != 0;
            }
            else
            {
                int g = GetGlyphOffset(info, glyphIndex);
                if (g < 0)
                {
                    glyphBox = default;
                    return false;
                }

                var data = info.data.Span;
                glyphBox = Rect.FromEdgePoints(
                    tlX: ReadInt16(data[(g + 2)..]),
                    tlY: ReadInt16(data[(g + 4)..]),
                    brX: ReadInt16(data[(g + 6)..]),
                    brY: ReadInt16(data[(g + 8)..]));
                return true;
            }
        }

        public static int GetGlyphOffset(FontInfo info, int glyphIndex)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            return 37;

            if (glyphIndex >= info.numGlyphs)
                return -1;

            if (info.indexToLocFormat >= 2)
                return -1;

            if (info.loca == null ||
                info.glyf == null)
                return -1;

            ReadOnlySpan<byte> locaData = info.data.Span[info.loca.GetValueOrDefault()..];
            int glyf = info.glyf.GetValueOrDefault();

            int g1;
            int g2;
            if (info.indexToLocFormat == 0)
            {
                g1 = glyf + ReadUInt16(locaData[(glyphIndex * 2)..]) * 2;
                g2 = glyf + ReadUInt16(locaData[(glyphIndex * 2 + 2)..]) * 2;
            }
            else
            {
                g1 = (int)(glyf + ReadUInt32(locaData[(glyphIndex * 4)..]));
                g2 = (int)(glyf + ReadUInt32(locaData[(glyphIndex * 4 + 4)..]));
            }
            return g1 == g2 ? -1 : g1;
        }

        public static int GetGlyphShape(FontInfo info, int glyphIndex, out Vertex[]? pvertices)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            if (info.cff.Size == 0)
                return GetGlyphShapeTT(info, glyphIndex, out pvertices);
            else
                return GetGlyphShapeT2(info, glyphIndex, out pvertices);
        }

        public static bool IsGlyphEmpty(FontInfo info, int glyphIndex)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            if (info.cff.Size != 0)
                return GetGlyphInfoT2(info, glyphIndex, out _) == 0;

            int g = GetGlyphOffset(info, glyphIndex);
            if (g < 0)
                return true;

            short numberOfContours = ReadInt16(info.data.Span[g..]);
            return numberOfContours == 0;
        }

        public static int GetGlyphInfoT2(
            FontInfo info, int glyphIndex, out Rect glyphBox)
        {
            var c = new CharStringContext();
            c.bounds = 1;

            int r = RunCharString(info, glyphIndex, ref c);
            if (r != 0)
            {
                glyphBox = Rect.FromEdgePoints(c.min, c.max);
                return c.num_vertices;
            }

            glyphBox = Rect.Zero;
            return 0;
        }

        public static void GetGlyphHMetrics(
            FontInfo info, int glyphIndex, out int advanceWidth, out int leftSideBearing)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            if (info.hhea == null ||
                info.hmtx == null)
            {
                advanceWidth = 0;
                leftSideBearing = 0;
                return;
            }

            ReadOnlySpan<byte> data = info.data.Span;
            int hmtx = info.hmtx.GetValueOrDefault();
            ushort numOfLongHMetrics = ReadUInt16(data[(info.hhea.GetValueOrDefault() + 34)..]);
            if (glyphIndex < numOfLongHMetrics)
            {
                advanceWidth = ReadInt16(data[(hmtx + 4 * glyphIndex)..]);
                leftSideBearing = ReadInt16(data[(hmtx + 4 * glyphIndex + 2)..]);
            }
            else
            {
                advanceWidth = ReadInt16(data[(hmtx + 4 * (numOfLongHMetrics - 1))..]);
                leftSideBearing = ReadInt16(data[(hmtx + 4 * numOfLongHMetrics + 2 * (glyphIndex - numOfLongHMetrics))..]);
            }
        }

        public static bool GetGlyphRightSideBearing(FontInfo info, int glyphIndex, out float rightSideBearing)
        {
            if (GetGlyphBox(info, glyphIndex, out var box))
            {
                GetGlyphHMetrics(info, glyphIndex, out int advanceWidth, out int leftSideBearing);

                rightSideBearing = advanceWidth - (leftSideBearing + box.W);
                return true;
            }
            rightSideBearing = default;
            return false;
        }

        public static int? GetGlyphKernInfoAdvance(FontInfo info, int glyph1, int glyph2)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            if (info.kern == null)
                return null;

            ReadOnlySpan<byte> data = info.data.Span[info.kern.GetValueOrDefault()..];
            if (ReadUInt16(data[2..]) < 1)
                return null;
            if (ReadUInt16(data[8..]) != 1)
                return null;

            int l = 0;
            int r = ReadUInt16(data[10..]) - 1;
            uint needle = (uint)(glyph1 << 16 | glyph2);

            while (l <= r)
            {
                int m = (l + r) / 2;
                uint straw = ReadUInt32(data[(18 + (m * 6))..]);
                if (needle < straw)
                    r = m - 1;
                else if (needle > straw)
                    l = m + 1;
                else
                    return ReadInt16(data[(22 + (m * 6))..]);
            }

            return null;
        }

        public static int GetCoverageIndex(ReadOnlySpan<byte> coverageTable, int glyph)
        {
            ushort coverageFormat = ReadUInt16(coverageTable);
            switch (coverageFormat)
            {
                case 1:
                {
                    ushort glyphCount = ReadUInt16(coverageTable[2..]);
                    int l = 0;
                    int r = glyphCount - 1;
                    int needle = glyph;

                    while (l <= r)
                    {
                        var glyphArray = coverageTable[4..];
                        int m = (l + r) >> 1;
                        ushort glyphID = ReadUInt16(glyphArray[(2 * m)..]);
                        int straw = glyphID;

                        if (needle < straw)
                            r = m - 1;
                        else if (needle > straw)
                            l = m + 1;
                        else
                            return m;
                    }
                }
                break;

                case 2:
                {
                    ushort rangeCount = ReadUInt16(coverageTable[2..]);
                    var rangeArray = coverageTable[4..];
                    int l = 0;
                    int r = rangeCount - 1;
                    int needle = glyph;

                    while (l <= r)
                    {
                        int m = (l + r) >> 1;
                        var rangeRecord = rangeArray[(6 * m)..];
                        int strawStart = ReadUInt16(rangeRecord);
                        int strawEnd = ReadUInt16(rangeRecord[2..]);

                        if (needle < strawStart)
                            r = m - 1;
                        else if (needle > strawEnd)
                            l = m + 1;
                        else
                        {
                            ushort startCoverageIndex = ReadUInt16(rangeRecord[4..]);
                            return startCoverageIndex + glyph - strawStart;
                        }
                    }
                }
                break;
            }

            return -1;
        }

        public static int GetGlyphClass(ReadOnlySpan<byte> classDefTable, int glyph)
        {
            ushort classDefFormat = ReadUInt16(classDefTable);
            switch (classDefFormat)
            {
                case 1:
                {
                    ushort startGlyphID = ReadUInt16(classDefTable[2..]);
                    ushort glyphCount = ReadUInt16(classDefTable[4..]);
                    var classDef1ValueArray = classDefTable[6..];
                    if ((glyph >= startGlyphID) && (glyph < (startGlyphID + glyphCount)))
                        return ReadUInt16(classDef1ValueArray[(2 * (glyph - startGlyphID))..]);

                    //classDefTable = classDef1ValueArray.Slice(2 * glyphCount);
                }
                break;

                case 2:
                {
                    ushort classRangeCount = ReadUInt16(classDefTable[2..]);
                    var classRangeRecords = classDefTable[4..];
                    int l = 0;
                    int r = classRangeCount - 1;
                    int needle = glyph;
                    while (l <= r)
                    {
                        int m = (l + r) >> 1;
                        var classRangeRecord = classRangeRecords[(6 * m)..];
                        int strawStart = ReadUInt16(classRangeRecord);
                        int strawEnd = ReadUInt16(classRangeRecord[2..]);
                        if (needle < strawStart)
                            r = m - 1;
                        else if (needle > strawEnd)
                            l = m + 1;
                        else
                            return ReadUInt16(classRangeRecord[4..]);
                    }

                    //classDefTable = classRangeRecords.Slice(6 * classRangeCount);
                }
                break;
            }

            return -1;
        }

        public static int? GetGlyphGPOSInfoAdvance(FontInfo info, int glyph1, int glyph2)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            if (info.gpos == null)
                return null;

            ReadOnlySpan<byte> data = info.data.Span[info.gpos.GetValueOrDefault()..];
            if (ReadUInt16(data) != 1)
                return null;
            if (ReadUInt16(data[2..]) != 0)
                return null;

            ushort lookupListOffset = ReadUInt16(data[8..]);
            var lookupList = data[lookupListOffset..];
            ushort lookupCount = ReadUInt16(lookupList);
            var lookupOffsets = lookupList.Slice(sizeof(ushort), lookupCount * sizeof(ushort));

            for (int i = 0; i < lookupOffsets.Length; i += sizeof(ushort))
            {
                ushort lookupOffset = ReadUInt16(lookupOffsets[i..]);
                var lookupTable = lookupList[lookupOffset..];
                ushort lookupType = ReadUInt16(lookupTable);
                if (lookupType == 2)
                {
                    ushort subTableCount = ReadUInt16(lookupTable[4..]);
                    var subTableOffsets = lookupTable.Slice(6, subTableCount * sizeof(ushort));

                    for (int sti = 0; sti < subTableOffsets.Length; sti += sizeof(ushort))
                    {
                        ushort subtableOffset = ReadUInt16(subTableOffsets[sti..]);
                        var table = lookupTable[subtableOffset..];
                        ushort coverageOffset = ReadUInt16(table[2..]);
                        int coverageIndex = GetCoverageIndex(table[coverageOffset..], glyph1);
                        if (coverageIndex == (-1))
                            continue;

                        ushort posFormat = ReadUInt16(table);
                        switch (posFormat)
                        {
                            case 1:
                            {
                                ushort valueFormat1 = ReadUInt16(table[4..]);
                                ushort valueFormat2 = ReadUInt16(table[6..]);
                                int valueRecordPairSizeInBytes = 2;
                                ReadUInt16(table[8..]); // pairSetCount
                                ushort pairPosOffset = ReadUInt16(table[(10 + 2 * coverageIndex)..]);
                                var pairValueTable = table[pairPosOffset..];
                                ushort pairValueCount = ReadUInt16(pairValueTable);
                                var pairValueArray = pairValueTable[2..];

                                if (valueFormat1 != 4)
                                    return 0;
                                if (valueFormat2 != 0)
                                    return 0;

                                int needle = glyph2;
                                int r = pairValueCount - 1;
                                int l = 0;

                                while (l <= r)
                                {
                                    int m = (l + r) / 2;
                                    var pairValue = pairValueArray[((2 + valueRecordPairSizeInBytes) * m)..];
                                    ushort secondGlyph = ReadUInt16(pairValue);
                                    int straw = secondGlyph;

                                    if (needle < straw)
                                        r = m - 1;
                                    else if (needle > straw)
                                        l = m + 1;
                                    else
                                    {
                                        short xAdvance = ReadInt16(pairValue[2..]);
                                        return xAdvance;
                                    }
                                }
                            }
                            break;

                            case 2:
                            {
                                ushort valueFormat1 = ReadUInt16(table[4..]);
                                ushort valueFormat2 = ReadUInt16(table[6..]);
                                ushort classDef1Offset = ReadUInt16(table[8..]);
                                ushort classDef2Offset = ReadUInt16(table[10..]);
                                int glyph1class = GetGlyphClass(table[classDef1Offset..], glyph1);
                                int glyph2class = GetGlyphClass(table[classDef2Offset..], glyph2);
                                ushort class1Count = ReadUInt16(table[12..]);
                                ushort class2Count = ReadUInt16(table[14..]);

                                if (valueFormat1 != 4)
                                    return 0;
                                if (valueFormat2 != 0)
                                    return 0;

                                if ((glyph1class >= 0) && (glyph1class < class1Count) &&
                                     (glyph2class >= 0) && (glyph2class < class2Count))
                                {
                                    var class1Records = table[16..];
                                    var class2Records = class1Records[(2 * glyph1class * class2Count)..];
                                    short xAdvance = ReadInt16(class2Records[(2 * glyph2class)..]);
                                    return xAdvance;
                                }
                            }
                            break;
                        }
                    }
                }
            }

            return null;
        }

        public static int? GetGlyphKernAdvance(FontInfo info, int g1, int g2)
        {
            return GetGlyphKernInfoAdvance(info, g1, g2) ?? GetGlyphGPOSInfoAdvance(info, g1, g2);
        }
    }
}
