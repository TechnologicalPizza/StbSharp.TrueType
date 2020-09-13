using System;

namespace StbSharp
{
    public partial class TrueType
    {
        public static int FindGlyphIndex(FontInfo info, int unicodeCodepoint)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            var data = info.data.Span;
            int index_map = info.index_map;
            ushort format = ReadUInt16(data.Slice(index_map + 0));
            if (format == 0)
            {
                int bytes = ReadUInt16(data.Slice(index_map + 2));
                if (unicodeCodepoint < (bytes - 6))
                    return data.Slice(index_map + 6 + unicodeCodepoint)[0];
                return default;
            }
            else if (format == 6)
            {
                int first = ReadUInt16(data.Slice(index_map + 6));
                int count = ReadUInt16(data.Slice(index_map + 8));
                if (unicodeCodepoint >= first && unicodeCodepoint < (first + count))
                    return ReadUInt16(data.Slice(index_map + 10 + (unicodeCodepoint - first) * 2));
                return default;
            }
            else if (format == 2)
            {
                return default;
            }
            else if (format == 4)
            {
                ushort segcount = (ushort)(ReadUInt16(data.Slice(index_map + 6)) >> 1);
                ushort searchRange = (ushort)(ReadUInt16(data.Slice(index_map + 8)) >> 1);
                ushort entrySelector = ReadUInt16(data.Slice(index_map + 10));
                ushort rangeShift = (ushort)(ReadUInt16(data.Slice(index_map + 12)) >> 1);
                int endCount = index_map + 14;
                int search = endCount;
                if (unicodeCodepoint > 0xffff)
                    return default;
                if (unicodeCodepoint >= ReadUInt16(data.Slice(search + rangeShift * 2)))
                    search += rangeShift * 2;
                search -= 2;

                while (entrySelector != 0)
                {
                    searchRange >>= 1;
                    ushort end = ReadUInt16(data.Slice(search + searchRange * 2));
                    if (unicodeCodepoint > end)
                        search += searchRange * 2;
                    --entrySelector;
                }
                search += 2;
                ushort item = (ushort)((search - endCount) >> 1);
                ushort start = ReadUInt16(data.Slice(index_map + 14 + segcount * 2 + 2 + 2 * item));
                if (unicodeCodepoint < start)
                    return default;

                ushort offset = ReadUInt16(data.Slice(index_map + 14 + segcount * 6 + 2 + 2 * item));
                if (offset == 0)
                {
                    return (ushort)(unicodeCodepoint + ReadInt16(
                        data.Slice(index_map + 14 + segcount * 4 + 2 + 2 * item)));
                }
                return ReadUInt16(data.Slice(
                    offset + (unicodeCodepoint - start) * 2 + index_map + 14 + segcount * 6 + 2 + 2 * item));
            }
            else if ((format == 12) || (format == 13))
            {
                uint ngroups = ReadUInt32(data.Slice(index_map + 12));
                int low = 0;
                int high = (int)ngroups;
                while (low < high)
                {
                    int mid = low + ((high - low) >> 1);
                    uint start_char = ReadUInt32(data.Slice(index_map + 16 + mid * 12));
                    uint end_char = ReadUInt32(data.Slice(index_map + 16 + mid * 12 + 4));

                    if (((uint)unicodeCodepoint) < start_char)
                        high = mid;
                    else if (((uint)unicodeCodepoint) > end_char)
                        low = mid + 1;
                    else
                    {
                        uint start_glyph = ReadUInt32(data.Slice(index_map + 16 + mid * 12 + 8));
                        if (format == 12)
                            return (int)(start_glyph + unicodeCodepoint - start_char);
                        else
                            return (int)start_glyph;
                    }
                }

                return default;
            }

            return default;
        }

        [CLSCompliant(false)]
        public static uint? FindTable(ReadOnlySpan<byte> data, int fontstart, ReadOnlySpan<char> tag)
        {
            if (tag.Length != 4)
                throw new ArgumentException("", nameof(tag));

            int num_tables = ReadUInt16(data.Slice(fontstart + 4));
            int tabledir = fontstart + 12;
            for (int i = 0; i < num_tables; i++)
            {
                int loc = tabledir + 16 * i;
                var slice = data.Slice(loc);
                if (slice[0] == tag[0] &&
                    slice[1] == tag[1] &&
                    slice[2] == tag[2] &&
                    slice[3] == tag[3])
                    return ReadUInt32(data.Slice(loc + 8));
            }

            return default;
        }

        public static int FindMatchingFont(
            ReadOnlySpan<byte> fontData, ReadOnlySpan<byte> nameUtf8, int flags)
        {
            for (int i = 0; ; i++)
            {
                int off = GetFontOffset(fontData, i);
                if (off < 0)
                    return off;

                if (Match(fontData, off, nameUtf8, flags))
                    return off;
            }
        }

        public static bool MatchPair(
            ReadOnlySpan<byte> fontData, int nameTable,
            ReadOnlySpan<byte> name, int targetId, int nextId)
        {
            int count = ReadUInt16(fontData.Slice(nameTable + 2));
            int stringOffset = nameTable + ReadUInt16(fontData.Slice(nameTable + 4));

            for (int i = 0; i < count; i++)
            {
                int loc = nameTable + 6 + 12 * i;
                int id = ReadUInt16(fontData.Slice(loc + 6));
                if (id == targetId)
                {
                    int platform = ReadUInt16(fontData.Slice(loc + 0));
                    int encoding = ReadUInt16(fontData.Slice(loc + 2));
                    int language = ReadUInt16(fontData.Slice(loc + 4));

                    if (platform == 0 ||
                        (platform == 3 && encoding == 1) ||
                        (platform == 3 && encoding == 10))
                    {
                        int slen = ReadUInt16(fontData.Slice(loc + 8));
                        int off = ReadUInt16(fontData.Slice(loc + 10));
                        int matchLength = BigEndianComparePrefixUtf8To16(
                            name, fontData.Slice(stringOffset + off, slen));

                        if (matchLength >= 0)
                        {
                            if ((i + 1) < count &&
                                ReadUInt16(fontData.Slice(loc + 12 + 6)) == nextId &&
                                ReadUInt16(fontData.Slice(+loc + 12)) == platform &&
                                ReadUInt16(fontData.Slice(+loc + 12 + 2)) == encoding &&
                                ReadUInt16(fontData.Slice(+loc + 12 + 4)) == language)
                            {
                                slen = ReadUInt16(fontData.Slice(+loc + 12 + 8));
                                off = ReadUInt16(fontData.Slice(+loc + 12 + 10));
                                if (slen == 0)
                                {
                                    if (matchLength == name.Length)
                                        return true;
                                }
                                else if ((matchLength < name.Length) && (name[matchLength] == ' '))
                                {
                                    matchLength++;
                                    if (BigEndianCompareUtf8To16(
                                        name[matchLength..name.Length],
                                        fontData.Slice(stringOffset + off, slen)))
                                        return true;
                                }
                            }
                            else
                            {
                                if (matchLength == name.Length)
                                    return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public static ReadOnlySpan<byte> GetFontName(
            FontInfo font, out int length,
            int platformID, int encodingID, int languageID, int nameID)
        {
            if (font == null)
                throw new ArgumentNullException(nameof(font));

            var fc = font.data.Span;
            int offset = font.fontindex;
            uint? nameTable = FindTable(fc, offset, "name");
            if (!nameTable.HasValue)
            {
                length = 0;
                return default;
            }
            int nm = (int)nameTable.GetValueOrDefault();

            int count = ReadUInt16(fc.Slice(nm + 2));
            int stringOffset = nm + ReadUInt16(fc.Slice(nm + 4));
            for (int i = 0; i < count; i++)
            {
                int loc = nm + 6 + 12 * i;
                if (platformID == ReadUInt16(fc.Slice(loc + 0)) &&
                    encodingID == ReadUInt16(fc.Slice(loc + 2)) &&
                    languageID == ReadUInt16(fc.Slice(loc + 4)) &&
                    nameID == ReadUInt16(fc.Slice(loc + 6)))
                {
                    length = ReadUInt16(fc.Slice(loc + 8));
                    return fc.Slice(stringOffset + ReadUInt16(fc.Slice(loc + 10)));
                }
            }

            length = 0;
            return null;
        }

        public static bool Match(
            ReadOnlySpan<byte> fontData, int offset, ReadOnlySpan<byte> name, int flags)
        {
            if (!IsFont(fontData.Slice(offset)))
                return false;

            if (flags != 0)
            {
                uint? hd = FindTable(fontData, offset, "head");
                if (!hd.HasValue)
                    return false;

                if ((ReadUInt16(fontData.Slice((int)hd.GetValueOrDefault() + 44)) & 7) != (flags & 7))
                    return false;
            }

            uint? nameTable = FindTable(fontData, offset, "name");
            if (!nameTable.HasValue)
                return false;
            int nm = (int)nameTable.GetValueOrDefault();

            if (flags != 0)
            {
                return MatchPair(fontData, nm, name, 16, -1)
                    || MatchPair(fontData, nm, name, 1, -1)
                    || MatchPair(fontData, nm, name, 3, -1);
            }
            else
            {
                return MatchPair(fontData, nm, name, 16, 17)
                    || MatchPair(fontData, nm, name, 1, 2) 
                    || MatchPair(fontData, nm, name, 3, -1);
            }
        }

        public static int BigEndianComparePrefixUtf8To16(
            ReadOnlySpan<byte> utf8, ReadOnlySpan<byte> utf16)
        {
            int i = 0;
            while (!utf16.IsEmpty)
            {
                ushort ch = (ushort)(utf16[0] * 256 + utf16[1]);
                if (ch < 0x80)
                {
                    if (i >= utf8.Length)
                        return -1;
                    if (utf8[i++] != ch)
                        return -1;
                }
                else if (ch < 0x800)
                {
                    if ((i + 1) >= utf8.Length)
                        return -1;
                    if (utf8[i++] != 0xc0 + (ch >> 6))
                        return -1;
                    if (utf8[i++] != 0x80 + (ch & 0x3f))
                        return -1;
                }
                else if ((ch >= 0xd800) && (ch < 0xdc00))
                {
                    ushort ch2 = (ushort)(utf16[2] * 256 + utf16[3]);
                    if ((i + 3) >= utf8.Length)
                        return -1;
                    uint c = (uint)(((ch - 0xd800) << 10) + (ch2 - 0xdc00) + 0x10000);
                    if (utf8[i++] != 0xf0 + (c >> 18))
                        return -1;
                    if (utf8[i++] != 0x80 + ((c >> 12) & 0x3f))
                        return -1;
                    if (utf8[i++] != 0x80 + ((c >> 6) & 0x3f))
                        return -1;
                    if (utf8[i++] != 0x80 + ((c) & 0x3f))
                        return -1;

                    utf16 = utf16.Slice(2);
                }
                else if ((ch >= 0xdc00) && (ch < 0xe000))
                {
                    return -1;
                }
                else
                {
                    if ((i + 2) >= utf8.Length)
                        return -1;
                    if (utf8[i++] != 0xe0 + (ch >> 12))
                        return -1;
                    if (utf8[i++] != 0x80 + ((ch >> 6) & 0x3f))
                        return -1;
                    if (utf8[i++] != 0x80 + ((ch) & 0x3f))
                        return -1;
                }

                utf16 = utf16.Slice(2);
            }
            return i;
        }

        public static bool BigEndianCompareUtf8To16(
            ReadOnlySpan<byte> utf8, ReadOnlySpan<byte> utf16)
        {
            return utf8.Length == BigEndianComparePrefixUtf8To16(utf8, utf16);
        }
    }
}
