using System;
using System.Runtime.CompilerServices;

namespace StbSharp
{
    public partial class TrueType
    {
        public static void GetBakedQuad(
            in BakedChar chardata, int pw, int ph,
            ref float xpos, float ypos, bool openglFillrule, out AlignedQuad q)
        {
            float d3d_bias = openglFillrule ? 0 : -0.5f;
            float ipw = 1f / pw;
            float iph = 1f / ph;
            int round_x = (int)MathF.Floor(xpos + chardata.off.X + 0.5f);
            int round_y = (int)MathF.Floor(ypos + chardata.off.Y + 0.5f);
            q.pos0.X = round_x + d3d_bias;
            q.pos0.Y = round_y + d3d_bias;
            q.pos1.X = round_x + chardata.x1 - chardata.x0 + d3d_bias;
            q.pos1.Y = round_y + chardata.y1 - chardata.y0 + d3d_bias;
            q.st0.X = chardata.x0 * ipw;
            q.st0.Y = chardata.y0 * iph;
            q.st1.X = chardata.x1 * ipw;
            q.st1.Y = chardata.y1 * iph;
            xpos += chardata.xadvance;
        }

        [SkipLocalsInit]
        public static void HorizontalPrefilter(Bitmap bitmap, int kernelWidth)
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int BatchFilter(
                ref uint total, Span<byte> span, Span<byte> buffer, int kernelWidth)
            {
                int x;
                for (x = 0; x < span.Length; x++)
                {
                    total += (uint)(span[x] - buffer[x & (8 - 1)]);
                    buffer[(x + kernelWidth) & (8 - 1)] = span[x];
                    span[x] = (byte)(total / (uint)kernelWidth);
                }
                return x;
            }

            if (kernelWidth > 8)
                throw new ArgumentOutOfRangeException(nameof(kernelWidth));

            Span<byte> buffer = stackalloc byte[8];
            int safeWidth = bitmap.Width - kernelWidth;

            int stride = bitmap.ByteStride;
            Span<byte> pixels = bitmap.Pixels;

            for (int j = 0; j < bitmap.Height; j++)
            {
                uint total = 0;
                buffer.Slice(0, kernelWidth).Clear();
                Span<byte> span = pixels.Slice(0, safeWidth + 1);

                int x = kernelWidth switch
                {
                    2 => BatchFilter(ref total, span, buffer, 2),
                    4 => BatchFilter(ref total, span, buffer, 4),
                    8 => BatchFilter(ref total, span, buffer, 8),
                    _ => BatchFilter(ref total, span, buffer, kernelWidth),
                };

                for (; x < bitmap.Width; x++)
                {
                    total -= buffer[x & (8 - 1)];
                    pixels[x] = (byte)(total / (uint)kernelWidth);
                }

                pixels = pixels[stride..];
            }
        }

        [SkipLocalsInit]
        public static void VerticalPrefilter(Bitmap bitmap, int kernelWidth)
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int BatchFilter(
                ref uint total, Span<byte> pixels, Span<byte> buffer, int safeHeight, int stride, int kernelWidth)
            {
                int y;
                for (y = 0; y <= safeHeight; y++)
                {
                    ref byte p = ref pixels[y * stride];
                    total += (uint)(p - buffer[y & (8 - 1)]);
                    buffer[(y + kernelWidth) & (8 - 1)] = p;
                    p = (byte)(total / (uint)kernelWidth);
                }
                return y;
            }

            if (kernelWidth > 8)
                throw new ArgumentOutOfRangeException(nameof(kernelWidth));

            Span<byte> buffer = stackalloc byte[8];
            int safeHeight = bitmap.Height - kernelWidth;

            int stride = bitmap.ByteStride;
            Span<byte> pixels = bitmap.Pixels;

            for (int j = 0; j < bitmap.Width; j++)
            {
                uint total = 0;
                buffer.Slice(0, kernelWidth).Clear();

                int x = kernelWidth switch
                {
                    2 => BatchFilter(ref total, pixels, buffer, safeHeight, stride, 2),
                    4 => BatchFilter(ref total, pixels, buffer, safeHeight, stride, 4),
                    8 => BatchFilter(ref total, pixels, buffer, safeHeight, stride, 8),
                    _ => BatchFilter(ref total, pixels, buffer, safeHeight, stride, kernelWidth),
                };

                for (; x < bitmap.Height; x++)
                {
                    total -= buffer[x & (8 - 1)];
                    pixels[x * stride] = (byte)(total / (uint)kernelWidth);
                }

                pixels = pixels[1..];
            }
        }

        public static float OversampleShift(int oversample)
        {
            if (oversample == 0)
                return 0f;
            return (-(oversample - 1)) / (2f * oversample);
        }
    }
}