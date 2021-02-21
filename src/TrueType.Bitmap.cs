using System;
using System.Numerics;

namespace StbSharp
{
    public partial class TrueType
    {
        public static byte[]? GetGlyphBitmap(
            FontInfo info, Vector2 scale, int glyph,
            out int width, out int height, out IntPoint offset)
        {
            return GetGlyphBitmapSubpixel(
                info, scale, Vector2.Zero, glyph, out width, out height, out offset);
        }

        public static byte[]? GetGlyphBitmapSubpixel(
            FontInfo info, Vector2 scale, Vector2 shift, int glyph,
            out int width, out int height, out IntPoint offset)
        {
            if (scale.X == 0)
                scale.X = scale.Y;
            if (scale.Y == 0)
            {
                if (scale.X == 0)
                {
                    width = 0;
                    height = 0;
                    offset = IntPoint.Zero;
                    return null;
                }
                scale.Y = scale.X;
            }

            if (!GetGlyphBitmapBoxSubpixel(info, glyph, scale, shift, out var glyphBox))
            {
                width = default;
                height = default;
                offset = default;
                return null;
            }

            width = glyphBox.W;
            height = glyphBox.H;
            offset = glyphBox.Position;

            if (width != 0 && height != 0)
            {
                int pixelBytes = width * height;
                var pixels = new byte[pixelBytes];

                var gbm = new Bitmap(pixels, width, height, width);
                gbm.Pixels.Fill(0);

                int num_verts = GetGlyphShape(info, glyph, out Vertex[]? vertices);
                Rasterize(
                    gbm, 0.35f, vertices.AsSpan(0, num_verts), scale, shift,
                    glyphBox.Position, IntPoint.Zero, invert: true);

                return pixels;
            }
            return null;
        }

        public static IntRect GetGlyphBitmapBoxSubpixel(Rect subGlyphBox, Vector2 scale, Vector2 shift)
        {
            var br = subGlyphBox.BottomRight;
            return IntRect.FromEdgePoints(
                tlX: (int)Math.Floor(subGlyphBox.X * scale.X + shift.X),
                tlY: (int)Math.Floor(-br.Y * scale.Y + shift.Y),
                brX: (int)Math.Ceiling(br.X * scale.X + shift.X),
                brY: (int)Math.Ceiling(-subGlyphBox.Y * scale.Y + shift.Y));
        }

        public static bool GetGlyphBitmapBoxSubpixel(
            FontInfo font, int glyph, Vector2 scale, Vector2 shift, out IntRect glyphBox)
        {
            if (GetGlyphBox(font, glyph, out var fBox))
            {
                glyphBox = GetGlyphBitmapBoxSubpixel(fBox, scale, shift);
                return true;
            }
            glyphBox = IntRect.Zero;
            return false;
        }

        public static bool GetGlyphBitmapBox(
            FontInfo font, int glyph, Vector2 scale, out IntRect glyphBox)
        {
            return GetGlyphBitmapBoxSubpixel(font, glyph, scale, Vector2.Zero, out glyphBox);
        }

        public static bool GetCodepointBitmapBoxSubpixel(
            FontInfo font, int codepoint, Vector2 scale, Vector2 shift, out IntRect glyphBox)
        {
            int glyph = FindGlyphIndex(font, codepoint);
            return GetGlyphBitmapBoxSubpixel(font, glyph, scale, shift, out glyphBox);
        }

        public static bool GetCodepointBitmapBox(
            FontInfo font, int codepoint, Vector2 scale, out IntRect glyphBox)
        {
            return GetCodepointBitmapBoxSubpixel(
                font, codepoint, scale, Vector2.Zero, out glyphBox);
        }

        public static void MakeGlyphBitmap(
            FontInfo info, Bitmap destination, float pixelFlatness,
            Vector2 scale, Vector2 shift, IntPoint pixelOffset, int glyph)
        {
            if (GetGlyphBitmapBoxSubpixel(info, glyph, scale, shift, out IntRect glyphBox) &&
                destination.Width != 0 && destination.Height != 0)
            {
                int num_verts = GetGlyphShape(info, glyph, out Vertex[]? vertices);

                Rasterize(
                    destination, pixelFlatness, vertices.AsSpan(0, num_verts), scale, shift,
                    glyphBox.Position, pixelOffset, true);
            }
        }

        public static void MakeCodepointBitmapPrefilter(
            FontInfo info, Bitmap destination, float pixelFlatness,
            Vector2 scale, Vector2 shift, IntPoint pixelOffset,
            IntPoint oversample, out Vector2 sub, int codepoint)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            MakeGlyphBitmapPrefilter(
                info, destination, pixelFlatness,
                scale, shift, pixelOffset,
                oversample, out sub, glyph);
        }

        public static void MakeCodepointBitmap(
            FontInfo info, Bitmap destination, float pixelFlatness,
            Vector2 scale, Vector2 shift, IntPoint pixelOffset, int codepoint)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            MakeGlyphBitmap(info, destination, pixelFlatness, scale, shift, pixelOffset, glyph);
        }

        public static int BakeFontBitmap(
            ReadOnlyMemory<byte> fontData, float pixelFlatness, Vector2 scale, 
            Span<byte> pixels, int pw, int ph,
            int firstChar, Span<BakedChar> chardata)
        {
            var info = new FontInfo();
            if (!InitFont(info, fontData))
                return -1;

            int x = 1;
            int y = 1;
            int bottom_y = 1;

            for (int i = 0; i < chardata.Length; ++i)
            {
                int glyph = FindGlyphIndex(info, firstChar + i);
                GetGlyphHMetrics(info, glyph, out int advance, out _);
                GetGlyphBitmapBox(info, glyph, scale, out var glyphBox);
                if ((x + glyphBox.W + 1) >= pw)
                {
                    y = bottom_y;
                    x = 1;
                }

                if ((y + glyphBox.H + 1) >= ph)
                    return -i;

                var pixelsSlice = pixels[(x + y * pw)..];
                var glyphBmp = new Bitmap(pixelsSlice, glyphBox.W, glyphBox.H, pw);
                MakeGlyphBitmap(info, glyphBmp, pixelFlatness, scale, Vector2.Zero, IntPoint.Zero, glyph);

                chardata[i].x0 = (ushort)x;
                chardata[i].y0 = (ushort)y;
                chardata[i].x1 = (ushort)(x + glyphBox.W);
                chardata[i].y1 = (ushort)(y + glyphBox.H);
                chardata[i].xadvance = scale.X * advance;
                chardata[i].off.X = glyphBox.X;
                chardata[i].off.Y = glyphBox.Y;
                x = x + glyphBox.W + 1;
                if ((y + glyphBox.H + 1) > bottom_y)
                    bottom_y = y + glyphBox.H + 1;
            }

            return bottom_y;
        }

        public static void MakeGlyphBitmapPrefilter(
            FontInfo info, Bitmap destination, float pixelFlatness,
            Vector2 scale, Vector2 shift, IntPoint pixelOffset,
            IntPoint prefilter, out Vector2 sub, int glyph)
        {
            int bw = destination.Width - (prefilter.X - 1);
            int bh = destination.Height - (prefilter.Y - 1);
            var glyphBitmap = new Bitmap(destination.Pixels, bw, bh, destination.ByteStride);
            MakeGlyphBitmap(info, glyphBitmap, pixelFlatness, scale, shift, pixelOffset, glyph);

            if (prefilter.X > 1)
                HorizontalPrefilter(destination, prefilter.X);
            if (prefilter.Y > 1)
                VerticalPrefilter(destination, prefilter.Y);

            sub.X = OversampleShift(prefilter.X);
            sub.Y = OversampleShift(prefilter.Y);
        }
    }
}
