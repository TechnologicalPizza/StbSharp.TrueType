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
        public static ushort ReadUInt16(ReadOnlySpan<byte> p) => (ushort)(p[0] * 256 + p[1]);

        public static short ReadInt16(ReadOnlySpan<byte> p) => (short)(p[0] * 256 + p[1]);

        public static uint ReadUInt32(ReadOnlySpan<byte> p) => (uint)((p[0] << 24) + (p[1] << 16) + (p[2] << 8) + p[3]);

        public static int ReadInt32(ReadOnlySpan<byte> p) => (p[0] << 24) + (p[1] << 16) + (p[2] << 8) + p[3];

        public static float CubeRoot(float x)
        {
            if (x < 0)
                return (float)-Math.Pow(-x, 1 / 3.0);
            else
                return (float)Math.Pow(x, 1 / 3.0);
        }

        public static int SolveCubic(float a, float b, float c, float* r)
        {
            float p = b - a * a / 3;
            float p3 = p * p * p;
            float q = a * (2 * a * a - 9 * b) / 27 + c;
            float d = q * q + 4 * p3 / 27;
            float s = -a / 3;
            if (d >= 0)
            {
                float z = (float)Math.Sqrt(d);
                float u = (-q + z) / 2;
                float v = (-q - z) / 2;
                u = CubeRoot(u);
                v = CubeRoot(v);

                r[0] = s + u + v;
                return 1;
            }
            else
            {
                float u = (float)Math.Sqrt(-p / 3);
                float v = (float)Math.Acos(-Math.Sqrt(-27 / p3) * q / 2) / 3;
                float m = (float)Math.Cos(v);
                float n = (float)Math.Cos(v - Math.PI / 2) * 1.732050808f;

                r[0] = s + u * 2 * m;
                r[1] = s - u * (m + n);
                r[2] = s - u * (m - n);
                return 3;
            }
        }
    }
}
