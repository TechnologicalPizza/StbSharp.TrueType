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
            int round_x = (int)Math.Floor(xpos + chardata.off.X + 0.5f);
            int round_y = (int)Math.Floor(ypos + chardata.off.Y + 0.5f);
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
        public static void HorizontalPrefilter(
            Span<byte> pixels, int w, int h, int byteStride, int kernelWidth)
        {
            Span<byte> buffer = stackalloc byte[8];
            int safeWidth = w - kernelWidth;

            for (int j = 0; j < h; ++j)
            {
                int i = 0;
                uint total = 0;
                buffer.Slice(0, kernelWidth).Fill(0);

                switch (kernelWidth)
                {
                    case 2:
                        for (i = 0; i <= safeWidth; ++i)
                        {
                            total += (uint)(pixels[i] - buffer[i & (8 - 1)]);
                            buffer[(i + kernelWidth) & (8 - 1)] = pixels[i];
                            pixels[i] = (byte)(total / 2);
                        }
                        break;

                    case 3:
                        for (i = 0; i <= safeWidth; ++i)
                        {
                            total += (uint)(pixels[i] - buffer[i & (8 - 1)]);
                            buffer[(i + kernelWidth) & (8 - 1)] = pixels[i];
                            pixels[i] = (byte)(total / 3);
                        }
                        break;

                    case 4:
                        for (i = 0; i <= safeWidth; ++i)
                        {
                            total += (uint)(pixels[i] - buffer[i & (8 - 1)]);
                            buffer[(i + kernelWidth) & (8 - 1)] = pixels[i];
                            pixels[i] = (byte)(total / 4);
                        }
                        break;

                    case 5:
                        for (i = 0; i <= safeWidth; ++i)
                        {
                            total += (uint)(pixels[i] - buffer[i & (8 - 1)]);
                            buffer[(i + kernelWidth) & (8 - 1)] = pixels[i];
                            pixels[i] = (byte)(total / 5);
                        }
                        break;

                    default:
                        for (i = 0; i <= safeWidth; ++i)
                        {
                            total += (uint)(pixels[i] - buffer[i & (8 - 1)]);
                            buffer[(i + kernelWidth) & (8 - 1)] = pixels[i];
                            pixels[i] = (byte)(total / kernelWidth);
                        }
                        break;
                }

                for (; i < w; ++i)
                {
                    total -= buffer[i & (8 - 1)];
                    pixels[i] = (byte)(total / kernelWidth);
                }

                pixels = pixels[byteStride..];
            }
        }

        [SkipLocalsInit]
        public static void VerticalPrefilter(
            Span<byte> pixels, int w, int h, int byteStride, int kernelWidth)
        {
            Span<byte> buffer = stackalloc byte[8];
            int safeWidth = h - kernelWidth;

            for (int j = 0; j < w; ++j)
            {
                int x = 0;
                uint total = 0;
                buffer.Slice(0, kernelWidth).Fill(0);

                switch (kernelWidth)
                {
                    case 2:
                        for (x = 0; x <= safeWidth; ++x)
                        {
                            total += (uint)(pixels[x * byteStride] - buffer[x & (8 - 1)]);
                            buffer[(x + kernelWidth) & (8 - 1)] = pixels[x * byteStride];
                            pixels[x * byteStride] = (byte)(total / 2);
                        }
                        break;

                    case 3:
                        for (x = 0; x <= safeWidth; ++x)
                        {
                            total += (uint)(pixels[x * byteStride] - buffer[x & (8 - 1)]);
                            buffer[(x + kernelWidth) & (8 - 1)] = pixels[x * byteStride];
                            pixels[x * byteStride] = (byte)(total / 3);
                        }
                        break;

                    case 4:
                        for (x = 0; x <= safeWidth; ++x)
                        {
                            total += (uint)(pixels[x * byteStride] - buffer[x & (8 - 1)]);
                            buffer[(x + kernelWidth) & (8 - 1)] = pixels[x * byteStride];
                            pixels[x * byteStride] = (byte)(total / 4);
                        }
                        break;

                    case 5:
                        for (x = 0; x <= safeWidth; ++x)
                        {
                            total += (uint)(pixels[x * byteStride] - buffer[x & (8 - 1)]);
                            buffer[(x + kernelWidth) & (8 - 1)] = pixels[x * byteStride];
                            pixels[x * byteStride] = (byte)(total / 5);
                        }
                        break;

                    default:
                        for (x = 0; x <= safeWidth; ++x)
                        {
                            total += (uint)(pixels[x * byteStride] - buffer[x & (8 - 1)]);
                            buffer[(x + kernelWidth) & (8 - 1)] = pixels[x * byteStride];
                            pixels[x * byteStride] = (byte)(total / kernelWidth);
                        }
                        break;
                }

                for (; x < h; ++x)
                {
                    total -= buffer[x & (8 - 1)];
                    pixels[x * byteStride] = (byte)(total / kernelWidth);
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