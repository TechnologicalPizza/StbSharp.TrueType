﻿using System;
using System.Runtime.InteropServices;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class TrueType
    {
        public static bool IsFont(ReadOnlySpan<byte> fontData)
        {
            if (fontData[0] == '1' &&
                fontData[1] == 0 &&
                fontData[2] == 0 &&
                fontData[3] == 0)
                return true;

            if (fontData[0] == "typ1"[0] &&
                fontData[1] == "typ1"[1] &&
                fontData[2] == "typ1"[2] &&
                fontData[3] == "typ1"[3])
                return true;

            if (fontData[0] == "OTTO"[0] &&
                fontData[1] == "OTTO"[1] &&
                fontData[2] == "OTTO"[2] &&
                fontData[3] == "OTTO"[3])
                return true;

            if (fontData[0] == 0 &&
                fontData[1] == 1 &&
                fontData[2] == 0 &&
                fontData[3] == 0)
                return true;

            if (fontData[0] == "true"[0] &&
                fontData[1] == "true"[1] &&
                fontData[2] == "true"[2] &&
                fontData[3] == "true"[3])
                return true;

            return false;
        }

        public static int GetFontOffset(ReadOnlySpan<byte> fontData, int index)
        {
            if (IsFont(fontData))
                return index == 0 ? 0 : -1;

            if (fontData[0] == "ttcf"[0] &&
                fontData[1] == "ttcf"[1] &&
                fontData[2] == "ttcf"[2] &&
                fontData[3] == "ttcf"[3])
            {
                if ((ReadUInt32(fontData.Slice(4)) == 0x00010000) ||
                    (ReadUInt32(fontData.Slice(4)) == 0x00020000))
                {
                    int n = ReadInt32(fontData.Slice(8));
                    if (index >= n)
                        return -1;
                    return (int)ReadUInt32(fontData.Slice(12 + index * 4));
                }
            }

            return -1;
        }

        public static int GetFontCount(ReadOnlySpan<byte> fontData)
        {
            if (IsFont(fontData))
                return 1;

            if (fontData[0] == "ttcf"[0] &&
                fontData[1] == "ttcf"[1] &&
                fontData[2] == "ttcf"[2] &&
                fontData[3] == "ttcf"[3])
            {
                uint x = ReadUInt32(fontData.Slice(4));
                if (x == 0x00010000 || x == 0x00020000)
                    return ReadInt32(fontData.Slice(8));
            }

            return 0;
        }

        public static Buffer GetSubRs(Buffer cff, Buffer fontdict)
        {
            Span<uint> tmp = stackalloc uint[2];
            tmp.Fill(0);

            var pdict = new Buffer();
            DictGetInts(ref fontdict, 18, tmp);
            if ((tmp[1] == 0) || (tmp[0] == 0))
                return Buffer.EmptyWithLength(9);

            pdict = cff.Slice((int)tmp[1], (int)tmp[0]);

            uint subsroff = 0;
            DictGetInts(ref pdict, 19, MemoryMarshal.CreateSpan(ref subsroff, 1));
            if (subsroff == 0)
                return Buffer.Empty;

            cff.Seek((int)(tmp[1] + subsroff));
            return CffGetIndex(ref cff);
        }

        public static bool InitFont(FontInfo info, ReadOnlyMemory<byte> fontData, int fontstart)
        {
            info.data = fontData;
            info.fontstart = fontstart;
            info.cff = Buffer.Empty;
            var data = fontData.Span;
            int cmap = (int)FindTable(data, fontstart, "cmap");
            info.loca = (int)FindTable(data, fontstart, "loca");
            info.head = (int)FindTable(data, fontstart, "head");
            info.glyf = (int)FindTable(data, fontstart, "glyf");
            info.hhea = (int)FindTable(data, fontstart, "hhea");
            info.hmtx = (int)FindTable(data, fontstart, "hmtx");
            info.kern = (int)FindTable(data, fontstart, "kern");
            info.gpos = (int)FindTable(data, fontstart, "GPOS");
            if ((cmap == 0) || (info.head == 0) || (info.hhea == 0) || (info.hmtx == 0))
                return false;

            if (info.glyf != 0)
            {
                if (info.loca == 0)
                    return false;
            }
            else
            {
                uint cstype = 2;
                uint charstrings = 0;
                uint fdarrayoff = 0;
                uint fdselectoff = 0;
                int cff = (int)FindTable(data, fontstart, "CFF ");
                if (cff == 0)
                    return false;

                info.fontdicts = Buffer.Empty;
                info.fdselect = Buffer.Empty;
                info.cff = new Buffer(fontData.Slice(cff), 512 * 1024 * 1024);
                var b = info.cff;
                b.Skip(2);
                b.Seek(b.GetByte());
                CffGetIndex(ref b);
                var topdictIndex = CffGetIndex(ref b);
                var topdict = CffIndexGet(topdictIndex, 0);
                CffGetIndex(ref b);
                info.gsubrs = CffGetIndex(ref b);
                DictGetInts(ref topdict, 17, MemoryMarshal.CreateSpan(ref charstrings, 1));
                DictGetInts(ref topdict, 0x100 | 6, MemoryMarshal.CreateSpan(ref cstype, 1));
                DictGetInts(ref topdict, 0x100 | 36, MemoryMarshal.CreateSpan(ref fdarrayoff, 1));
                DictGetInts(ref topdict, 0x100 | 37, MemoryMarshal.CreateSpan(ref fdselectoff, 1));
                info.subrs = GetSubRs(b, topdict);
                if (cstype != 2 || charstrings == 0)
                    return false;

                if (fdarrayoff != 0)
                {
                    if (fdselectoff == 0)
                        return false;

                    b.Seek((int)fdarrayoff);
                    info.fontdicts = CffGetIndex(ref b);
                    info.fdselect = b.Slice((int)fdselectoff, (int)(b.size - fdselectoff));
                }

                b.Seek((int)charstrings);
                info.charstrings = CffGetIndex(ref b);
            }

            int t = (int)FindTable(data, fontstart, "maxp");
            if (t != 0)
                info.numGlyphs = ReadUInt16(data.Slice(t + 4));
            else
                info.numGlyphs = 0xffff;

            info.svg = -1;

            int numTables = ReadUInt16(data.Slice(cmap + 2));
            info.index_map = 0;
            for (int i = 0; i < numTables; ++i)
            {
                int encoding_record = cmap + 4 + 8 * i;
                switch (ReadUInt16(data.Slice(encoding_record)))
                {
                    case STBTT_PLATFORM_ID_MICROSOFT:
                        switch (ReadUInt16(data.Slice(encoding_record + 2)))
                        {
                            case STBTT_MS_EID_UNICODE_BMP:
                            case STBTT_MS_EID_UNICODE_FULL:
                                info.index_map = (int)(cmap + ReadUInt32(data.Slice(encoding_record + 4)));
                                break;
                        }
                        break;

                    case STBTT_PLATFORM_ID_UNICODE:
                        info.index_map = (int)(cmap + ReadUInt32(data.Slice(encoding_record + 4)));
                        break;
                }
            }

            if (info.index_map == 0)
                return false;

            info.indexToLocFormat = ReadUInt16(data.Slice(info.head + 50));
            return true;
        }

        public static Buffer CidGetGlyphSubRs(FontInfo info, int glyph_index)
        {
            var fdselect = info.fdselect;
            int fdselector = -1;
            fdselect.Seek(0);

            int fmt = fdselect.GetByte();
            if (fmt == 0)
            {
                fdselect.Skip(glyph_index);
                fdselector = fdselect.GetByte();
            }
            else if (fmt == 3)
            {
                int nranges = (int)fdselect.Get(2);
                int start = (int)fdselect.Get(2);
                for (int i = 0; i < nranges; i++)
                {
                    int v = fdselect.GetByte();
                    int end = (int)fdselect.Get(2);
                    if ((glyph_index >= start) && (glyph_index < end))
                    {
                        fdselector = v;
                        break;
                    }
                    start = end;
                }
            }

            return GetSubRs(info.cff, CffIndexGet(info.fontdicts, fdselector));
        }

        public static int GetKerningTableLength(FontInfo info)
        {
            var data = info.data.Span.Slice(info.kern);

            // we only look at the first table. it must be 'horizontal' and format 0.
            if (info.kern == 0)
                return 0;
            if (ReadUInt16(data.Slice(2)) < 1) // number of tables, need at least 1
                return 0;
            if (ReadUInt16(data.Slice(8)) != 1) // horizontal flag must be set in format
                return 0;

            return ReadUInt16(data.Slice(10));
        }

        public static int GetKerningTable(FontInfo info, KerningEntry* table, int table_length)
        {
            var data = info.data.Span.Slice(info.kern);

            // we only look at the first table. it must be 'horizontal' and format 0.
            if (info.kern == 0)
                return 0;
            if (ReadUInt16(data.Slice(2)) < 1) // number of tables, need at least 1
                return 0;
            if (ReadUInt16(data.Slice(8)) != 1) // horizontal flag must be set in format
                return 0;

            int length = ReadUInt16(data.Slice(10));
            if (table_length < length)
                length = table_length;

            for (int k = 0; k < length; k++)
            {
                table[k].glyph1 = ReadUInt16(data.Slice(18 + (k * 6)));
                table[k].glyph2 = ReadUInt16(data.Slice(20 + (k * 6)));
                table[k].advance = ReadInt16(data.Slice(22 + (k * 6)));
            }

            return length;
        }
    }
}