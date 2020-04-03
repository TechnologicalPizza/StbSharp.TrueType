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
        public static void GetBakedQuad(
            in BakedChar chardata, int pw, int ph,
            ref float xpos, float ypos, bool opengl_fillrule, out AlignedQuad q)
        {
            float d3d_bias = opengl_fillrule ? 0 : -0.5f;
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

        public static void HorizontalPrefilter(
            Span<byte> pixels, int w, int h, int stride_in_bytes, int kernel_width)
        {
            Span<byte> buffer = stackalloc byte[8];
            int safe_w = w - kernel_width;

            for (int j = 0; j < h; ++j)
            {
                int i = 0;
                uint total = 0;
                buffer.Slice(0, kernel_width).Fill(0);

                switch (kernel_width)
                {
                    case 2:
                        for (i = 0; i <= safe_w; ++i)
                        {
                            total += (uint)(pixels[i] - buffer[i & (8 - 1)]);
                            buffer[(i + kernel_width) & (8 - 1)] = pixels[i];
                            pixels[i] = (byte)(total / 2);
                        }
                        break;

                    case 3:
                        for (i = 0; i <= safe_w; ++i)
                        {
                            total += (uint)(pixels[i] - buffer[i & (8 - 1)]);
                            buffer[(i + kernel_width) & (8 - 1)] = pixels[i];
                            pixels[i] = (byte)(total / 3);
                        }
                        break;

                    case 4:
                        for (i = 0; i <= safe_w; ++i)
                        {
                            total += (uint)(pixels[i] - buffer[i & (8 - 1)]);
                            buffer[(i + kernel_width) & (8 - 1)] = pixels[i];
                            pixels[i] = (byte)(total / 4);
                        }
                        break;

                    case 5:
                        for (i = 0; i <= safe_w; ++i)
                        {
                            total += (uint)(pixels[i] - buffer[i & (8 - 1)]);
                            buffer[(i + kernel_width) & (8 - 1)] = pixels[i];
                            pixels[i] = (byte)(total / 5);
                        }
                        break;

                    default:
                        for (i = 0; i <= safe_w; ++i)
                        {
                            total += (uint)(pixels[i] - buffer[i & (8 - 1)]);
                            buffer[(i + kernel_width) & (8 - 1)] = pixels[i];
                            pixels[i] = (byte)(total / kernel_width);
                        }
                        break;
                }

                for (; i < w; ++i)
                {
                    total -= buffer[i & (8 - 1)];
                    pixels[i] = (byte)(total / kernel_width);
                }

                pixels = pixels.Slice(stride_in_bytes);
            }
        }

        public static void VerticalPrefilter(
            Span<byte> pixels, int w, int h, int stride_in_bytes, int kernel_width)
        {
            Span<byte> buffer = stackalloc byte[8];
            int safe_h = h - kernel_width;

            for (int j = 0; j < w; ++j)
            {
                int i = 0;
                uint total = 0;
                buffer.Slice(0, kernel_width).Fill(0);

                switch (kernel_width)
                {
                    case 2:
                        for (i = 0; i <= safe_h; ++i)
                        {
                            total += (uint)(pixels[i * stride_in_bytes] - buffer[i & (8 - 1)]);
                            buffer[(i + kernel_width) & (8 - 1)] = pixels[i * stride_in_bytes];
                            pixels[i * stride_in_bytes] = (byte)(total / 2);
                        }
                        break;

                    case 3:
                        for (i = 0; i <= safe_h; ++i)
                        {
                            total += (uint)(pixels[i * stride_in_bytes] - buffer[i & (8 - 1)]);
                            buffer[(i + kernel_width) & (8 - 1)] = pixels[i * stride_in_bytes];
                            pixels[i * stride_in_bytes] = (byte)(total / 3);
                        }
                        break;

                    case 4:
                        for (i = 0; i <= safe_h; ++i)
                        {
                            total += (uint)(pixels[i * stride_in_bytes] - buffer[i & (8 - 1)]);
                            buffer[(i + kernel_width) & (8 - 1)] = pixels[i * stride_in_bytes];
                            pixels[i * stride_in_bytes] = (byte)(total / 4);
                        }
                        break;

                    case 5:
                        for (i = 0; i <= safe_h; ++i)
                        {
                            total += (uint)(pixels[i * stride_in_bytes] - buffer[i & (8 - 1)]);
                            buffer[(i + kernel_width) & (8 - 1)] = pixels[i * stride_in_bytes];
                            pixels[i * stride_in_bytes] = (byte)(total / 5);
                        }
                        break;

                    default:
                        for (i = 0; i <= safe_h; ++i)
                        {
                            total += (uint)(pixels[i * stride_in_bytes] - buffer[i & (8 - 1)]);
                            buffer[(i + kernel_width) & (8 - 1)] = pixels[i * stride_in_bytes];
                            pixels[i * stride_in_bytes] = (byte)(total / kernel_width);
                        }
                        break;
                }

                for (; i < h; ++i)
                {
                    total -= buffer[i & (8 - 1)];
                    pixels[i * stride_in_bytes] = (byte)(total / kernel_width);
                }

                pixels = pixels.Slice(1);
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