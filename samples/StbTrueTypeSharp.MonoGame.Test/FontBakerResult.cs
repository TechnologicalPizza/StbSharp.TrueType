using System;
using System.Collections.Generic;

namespace StbSharp.MonoGame.Test
{
	public class FontBakerResult
	{
		public FontBakerResult(Dictionary<int, GlyphInfo> glyphs, byte[] bitmap, int width, int height)
		{
			Glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
			Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));

            if (width <= 0) 
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                new ArgumentOutOfRangeException(nameof(height));

            if (bitmap.Length < width * height)
                throw new ArgumentException("pixels.Length should be higher than width * height");

            Width = width;
			Height = height;
		}

		public Dictionary<int, GlyphInfo> Glyphs { get; }
		public byte[] Bitmap { get; }

		public int Width { get; }
		public int Height { get; }
	}
}