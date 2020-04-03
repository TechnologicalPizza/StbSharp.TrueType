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
        public static int FindGlyphIndex(FontInfo info, int unicode_codepoint)
        {
            var data = info.data.Span;
            int index_map = info.index_map;
            ushort format = ReadUInt16(data.Slice(index_map + 0));
            if (format == 0)
            {
                int bytes = ReadUInt16(data.Slice(index_map + 2));
                if (unicode_codepoint < (bytes - 6))
                    return data.Slice(index_map + 6 + unicode_codepoint)[0];
                return 0;
            }
            else if (format == 6)
            {
                int first = ReadUInt16(data.Slice(index_map + 6));
                int count = ReadUInt16(data.Slice(index_map + 8));
                if (unicode_codepoint >= first && unicode_codepoint < (first + count))
                    return ReadUInt16(data.Slice(index_map + 10 + (unicode_codepoint - first) * 2));
                return 0;
            }
            else if (format == 2)
            {
                return 0;
            }
            else if (format == 4)
            {
                ushort segcount = (ushort)(ReadUInt16(data.Slice(index_map + 6)) >> 1);
                ushort searchRange = (ushort)(ReadUInt16(data.Slice(index_map + 8)) >> 1);
                ushort entrySelector = ReadUInt16(data.Slice(index_map + 10));
                ushort rangeShift = (ushort)(ReadUInt16(data.Slice(index_map + 12)) >> 1);
                int endCount = index_map + 14;
                int search = endCount;
                if (unicode_codepoint > 0xffff)
                    return 0;
                if (unicode_codepoint >= ReadUInt16(data.Slice(search + rangeShift * 2)))
                    search += rangeShift * 2;
                search -= 2;

                while (entrySelector != 0)
                {
                    searchRange >>= 1;
                    ushort end = ReadUInt16(data.Slice(search + searchRange * 2));
                    if (unicode_codepoint > end)
                        search += searchRange * 2;
                    --entrySelector;
                }
                search += 2;
                ushort item = (ushort)((search - endCount) >> 1);
                ushort start = ReadUInt16(data.Slice(index_map + 14 + segcount * 2 + 2 + 2 * item));
                if (unicode_codepoint < start)
                    return 0;

                ushort offset = ReadUInt16(data.Slice(index_map + 14 + segcount * 6 + 2 + 2 * item));
                if (offset == 0)
                    return (ushort)(unicode_codepoint + ReadInt16(
                        data.Slice(index_map + 14 + segcount * 4 + 2 + 2 * item)));

                return ReadUInt16(data.Slice(
                    offset + (unicode_codepoint - start) * 2 + index_map + 14 + segcount * 6 + 2 + 2 * item));
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

                    if (((uint)unicode_codepoint) < start_char)
                        high = mid;
                    else if (((uint)unicode_codepoint) > end_char)
                        low = mid + 1;
                    else
                    {
                        uint start_glyph = ReadUInt32(data.Slice(index_map + 16 + mid * 12 + 8));
                        if (format == 12)
                            return (int)(start_glyph + unicode_codepoint - start_char);
                        else
                            return (int)start_glyph;
                    }
                }

                return 0;
            }

            return 0;
        }

        public static uint FindTable(ReadOnlySpan<byte> data, int fontstart, string tag)
        {
            int num_tables = ReadUInt16(data.Slice(fontstart + 4));
            int tabledir = fontstart + 12;
            for (int i = 0; i < num_tables; ++i)
            {
                int loc = tabledir + 16 * i;
                var slice = data.Slice(loc);
                if (slice[0] == tag[0] &&
                    slice[1] == tag[1] &&
                    slice[2] == tag[2] &&
                    slice[3] == tag[3])
                    return ReadUInt32(data.Slice(loc + 8));
            }

            return 0;
        }

        public static int FindMatchingFont(
            ReadOnlySpan<byte> fontData, ReadOnlySpan<byte> name_utf8, int flags)
        {
            int i;
            for (i = 0; ; ++i)
            {
                int off = GetFontOffset(fontData, i);
                if (off < 0)
                    return off;

                if (Match(fontData, off, name_utf8, flags))
                    return off;
            }
        }

        public static int MatchPair(
            ReadOnlySpan<byte> fontData, int nm,
            ReadOnlySpan<byte> name, int nlen, int target_id, int next_id)
        {
            int count = ReadUInt16(fontData.Slice(nm + 2));
            int stringOffset = nm + ReadUInt16(fontData.Slice(nm + 4));
            int i;
            for (i = 0; i < count; ++i)
            {
                int loc = nm + 6 + 12 * i;
                int id = ReadUInt16(fontData.Slice(loc + 6));
                if (id == target_id)
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
                        int matchlen = CompareUTF8toUTF16_bigendian_prefix(
                            name.Slice(0, nlen), fontData.Slice(stringOffset + off, slen));

                        if (matchlen >= 0)
                        {
                            if ((i + 1) < count && 
                                ReadUInt16(fontData.Slice(loc + 12 + 6)) == next_id &&
                                ReadUInt16(fontData.Slice(+loc + 12)) == platform &&
                                ReadUInt16(fontData.Slice(+loc + 12 + 2)) == encoding &&
                                ReadUInt16(fontData.Slice(+loc + 12 + 4)) == language)
                            {
                                slen = ReadUInt16(fontData.Slice(+loc + 12 + 8));
                                off = ReadUInt16(fontData.Slice(+loc + 12 + 10));
                                if (slen == 0)
                                {
                                    if (matchlen == nlen)
                                        return 1;
                                }
                                else if ((matchlen < nlen) && (name[matchlen] == ' '))
                                {
                                    ++matchlen;
                                    if (CompareUTF8toUTF16_bigendian(
                                        name.Slice(matchlen, nlen - matchlen),
                                        fontData.Slice(stringOffset + off, slen)) != 0)
                                        return 1;
                                }
                            }
                            else
                            {
                                if (matchlen == nlen)
                                    return 1;
                            }
                        }
                    }
                }
            }

            return 0;
        }

        public static ReadOnlySpan<byte> GetFontName(
            FontInfo font, out int length,
            int platformID, int encodingID, int languageID, int nameID)
        {
            var fc = font.data.Span;
            int offset = font.fontindex;
            int nm = (int)FindTable(fc, offset, "name");
            if (nm == 0)
            {
                length = 0;
                return null;
            }

            int count = ReadUInt16(fc.Slice(nm + 2));
            int stringOffset = nm + ReadUInt16(fc.Slice(nm + 4));
            for (int i = 0; i < count; ++i)
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
                int hd = (int)FindTable(fontData, offset, "head");
                if ((ReadUInt16(fontData.Slice(hd + 44)) & 7) != (flags & 7))
                    return false;
            }

            int nm = (int)FindTable(fontData, offset, "name");
            if (nm == 0)
                return false;

            int nlen = CRuntime.StringLength(name);
            if (flags != 0)
            {
                if (MatchPair(fontData, nm, name, nlen, 16, -1) != 0 ||
                    MatchPair(fontData, nm, name, nlen, 1, -1) != 0 ||
                    MatchPair(fontData, nm, name, nlen, 3, -1) != 0)
                    return true;
            }
            else
            {
                if (MatchPair(fontData, nm, name, nlen, 16, 17) != 0 ||
                    MatchPair(fontData, nm, name, nlen, 1, 2) != 0 || 
                    MatchPair(fontData, nm, name, nlen, 3, -1) != 0)
                    return true;
            }

            return false;
        }

        public static int CompareUTF8toUTF16_bigendian_prefix(
            ReadOnlySpan<byte> s1, ReadOnlySpan<byte> s2)
        {
            int len2 = s2.Length;
            int i = 0;
            while (len2 != 0)
            {
                ushort ch = (ushort)(s2[0] * 256 + s2[1]);
                if (ch < 0x80)
                {
                    if (i >= s1.Length)
                        return -1;
                    if (s1[i++] != ch)
                        return -1;
                }
                else if (ch < 0x800)
                {
                    if ((i + 1) >= s1.Length)
                        return -1;
                    if (s1[i++] != 0xc0 + (ch >> 6))
                        return -1;
                    if (s1[i++] != 0x80 + (ch & 0x3f))
                        return -1;
                }
                else if ((ch >= 0xd800) && (ch < 0xdc00))
                {
                    ushort ch2 = (ushort)(s2[2] * 256 + s2[3]);
                    if ((i + 3) >= s1.Length)
                        return -1;
                    uint c = (uint)(((ch - 0xd800) << 10) + (ch2 - 0xdc00) + 0x10000);
                    if (s1[i++] != 0xf0 + (c >> 18))
                        return -1;
                    if (s1[i++] != 0x80 + ((c >> 12) & 0x3f))
                        return -1;
                    if (s1[i++] != 0x80 + ((c >> 6) & 0x3f))
                        return -1;
                    if (s1[i++] != 0x80 + ((c) & 0x3f))
                        return -1;
                    s2 = s2.Slice(2);
                    len2 -= 2;
                }
                else if ((ch >= 0xdc00) && (ch < 0xe000))
                {
                    return -1;
                }
                else
                {
                    if ((i + 2) >= s1.Length)
                        return -1;
                    if (s1[i++] != 0xe0 + (ch >> 12))
                        return -1;
                    if (s1[i++] != 0x80 + ((ch >> 6) & 0x3f))
                        return -1;
                    if (s1[i++] != 0x80 + ((ch) & 0x3f))
                        return -1;
                }

                s2 = s2.Slice(2);
                len2 -= 2;
            }

            return i;
        }

        public static int CompareUTF8toUTF16_bigendian(
            ReadOnlySpan<byte> s1, ReadOnlySpan<byte> s2)
        {
            return s1.Length == CompareUTF8toUTF16_bigendian_prefix(s1, s2) ? 1 : 0;
        }
    }
}
