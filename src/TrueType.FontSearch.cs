using System;
using System.Collections;
using System.Collections.Generic;

namespace StbSharp
{
    public partial class TrueType
    {
        public static int FindGlyphIndex(FontInfo info, int unicodeCodepoint)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            if (info.index_map == null)
                return default;

            ReadOnlySpan<byte> data = info.data.Span;
            int index_map = info.index_map.GetValueOrDefault();

            ushort format = ReadUInt16(data[(index_map + 0)..]);
            if (format == 0)
            {
                int bytes = ReadUInt16(data[(index_map + 2)..]);
                if (unicodeCodepoint < (bytes - 6))
                    return data[(index_map + 6 + unicodeCodepoint)..][0];
                return default;
            }
            else if (format == 6)
            {
                int first = ReadUInt16(data[(index_map + 6)..]);
                int count = ReadUInt16(data[(index_map + 8)..]);
                if (unicodeCodepoint >= first && unicodeCodepoint < (first + count))
                    return ReadUInt16(data[(index_map + 10 + (unicodeCodepoint - first) * 2)..]);
                return default;
            }
            else if (format == 2)
            {
                return default;
            }
            else if (format == 4)
            {
                ushort segcount = (ushort)(ReadUInt16(data[(index_map + 6)..]) >> 1);
                ushort searchRange = (ushort)(ReadUInt16(data[(index_map + 8)..]) >> 1);
                ushort entrySelector = ReadUInt16(data[(index_map + 10)..]);
                ushort rangeShift = (ushort)(ReadUInt16(data[(index_map + 12)..]) >> 1);
                int endCount = index_map + 14;
                int search = endCount;
                if (unicodeCodepoint > 0xffff)
                    return default;
                if (unicodeCodepoint >= ReadUInt16(data[(search + rangeShift * 2)..]))
                    search += rangeShift * 2;
                search -= 2;

                while (entrySelector != 0)
                {
                    searchRange >>= 1;
                    ushort end = ReadUInt16(data[(search + searchRange * 2)..]);
                    if (unicodeCodepoint > end)
                        search += searchRange * 2;
                    --entrySelector;
                }
                search += 2;
                ushort item = (ushort)((search - endCount) >> 1);
                ushort start = ReadUInt16(data[(index_map + 14 + segcount * 2 + 2 + 2 * item)..]);
                if (unicodeCodepoint < start)
                    return default;

                ushort offset = ReadUInt16(data[(index_map + 14 + segcount * 6 + 2 + 2 * item)..]);
                if (offset == 0)
                {
                    return (ushort)(unicodeCodepoint + ReadInt16(
                        data[(index_map + 14 + segcount * 4 + 2 + 2 * item)..]));
                }
                return ReadUInt16(data[
                    (offset + (unicodeCodepoint - start) * 2 + index_map + 14 + segcount * 6 + 2 + 2 * item)..]);
            }
            else if ((format == 12) || (format == 13))
            {
                uint ngroups = ReadUInt32(data[(index_map + 12)..]);
                int low = 0;
                int high = (int)ngroups;
                while (low < high)
                {
                    int mid = low + ((high - low) >> 1);
                    uint start_char = ReadUInt32(data[(index_map + 16 + mid * 12)..]);
                    uint end_char = ReadUInt32(data[(index_map + 16 + mid * 12 + 4)..]);

                    if (((uint)unicodeCodepoint) < start_char)
                        high = mid;
                    else if (((uint)unicodeCodepoint) > end_char)
                        low = mid + 1;
                    else
                    {
                        uint start_glyph = ReadUInt32(data[(index_map + 16 + mid * 12 + 8)..]);
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

        public static int? FindTable(ReadOnlySpan<byte> data, ReadOnlySpan<char> tag)
        {
            if (tag.Length != 4)
                throw new ArgumentException("", nameof(tag));
            if (data.Length < 6)
                return null;

            int num_tables = ReadUInt16(data[4..]);
            int tabledir = 12;
            for (int i = 0; i < num_tables; i++)
            {
                int loc = tabledir + 16 * i;
                ReadOnlySpan<byte> slice = data.Slice(loc, 4);
                if (slice[0] == tag[0] &&
                    slice[1] == tag[1] &&
                    slice[2] == tag[2] &&
                    slice[3] == tag[3])
                {
                    return (int)ReadUInt32(data[(loc + 8)..]);
                }
            }

            return default;
        }

        public static int FindMatchingFont(
            ReadOnlyMemory<byte> fontData, ReadOnlySpan<byte> nameUtf8, int flags)
        {
            for (int i = 0; ; i++)
            {
                int off = GetFontOffset(fontData.Span[i..]);
                if (off < 0)
                    return off;

                if (Match(fontData[off..], nameUtf8, flags))
                    return off;
            }
        }

        public static bool MatchPair<TEnumerator>(
            TEnumerator nameEnumerator, ReadOnlySpan<byte> name, ReadOnlySpan<int> nameIds)
            where TEnumerator : IEnumerator<FontName>
        {
            while (nameEnumerator.MoveNext())
            {
                FontName fontName = nameEnumerator.Current;
                if (!nameIds.Contains(fontName.NameID))
                    continue;

                if (fontName.PlatformID == FontPlatformID.Unicode ||
                    (fontName.PlatformID == FontPlatformID.Microsoft && fontName.EncodingID == 1) ||
                    (fontName.PlatformID == FontPlatformID.Microsoft && fontName.EncodingID == 10))
                {
                    int matchLength = BigEndianComparePrefixUtf8To16(name, fontName.Name.Span);
                    if (matchLength == name.Length)
                        return true;
                }
            }
            return false;
        }

        public struct FontName
        {
            public FontPlatformID PlatformID { get; }
            public int EncodingID { get; }
            public int LanguageID { get; }
            public int NameID { get; }
            public ReadOnlyMemory<byte> Name { get; }

            public FontName(FontPlatformID platformID, int encodingID, int languageID, int nameID, ReadOnlyMemory<byte> name)
            {
                PlatformID = platformID;
                EncodingID = encodingID;
                LanguageID = languageID;
                NameID = nameID;
                Name = name;
            }
        }

        public struct FontNameEnumerator : IEnumerator<FontName>
        {
            private int _index;
            private int _tableOffset;

            public ReadOnlyMemory<byte> FontData { get; }
            public int Count { get; }
            public int StringOffset { get; }

            public FontPlatformID PlatformID { get; private set; }
            public int EncodingID { get; private set; }
            public int LanguageID { get; private set; }
            public int NameID { get; private set; }
            public Range Range { get; private set; }

            public FontName Current => new FontName(PlatformID, EncodingID, LanguageID, NameID, FontData[Range]);
            object IEnumerator.Current => Current;

            public FontNameEnumerator(ReadOnlyMemory<byte> fontData) : this()
            {
                FontData = fontData;

                ReadOnlySpan<byte> data = FontData.Span;
                int? nameTable = FindTable(data, "name");
                if (nameTable == null)
                    return;

                _tableOffset = nameTable.GetValueOrDefault();
                Count = ReadUInt16(data[(_tableOffset + 2)..]);
                StringOffset = _tableOffset + ReadUInt16(data[(_tableOffset + 4)..]);
            }

            public bool MoveNext()
            {
                if (_index < Count)
                {
                    ReadOnlySpan<byte> data = FontData.Span;
                    int loc = _tableOffset + 6 + 12 * _index;
                    PlatformID = (FontPlatformID)ReadUInt16(data[(loc + 0)..]);
                    EncodingID = ReadUInt16(data[(loc + 2)..]);
                    LanguageID = ReadUInt16(data[(loc + 4)..]);
                    NameID = ReadUInt16(data[(loc + 6)..]);

                    int length = ReadUInt16(data[(loc + 8)..]);
                    int start = StringOffset + ReadUInt16(data[(loc + 10)..]);
                    Range = new Range(start, start + length);

                    _index++;
                    return true;
                }
                return false;
            }

            public FontNameEnumerator GetEnumerator()
            {
                return this;
            }

            public void Reset()
            {
                _index = 0;
            }

            public void Dispose()
            {
            }
        }

        public static FontNameEnumerator EnumerateFontNames(FontInfo font)
        {
            if (font == null)
                throw new ArgumentNullException(nameof(font));

            return new FontNameEnumerator(font.data);
        }

        public static ReadOnlySpan<byte> GetFontName(
            FontInfo font, out int length,
            int platformID, int encodingID, int languageID, int nameID)
        {
            if (font == null)
                throw new ArgumentNullException(nameof(font));

            var fc = font.data.Span;
            int? nameTable = FindTable(fc, "name");
            if (!nameTable.HasValue)
            {
                length = 0;
                return default;
            }
            int nm = nameTable.GetValueOrDefault();

            int count = ReadUInt16(fc[(nm + 2)..]);
            int stringOffset = nm + ReadUInt16(fc[(nm + 4)..]);
            for (int i = 0; i < count; i++)
            {
                int loc = nm + 6 + 12 * i;
                if (platformID == ReadUInt16(fc[(loc + 0)..]) &&
                    encodingID == ReadUInt16(fc[(loc + 2)..]) &&
                    languageID == ReadUInt16(fc[(loc + 4)..]) &&
                    nameID == ReadUInt16(fc[(loc + 6)..]))
                {
                    length = ReadUInt16(fc[(loc + 8)..]);
                    return fc[(stringOffset + ReadUInt16(fc[(loc + 10)..]))..];
                }
            }

            length = 0;
            return null;
        }

        public static bool Match(
            ReadOnlyMemory<byte> fontData, ReadOnlySpan<byte> name, int flags)
        {
            ReadOnlySpan<byte> data = fontData.Span;

            if (!IsFont(data))
                return false;

            if (flags != 0)
            {
                int? hd = FindTable(data, "head");
                if (hd == null)
                    return false;

                if ((ReadUInt16(data[(hd.GetValueOrDefault() + 44)..]) & 7) != (flags & 7))
                    return false;
            }

            var names = new FontNameEnumerator(fontData);
            if (flags != 0)
            {
                return MatchPair(names, name, stackalloc int[] { 1, 3, 16 });
            }
            else
            {
                return MatchPair(names, name, stackalloc int[] { 1, 2, 3, 16, 17 });
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

                    utf16 = utf16[2..];
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

                utf16 = utf16[2..];
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
