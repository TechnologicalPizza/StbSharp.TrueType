using System;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class StbTrueType
    {
        public const int STBTT_vmove = 1;
        public const int STBTT_vline = 2;
        public const int STBTT_vcurve = 3;
        public const int STBTT_vcubic = 4;

        public const int STBTT_PLATFORM_ID_UNICODE = 0;
        public const int STBTT_PLATFORM_ID_MAC = 1;
        public const int STBTT_PLATFORM_ID_ISO = 2;
        public const int STBTT_PLATFORM_ID_MICROSOFT = 3;

        public const int STBTT_UNICODE_EID_UNICODE_1_0 = 0;
        public const int STBTT_UNICODE_EID_UNICODE_1_1 = 1;
        public const int STBTT_UNICODE_EID_ISO_10646 = 2;
        public const int STBTT_UNICODE_EID_UNICODE_2_0_BMP = 3;
        public const int STBTT_UNICODE_EID_UNICODE_2_0_FULL = 4;

        public const int STBTT_MS_EID_SYMBOL = 0;
        public const int STBTT_MS_EID_UNICODE_BMP = 1;
        public const int STBTT_MS_EID_SHIFTJIS = 2;
        public const int STBTT_MS_EID_UNICODE_FULL = 10;

        public const int STBTT_MAC_EID_ROMAN = 0;
        public const int STBTT_MAC_EID_ARABIC = 4;
        public const int STBTT_MAC_EID_JAPANESE = 1;
        public const int STBTT_MAC_EID_HEBREW = 5;
        public const int STBTT_MAC_EID_CHINESE_TRAD = 2;
        public const int STBTT_MAC_EID_GREEK = 6;
        public const int STBTT_MAC_EID_KOREAN = 3;
        public const int STBTT_MAC_EID_RUSSIAN = 7;

        public const int STBTT_MS_LANG_ENGLISH = 0x0409;
        public const int STBTT_MS_LANG_ITALIAN = 0x0410;
        public const int STBTT_MS_LANG_CHINESE = 0x0804;
        public const int STBTT_MS_LANG_JAPANESE = 0x0411;
        public const int STBTT_MS_LANG_DUTCH = 0x0413;
        public const int STBTT_MS_LANG_KOREAN = 0x0412;
        public const int STBTT_MS_LANG_FRENCH = 0x040c;
        public const int STBTT_MS_LANG_RUSSIAN = 0x0419;
        public const int STBTT_MS_LANG_GERMAN = 0x0407;
        public const int STBTT_MS_LANG_SPANISH = 0x0409;
        public const int STBTT_MS_LANG_HEBREW = 0x040d;
        public const int STBTT_MS_LANG_SWEDISH = 0x041D;

        public const int STBTT_MAC_LANG_ENGLISH = 0;
        public const int STBTT_MAC_LANG_JAPANESE = 11;
        public const int STBTT_MAC_LANG_ARABIC = 12;
        public const int STBTT_MAC_LANG_KOREAN = 23;
        public const int STBTT_MAC_LANG_DUTCH = 4;
        public const int STBTT_MAC_LANG_RUSSIAN = 32;
        public const int STBTT_MAC_LANG_FRENCH = 1;
        public const int STBTT_MAC_LANG_SPANISH = 6;
        public const int STBTT_MAC_LANG_GERMAN = 2;
        public const int STBTT_MAC_LANG_SWEDISH = 5;
        public const int STBTT_MAC_LANG_HEBREW = 10;
        public const int STBTT_MAC_LANG_CHINESE_SIMPLIFIED = 33;
        public const int STBTT_MAC_LANG_ITALIAN = 3;
        public const int STBTT_MAC_LANG_CHINESE_TRAD = 19;

        public static void GetBakedQuad(
            in TTBakedChar chardata, int pw, int ph,
            ref float xpos, float ypos, bool opengl_fillrule, out TTAlignedQuad q)
        {
            float d3d_bias = opengl_fillrule ? 0 : -0.5f;
            float ipw = 1f / pw;
            float iph = 1f / ph;
            int round_x = (int)Math.Floor(xpos + chardata.xoff + 0.5f);
            int round_y = (int)Math.Floor(ypos + chardata.yoff + 0.5f);
            q.x0 = round_x + d3d_bias;
            q.y0 = round_y + d3d_bias;
            q.x1 = round_x + chardata.x1 - chardata.x0 + d3d_bias;
            q.y1 = round_y + chardata.y1 - chardata.y0 + d3d_bias;
            q.s0 = chardata.x0 * ipw;
            q.t0 = chardata.y0 * iph;
            q.s1 = chardata.x1 * ipw;
            q.t1 = chardata.y1 * iph;
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