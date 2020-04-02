using StbTrueTypeSharp;
using System;
using System.Collections.Generic;

namespace StbSharp.MonoGame.Test
{
	public unsafe class FontBaker
	{
		private byte[] _bitmap;
		private StbTrueType.stbtt_pack_context _context;
		private Dictionary<int, GlyphInfo> _glyphs;
		private int bitmapWidth, bitmapHeight;

        public void Start(int width, int height, bool skipMissing = true)
        {
            bitmapWidth = width;
            bitmapHeight = height;
            _bitmap = new byte[width * height];

            StbTrueType.PackPrepare(_context, skipMissing, width, height, width, 1);
            _glyphs.Clear();
        }

        public void Add(
            ReadOnlyMemory<byte> ttf, float fontPixelHeight, ReadOnlySpan<CharacterRange> ranges)
        {
            if (ttf.IsEmpty)
                throw new ArgumentException(nameof(ttf));

            if (fontPixelHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(fontPixelHeight));

            if (ranges.IsEmpty)
                throw new ArgumentException();

            var fontInfo = new StbTrueType.TTFontInfo();
            if (!StbTrueType.InitFont(fontInfo, ttf, 0))
                throw new Exception("Failed to init font.");

            var scale = StbTrueType.ScaleForPixelHeight(fontInfo, fontPixelHeight);
            StbTrueType.GetFontVMetrics(fontInfo, out int ascent, out _, out _);

            foreach (var range in ranges)
            {
                if (range.Start > range.End)
                    continue;

                var charData = new StbTrueType.TTPackedChar[range.Size];
                
                StbTrueType.PackFontRange(
                    _context, _bitmap, ttf, fontPixelHeight, range.Start, charData);

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