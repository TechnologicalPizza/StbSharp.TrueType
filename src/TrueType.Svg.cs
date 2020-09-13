using System;

namespace StbSharp
{
    public partial class TrueType
    {
        public static int GetSvgIndex(FontInfo info)
        {
            if (info.svg < 0)
            {
                var data = info.data.Span;
                int t = (int)(FindTable(data, info.fontindex, "SVG ").GetValueOrDefault());
                if (t != 0)
                {
                    int offset = (int)ReadUInt32(data.Slice(t + 2));
                    info.svg = t + offset;
                }
                else
                {
                    info.svg = 0;
                }
            }
            return info.svg;
        }

        private static ReadOnlyMemory<byte> FindSVGDoc(FontInfo info, int glyph)
        {
            var svg_doc_list = info.data.Slice(GetSvgIndex(info));
            int numEntries = ReadUInt16(svg_doc_list.Span);
            var svg_docs = svg_doc_list.Slice(2);

            for (int i = 0; i < numEntries; i++)
            {
                var svg_doc = svg_docs.Slice(12 * i);
                var svg_doc_data = svg_doc.Span;
                if ((glyph >= ReadUInt16(svg_doc_data)) &&
                    (glyph <= ReadUInt16(svg_doc_data.Slice(2))))
                    return svg_doc;
            }
            return default;
        }

        private static ReadOnlyMemory<byte> GetGlyphSVG(FontInfo info, int glyph)
        {
            if (info.svg != 0)
            {
                var svg_doc = FindSVGDoc(info, glyph);
                if (!svg_doc.IsEmpty)
                {
                    var svg_doc_data = svg_doc.Span;
                    int start = info.svg + (int)ReadUInt32(svg_doc_data.Slice(4));
                    int length = (int)ReadUInt32(svg_doc_data.Slice(8));
                    return info.data.Slice(start, length);
                }
            }
            return default;
        }

        private static ReadOnlyMemory<byte> GetCodepointSVG(FontInfo info, int unicode_codepoint)
        {
            int index = FindGlyphIndex(info, unicode_codepoint);
            return GetGlyphSVG(info, index);
        }
    }
}
