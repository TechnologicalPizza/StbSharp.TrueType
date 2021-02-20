using System;

namespace StbSharp
{
    public partial class TrueType
    {
        public static int? GetSvgIndex(FontInfo info)
        {
            if (info.svg != null)
                return info.svg;

            ReadOnlySpan<byte> data = info.data.Span;
            int? t = FindTable(data, "SVG ");
            if (t != null)
            {
                int tv = t.GetValueOrDefault();
                int offset = (int)ReadUInt32(data[(tv + 2)..]);
                info.svg = tv + offset;
            }
            else
            {
                info.svg = 0;
            }
            return info.svg;
        }

        private static ReadOnlyMemory<byte> FindSVGDoc(FontInfo info, int glyph)
        {
            int? index = GetSvgIndex(info);
            if (index == null)
                return default;

            ReadOnlyMemory<byte> svg_doc_list = info.data[index.GetValueOrDefault()..];
            int numEntries = ReadUInt16(svg_doc_list.Span);
            ReadOnlyMemory<byte> svg_docs = svg_doc_list[2..];

            for (int i = 0; i < numEntries; i++)
            {
                ReadOnlyMemory<byte> svg_doc = svg_docs[(12 * i)..];
                ReadOnlySpan<byte> svg_doc_data = svg_doc.Span;

                if ((glyph >= ReadUInt16(svg_doc_data)) &&
                    (glyph <= ReadUInt16(svg_doc_data[2..])))
                    return svg_doc;
            }
            return default;
        }

        private static ReadOnlyMemory<byte> GetGlyphSVG(FontInfo info, int glyph)
        {
            if (info.svg == null)
                return default;

            ReadOnlyMemory<byte> svg_doc = FindSVGDoc(info, glyph);
            if (svg_doc.IsEmpty)
                return default;

            ReadOnlySpan<byte> svg_doc_data = svg_doc.Span;
            int start = (int)(info.svg + ReadUInt32(svg_doc_data[4..]));
            int length = (int)ReadUInt32(svg_doc_data[8..]);
            return info.data.Slice(start, length);
        }

        private static ReadOnlyMemory<byte> GetCodepointSVG(FontInfo info, int unicode_codepoint)
        {
            int index = FindGlyphIndex(info, unicode_codepoint);
            return GetGlyphSVG(info, index);
        }
    }
}
