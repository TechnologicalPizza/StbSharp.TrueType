using System;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class TrueType
    {
        public static bool GetGlyphBox(
            FontInfo info, int glyphIndex, out Rect glyphBox)
        {
            if (info.cff.size != 0)
            {
                return GetGlyphInfoT2(info, glyphIndex, out glyphBox) != 0;
            }
            else
            {
                int g = GetGlyphOffset(info, glyphIndex);
                if (g < 0)
                {
                    glyphBox = Rect.Zero;
                    return false;
                }

                var data = info.data.Span;
                glyphBox = Rect.FromEdgePoints(
                    tlX: ReadInt16(data.Slice(g + 2)),
                    tlY: ReadInt16(data.Slice(g + 4)),
                    brX: ReadInt16(data.Slice(g + 6)),
                    brY: ReadInt16(data.Slice(g + 8)));
                return true;
            }
        }

        public static int GetGlyphOffset(FontInfo info, int glyphIndex)
        {
            if (glyphIndex >= info.numGlyphs)
                return -1;

            if (info.indexToLocFormat >= 2)
                return -1;

            int g1;
            int g2;
            var locaData = info.data.Span.Slice(info.loca);
            if (info.indexToLocFormat == 0)
            {
                g1 = info.glyf + ReadUInt16(locaData.Slice(glyphIndex * 2)) * 2;
                g2 = info.glyf + ReadUInt16(locaData.Slice(glyphIndex * 2 + 2)) * 2;
            }
            else
            {
                g1 = (int)(info.glyf + ReadUInt32(locaData.Slice(glyphIndex * 4)));
                g2 = (int)(info.glyf + ReadUInt32(locaData.Slice(glyphIndex * 4 + 4)));
            }
            return g1 == g2 ? -1 : g1;
        }

        public static int GetGlyphShape(FontInfo info, int glyphIndex, out Vertex[]? pvertices)
        {
            if (info.cff.size == 0)
                return GetGlyphShapeTT(info, glyphIndex, out pvertices);
            else
                return GetGlyphShapeT2(info, glyphIndex, out pvertices);
        }

        public static bool IsGlyphEmpty(FontInfo info, int glyphIndex)
        {
            if (info.cff.size != 0)
                return GetGlyphInfoT2(info, glyphIndex, out _) == 0;

            int g = GetGlyphOffset(info, glyphIndex);
            if (g < 0)
                return true;

            short numberOfContours = ReadInt16(info.data.Span.Slice(g));
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
            ushort numOfLongHorMetrics = ReadUInt16(info.data.Span.Slice(info.hhea + 34));
            if (glyphIndex < numOfLongHorMetrics)
            {
                advanceWidth = ReadInt16(info.data.Span.Slice(info.hmtx + 4 * glyphIndex));
                leftSideBearing = ReadInt16(info.data.Span.Slice(info.hmtx + 4 * glyphIndex + 2));
            }
            else
            {
                advanceWidth = ReadInt16(info.data.Span.Slice(
                    info.hmtx + 4 * (numOfLongHorMetrics - 1)));

                leftSideBearing = ReadInt16(info.data.Span.Slice(
                    info.hmtx + 4 * numOfLongHorMetrics + 2 * (glyphIndex - numOfLongHorMetrics)));
            }
        }

        public static int GetGlyphKernInfoAdvance(FontInfo info, int glyph1, int glyph2)
        {
            var data = info.data.Span.Slice(info.kern);
            if (info.kern == 0)
                return 0;
            if (ReadUInt16(data.Slice(2)) < 1)
                return 0;
            if (ReadUInt16(data.Slice(8)) != 1)
                return 0;

            int l = 0;
            int r = ReadUInt16(data.Slice(10)) - 1;
            uint needle = (uint)(glyph1 << 16 | glyph2);

            while (l <= r)
            {
                int m = (l + r) >> 1;
                uint straw = ReadUInt32(data.Slice(18 + (m * 6)));
                if (needle < straw)
                    r = m - 1;
                else if (needle > straw)
                    l = m + 1;
                else
                    return ReadInt16(data.Slice(22 + (m * 6)));
            }

            return 0;
        }

        public static int GetCoverageIndex(ReadOnlySpan<byte> coverageTable, int glyph)
        {
            ushort coverageFormat = ReadUInt16(coverageTable);
            switch (coverageFormat)
            {
                case 1:
                {
                    ushort glyphCount = ReadUInt16(coverageTable.Slice(2));
                    int l = 0;
                    int r = glyphCount - 1;
                    int needle = glyph;
                    while (l <= r)
                    {
                        var glyphArray = coverageTable.Slice(4);
                        int m = (l + r) >> 1;
                        ushort glyphID = ReadUInt16(glyphArray.Slice(2 * m));
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
                    ushort rangeCount = ReadUInt16(coverageTable.Slice(2));
                    var rangeArray = coverageTable.Slice(4);
                    int l = 0;
                    int r = rangeCount - 1;
                    int needle = glyph;
                    while (l <= r)
                    {
                        int m = (l + r) >> 1;
                        var rangeRecord = rangeArray.Slice(6 * m);
                        int strawStart = ReadUInt16(rangeRecord);
                        int strawEnd = ReadUInt16(rangeRecord.Slice(2));
                        if (needle < strawStart)
                            r = m - 1;
                        else if (needle > strawEnd)
                            l = m + 1;
                        else
                        {
                            ushort startCoverageIndex = ReadUInt16(rangeRecord.Slice(4));
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
                    ushort startGlyphID = ReadUInt16(classDefTable.Slice(2));
                    ushort glyphCount = ReadUInt16(classDefTable.Slice(4));
                    var classDef1ValueArray = classDefTable.Slice(6);
                    if ((glyph >= startGlyphID) && (glyph < (startGlyphID + glyphCount)))
                        return ReadUInt16(classDef1ValueArray.Slice(2 * (glyph - startGlyphID)));

                    //classDefTable = classDef1ValueArray.Slice(2 * glyphCount);
                }
                break;

                case 2:
                {
                    ushort classRangeCount = ReadUInt16(classDefTable.Slice(2));
                    var classRangeRecords = classDefTable.Slice(4);
                    int l = 0;
                    int r = classRangeCount - 1;
                    int needle = glyph;
                    while (l <= r)
                    {
                        int m = (l + r) >> 1;
                        var classRangeRecord = classRangeRecords.Slice(6 * m);
                        int strawStart = ReadUInt16(classRangeRecord);
                        int strawEnd = ReadUInt16(classRangeRecord.Slice(2));
                        if (needle < strawStart)
                            r = m - 1;
                        else if (needle > strawEnd)
                            l = m + 1;
                        else
                            return ReadUInt16(classRangeRecord.Slice(4));
                    }

                    //classDefTable = classRangeRecords.Slice(6 * classRangeCount);
                }
                break;
            }

            return -1;
        }

        public static int GetGlyphGPOSInfoAdvance(FontInfo info, int glyph1, int glyph2)
        {
            if (info.gpos == 0)
                return 0;

            var data = info.data.Span.Slice(info.gpos);
            if (ReadUInt16(data) != 1)
                return 0;
            if (ReadUInt16(data.Slice(2)) != 0)
                return 0;

            ushort lookupListOffset = ReadUInt16(data.Slice(8));
            var lookupList = data.Slice(lookupListOffset);
            ushort lookupCount = ReadUInt16(lookupList);

            for (int i = 0; i < lookupCount; ++i)
            {
                ushort lookupOffset = ReadUInt16(lookupList.Slice(2 + 2 * i));
                var lookupTable = lookupList.Slice(lookupOffset);
                ushort lookupType = ReadUInt16(lookupTable);
                ushort subTableCount = ReadUInt16(lookupTable.Slice(4));
                var subTableOffsets = lookupTable.Slice(6);
                if (lookupType == 2)
                {
                    for (int sti = 0; sti < subTableCount; sti++)
                    {
                        ushort subtableOffset = ReadUInt16(subTableOffsets.Slice(2 * sti));
                        var table = lookupTable.Slice(subtableOffset);
                        ushort posFormat = ReadUInt16(table);
                        ushort coverageOffset = ReadUInt16(table.Slice(2));
                        int coverageIndex = GetCoverageIndex(table.Slice(coverageOffset), glyph1);
                        if (coverageIndex == (-1))
                            continue;

                        switch (posFormat)
                        {
                            case 1:
                            {
                                ushort valueFormat1 = ReadUInt16(table.Slice(4));
                                ushort valueFormat2 = ReadUInt16(table.Slice(6));
                                int valueRecordPairSizeInBytes = 2;
                                ReadUInt16(table.Slice(8)); // pairSetCount
                                ushort pairPosOffset = ReadUInt16(table.Slice(10 + 2 * coverageIndex));
                                var pairValueTable = table.Slice(pairPosOffset);
                                ushort pairValueCount = ReadUInt16(pairValueTable);
                                var pairValueArray = pairValueTable.Slice(2);
                                if (valueFormat1 != 4)
                                    return 0;
                                if (valueFormat2 != 0)
                                    return 0;

                                int needle = glyph2;
                                int r = pairValueCount - 1;
                                int l = 0;
                                while (l <= r)
                                {
                                    int m = (l + r) >> 1;
                                    var pairValue = pairValueArray.Slice((2 + valueRecordPairSizeInBytes) * m);
                                    ushort secondGlyph = ReadUInt16(pairValue);
                                    int straw = secondGlyph;
                                    if (needle < straw)
                                        r = m - 1;
                                    else if (needle > straw)
                                        l = m + 1;
                                    else
                                    {
                                        short xAdvance = ReadInt16(pairValue.Slice(2));
                                        return xAdvance;
                                    }
                                }
                            }
                            break;

                            case 2:
                            {
                                ushort valueFormat1 = ReadUInt16(table.Slice(4));
                                ushort valueFormat2 = ReadUInt16(table.Slice(6));
                                ushort classDef1Offset = ReadUInt16(table.Slice(8));
                                ushort classDef2Offset = ReadUInt16(table.Slice(10));
                                int glyph1class = GetGlyphClass(table.Slice(classDef1Offset), glyph1);
                                int glyph2class = GetGlyphClass(table.Slice(classDef2Offset), glyph2);
                                ushort class1Count = ReadUInt16(table.Slice(12));
                                ushort class2Count = ReadUInt16(table.Slice(14));

                                if (valueFormat1 != 4)
                                    return 0;
                                if (valueFormat2 != 0)
                                    return 0;
                                if ((glyph1class >= 0) && (glyph1class < class1Count) &&
                                     (glyph2class >= 0) && (glyph2class < class2Count))
                                {
                                    var class1Records = table.Slice(16);
                                    var class2Records = class1Records.Slice(2 * glyph1class * class2Count);
                                    short xAdvance = ReadInt16(class2Records.Slice(2 * glyph2class));
                                    return xAdvance;
                                }
                            }
                            break;
                        }
                    }
                }
            }

            return 0;
        }

        public static int GetGlyphKernAdvance(FontInfo info, int g1, int g2)
        {
            if (info.gpos != 0)
                return GetGlyphGPOSInfoAdvance(info, g1, g2);
            if (info.kern != 0)
                return GetGlyphKernInfoAdvance(info, g1, g2);
            return 0;
        }
    }
}
