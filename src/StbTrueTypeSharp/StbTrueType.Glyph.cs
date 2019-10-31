﻿using System;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
	internal
#endif
    unsafe partial class StbTrueType
    {
        public static int GetGlyphBox(
            TTFontInfo info, int glyph_index, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = 0;
            y0 = 0;
            x1 = 0;
            y1 = 0;

            if (info.cff.size != 0)
            {
                GetGlyphInfoT2(info, glyph_index, out x0, out y0, out x1, out y1);
            }
            else
            {
                int g = GetGlyphOffset(info, glyph_index);
                if (g < 0)
                    return 0;

                var data = info.data.Span;
                x0 = ReadInt16(data.Slice(g + 2));
                y0 = ReadInt16(data.Slice(g + 4));
                x1 = ReadInt16(data.Slice(g + 6));
                y1 = ReadInt16(data.Slice(g + 8));
            }
            return 1;
        }

        public static int GetGlyphOffset(TTFontInfo info, int glyph_index)
        {
            if (glyph_index >= info.numGlyphs)
                return -1;

            if (info.indexToLocFormat >= 2)
                return -1;

            int g1;
            int g2;
            if (info.indexToLocFormat == 0)
            {
                g1 = info.glyf + ReadUInt16(info.data.Span.Slice(info.loca + glyph_index * 2)) * 2;
                g2 = info.glyf + ReadUInt16(info.data.Span.Slice(info.loca + glyph_index * 2 + 2)) * 2;
            }
            else
            {
                g1 = (int)(info.glyf + ReadUInt32(info.data.Span.Slice(info.loca + glyph_index * 4)));
                g2 = (int)(info.glyf + ReadUInt32(info.data.Span.Slice(info.loca + glyph_index * 4 + 4)));
            }
            return g1 == g2 ? -1 : g1;
        }

        public static int GetGlyphShape(TTFontInfo info, int glyph_index, out TTVertex* pvertices)
        {
            if (info.cff.size == 0)
                return GetGlyphShapeTT(info, glyph_index, out pvertices);
            else
                return GetGlyphShapeT2(info, glyph_index, out pvertices);
        }

        public static int IsGlyphEmpty(TTFontInfo info, int glyph_index)
        {
            if (info.cff.size != 0)
                return GetGlyphInfoT2(info, glyph_index, out _, out _, out _, out _) == 0 ? 1 : 0;

            int g = GetGlyphOffset(info, glyph_index);
            if (g < 0)
                return 1;

            short numberOfContours = ReadInt16(info.data.Span.Slice(g));
            return numberOfContours == 0 ? 1 : 0;
        }

        public static int GetGlyphInfoT2(
            TTFontInfo info, int glyph_index, out int x0, out int y0, out int x1, out int y1)
        {
            var c = new TTCharStringContext();
            c.bounds = 1;

            int r = RunCharString(info, glyph_index, &c);
            x0 = r != 0 ? c.min_x : 0;
            y0 = r != 0 ? c.min_y : 0;
            x1 = r != 0 ? c.max_x : 0;
            y1 = r != 0 ? c.max_y : 0;
            return r != 0 ? c.num_vertices : 0;
        }

        public static void GetGlyphHMetrics(
            TTFontInfo info, int glyph_index, out int advanceWidth, out int leftSideBearing)
        {
            ushort numOfLongHorMetrics = ReadUInt16(info.data.Span.Slice(info.hhea + 34));
            if (glyph_index < numOfLongHorMetrics)
            {
                advanceWidth = ReadInt16(info.data.Span.Slice(info.hmtx + 4 * glyph_index));
                leftSideBearing = ReadInt16(info.data.Span.Slice(info.hmtx + 4 * glyph_index + 2));
            }
            else
            {
                advanceWidth = ReadInt16(info.data.Span.Slice(info.hmtx + 4 * (numOfLongHorMetrics - 1)));
                leftSideBearing = ReadInt16(
                    info.data.Span.Slice(info.hmtx + 4 * numOfLongHorMetrics + 2 * (glyph_index - numOfLongHorMetrics)));
            }
        }

        public static int GetGlyphKernInfoAdvance(TTFontInfo info, int glyph1, int glyph2)
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

        public static int GetGlyphGPOSInfoAdvance(TTFontInfo info, int glyph1, int glyph2)
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

        public static int GetGlyphKernAdvance(TTFontInfo info, int g1, int g2)
        {
            int xAdvance = 0;
            if (info.gpos != 0)
                xAdvance += GetGlyphGPOSInfoAdvance(info, g1, g2);
            if (info.kern != 0)
                xAdvance += GetGlyphKernInfoAdvance(info, g1, g2);
            return xAdvance;
        }
    }
}