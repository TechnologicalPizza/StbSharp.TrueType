namespace StbSharp
{
#if !STBSHARP_INTERNAL
	public
#else
	internal
# endif
	struct GlyphInfo
	{
        public int X;
        public int Y;
        public int Width;
        public int Height;
               
        public int XOffset;
        public int YOffset;
		public int XAdvance;

        public GlyphInfo(int x, int y, int width, int height, int xOffset, int yOffset, int xAdvance)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            XOffset = xOffset;
            YOffset = yOffset;
            XAdvance = xAdvance;
        }
    }
}