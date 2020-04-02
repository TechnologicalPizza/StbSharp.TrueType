using System;
using System.Collections.Generic;

namespace StbSharp.MonoGame.Test
{
    public unsafe class FontBaker
    {
        private byte[] _bitmap;
        private TrueType.PackContext _context = new TrueType.PackContext();
        private Dictionary<int, GlyphInfo> _glyphs = new Dictionary<int, GlyphInfo>();
        private int bitmapWidth, bitmapHeight;

        public void Start(int width, int height, bool skipMissing = true)
        {
            bitmapWidth = width;
            bitmapHeight = height;
            _bitmap = new byte[width * height];

            TrueType.PackPrepare(_context, skipMissing, width, height, width, 1);
            _glyphs.Clear();
        }

        public void Add(
            ReadOnlyMemory<byte> fontData,
            int fontIndex,
            float pixelHeight,
            ReadOnlySpan<CharacterRange> ranges)
        {
            if (fontData.IsEmpty)
                throw new ArgumentException(nameof(fontData));

            if (pixelHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(pixelHeight));

            if (ranges.IsEmpty)
                throw new ArgumentException();

            var fontInfo = new TrueType.FontInfo();
            if (!TrueType.InitFont(fontInfo, fontData, fontIndex))
                throw new Exception("Failed to init font.");

            var scale = TrueType.ScaleForPixelHeight(fontInfo, pixelHeight);
            TrueType.GetFontVMetrics(fontInfo, out int ascent, out _, out _);

            foreach (var range in ranges)
            {
                if (range.Start > range.End)
                    continue;

                var charData = new TrueType.PackedChar[range.Size];
                
                TrueType.PackFontRange(
                    _context, _bitmap, fontData, pixelHeight, range.Start, charData);

                for (int i = 0; i < charData.Length; ++i)
                {
                    var yOff = charData[i].offset0.y;
                    yOff += ascent * scale.x;

                    var glyphInfo = new GlyphInfo(
                        x: charData[i].x0,
                        y: charData[i].y0,
                        width: charData[i].x1 - charData[i].x0,
                        height: charData[i].y1 - charData[i].y0,
                        xOffset: (int)charData[i].offset0.x,
                        yOffset: (int)Math.Round(yOff),
                        xAdvance: (int)Math.Round(charData[i].xadvance));

                    _glyphs[i + range.Start] = glyphInfo;
                }
            }
        }

        public FontBakerResult GetResult()
        {
            return new FontBakerResult(_glyphs, _bitmap, bitmapWidth, bitmapHeight);
        }
    }
}