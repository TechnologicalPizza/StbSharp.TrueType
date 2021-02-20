using System;
using System.Runtime.InteropServices;

namespace StbSharp
{
    public partial class TrueType
    {
        public static bool IsFont(ReadOnlySpan<byte> fontData)
        {
            if (fontData.Length < 4)
                return false;

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

        public static int GetFontOffset(ReadOnlySpan<byte> fontData)
        {
            if (IsFont(fontData))
                return 0;

            if (fontData.Length < 4)
                return -1;

            if (fontData[0] == "ttcf"[0] &&
                fontData[1] == "ttcf"[1] &&
                fontData[2] == "ttcf"[2] &&
                fontData[3] == "ttcf"[3])
            {
                if ((ReadUInt32(fontData[4..]) == 0x00010000) ||
                    (ReadUInt32(fontData[4..]) == 0x00020000))
                {
                    int n = ReadInt32(fontData[8..]);
                    if (n <= 0)
                        return -1;
                    
                    return (int)ReadUInt32(fontData[12..]);
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
                uint x = ReadUInt32(fontData[4..]);
                if (x == 0x00010000 || x == 0x00020000)
                    return ReadInt32(fontData[8..]);
            }

            return 0;
        }

        public static Buffer GetSubRs(Buffer cff, Buffer fontdict)
        {
            Span<uint> tmp = stackalloc uint[2] { 0, 0 };

            var pdict = new Buffer();
            DictGetInts(ref fontdict, 18, tmp);

            if (tmp[0] == 0 || tmp[1] == 0)
                return Buffer.Empty;

            pdict = cff.Slice((int)tmp[1], (int)tmp[0]);

            uint subsroff = 0;
            DictGetInts(ref pdict, 19, MemoryMarshal.CreateSpan(ref subsroff, 1));
            if (subsroff == 0)
                return Buffer.Empty;

            cff.Seek((int)(tmp[1] + subsroff));
            return CffGetIndex(ref cff);
        }

        public static bool InitFont(
            FontInfo info, ReadOnlyMemory<byte> fontData)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            info.data = fontData;
            info.cff = Buffer.Empty;

            ReadOnlySpan<byte> data = fontData.Span;
            int? cmap = FindTable(data, "cmap");
            info.loca = FindTable(data, "loca");
            info.head = FindTable(data, "head");
            info.glyf = FindTable(data, "glyf");
            info.hhea = FindTable(data, "hhea");
            info.hmtx = FindTable(data, "hmtx");
            info.kern = FindTable(data, "kern");
            info.gpos = FindTable(data, "GPOS");

            if ((cmap == null) ||
                (info.head == null) ||
                (info.hhea == null) ||
                (info.hmtx == null))
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
                int? cff = FindTable(data, "CFF ");
                if (cff == null)
                    return false;

                info.fontdicts = Buffer.Empty;
                info.fdselect = Buffer.Empty;
                info.cff = new Buffer(fontData[cff.GetValueOrDefault()..]);

                var b = info.cff;
                b.Skip(2);
                b.Seek(b.GetByte());
                CffGetIndex(ref b);
                var topdictIndex = CffGetIndex(ref b);
                var topdict = CffIndexGet(topdictIndex, 0);
                CffGetIndex(ref b);
                info.gsubrs = CffGetIndex(ref b);
                DictGetInts(ref topdict, 0x000 | 17, MemoryMarshal.CreateSpan(ref charstrings, 1));
                DictGetInts(ref topdict, 0x100 | 06, MemoryMarshal.CreateSpan(ref cstype, 1));
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
                    info.fdselect = b.Slice((int)fdselectoff, (int)(b.Size - fdselectoff));
                }

                b.Seek((int)charstrings);
                info.charstrings = CffGetIndex(ref b);
            }

            int? t = FindTable(data, "maxp");
            if (t != null)
                info.numGlyphs = ReadUInt16(data[(t.GetValueOrDefault() + 4)..]);

            info.svg = -1;

            int numTables = ReadUInt16(data[(cmap.GetValueOrDefault() + 2)..]);
            info.index_map = 0;
            for (int i = 0; i < numTables; i++)
            {
                int encoding_record = cmap.GetValueOrDefault() + 4 + 8 * i;
                var pId = (FontPlatformID)ReadUInt16(data[encoding_record..]);
                switch (pId)
                {
                    case FontPlatformID.Microsoft:
                        var msEid = (FontMicrosoftEncodingID)ReadUInt16(data[(encoding_record + 2)..]);
                        switch (msEid)
                        {
                            case FontMicrosoftEncodingID.UnicodeBmp:
                            case FontMicrosoftEncodingID.UnicodeFull:
                                info.index_map = (int)(cmap.GetValueOrDefault() + ReadUInt32(data[(encoding_record + 4)..]));
                                break;

                            case FontMicrosoftEncodingID.Symbol:

                                break;
                        }
                        break;

                    case FontPlatformID.Unicode:
                        info.index_map = (int)(cmap.GetValueOrDefault() + ReadUInt32(data[(encoding_record + 4)..]));
                        break;
                }
            }

            info.indexToLocFormat = ReadUInt16(data[(info.head.GetValueOrDefault() + 50)..]);

            if (info.index_map == 0)
                return false;

            return true;
        }

        public static Buffer CidGetGlyphSubRs(FontInfo info, int glyphIndex)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            var fdselect = info.fdselect;
            int fdselector = -1;
            fdselect.Seek(0);

            int fmt = fdselect.GetByte();
            if (fmt == 0)
            {
                fdselect.Skip(glyphIndex);
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
                    if ((glyphIndex >= start) && (glyphIndex < end))
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
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            if (info.kern == null)
                return 0;

            ReadOnlySpan<byte> data = info.data.Span[info.kern.GetValueOrDefault()..];

            // we only look at the first table. it must be 'horizontal' and format 0.
            if (info.kern == 0)
                return 0;

            if (ReadUInt16(data[2..]) < 1) // number of tables, need at least 1
                return 0;

            if (ReadUInt16(data[8..]) != 1) // horizontal flag must be set in format
                return 0;

            return ReadUInt16(data[10..]);
        }

        public static int GetKerningTable(FontInfo info, Span<KerningEntry> destination)
        {
            int length = GetKerningTableLength(info);
            if (length == 0)
                return 0;

            if (info.kern == null)
                return 0;

            ReadOnlySpan<byte> data = info.data.Span[info.kern.GetValueOrDefault()..];
            if (destination.Length > length)
                destination = destination.Slice(0, length);

            for (int i = 0; i < destination.Length; i++)
            {
                destination[i].glyph1 = ReadUInt16(data[(18 + (i * 6))..]);
                destination[i].glyph2 = ReadUInt16(data[(20 + (i * 6))..]);
                destination[i].advance = ReadInt16(data[(22 + (i * 6))..]);
            }

            return destination.Length;
        }
    }
}
