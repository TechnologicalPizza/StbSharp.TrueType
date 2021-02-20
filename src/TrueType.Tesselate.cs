using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace StbSharp
{
    public partial class TrueType
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddPoint(ref Vector2 points, int offset, float x, float y)
        {
            if (Unsafe.IsNullRef(ref points))
                return;

            ref Vector2 dst = ref Unsafe.Add(ref points, offset);
            dst.X = x;
            dst.Y = y;
        }

        public static void TesselateCurve(
            ref Vector2 points, ref int num_points, float x0, float y0, float x1,
            float y1, float x2, float y2, float objspace_flatness_squared, int n)
        {
            if (n > 16)
                return;

            float mx = (x0 + x2 + 2 * x1) * 0.25f;
            float my = (y0 + y2 + 2 * y1) * 0.25f;
            float dx = (x0 + x2) * 0.5f - mx;
            float dy = (y0 + y2) * 0.5f - my;

            if ((dx * dx + dy * dy) > objspace_flatness_squared)
            {
                TesselateCurve(
                    ref points, ref num_points,
                    x0, y0,
                    (x0 + x1) * 0.5f, 
                    (y0 + y1) * 0.5f,
                    mx, my,
                    objspace_flatness_squared, n + 1);

                TesselateCurve(
                    ref points, ref num_points,
                    mx, my,
                    (x1 + x2) * 0.5f,
                    (y1 + y2) * 0.5f,
                    x2, y2,
                    objspace_flatness_squared, n + 1);
            }
            else
            {
                AddPoint(ref points, num_points, x2, y2);
                num_points++;
            }
        }

        public static void TesselateCubic(
            ref Vector2 points, ref int num_points,
            float x0, float y0,
            float x1, float y1,
            float x2, float y2,
            float x3, float y3,
            float objspace_flatness_squared, int n)
        {
            if (n > 16)
                return;

            float dx0 = x1 - x0;
            float dy0 = y1 - y0;
            float dx1 = x2 - x1;
            float dy1 = y2 - y1;
            float dx2 = x3 - x2;
            float dy2 = y3 - y2;
            float dx = x3 - x0;
            float dy = y3 - y0;

            float longlen =
                MathF.Sqrt(dx0 * dx0 + dy0 * dy0) +
                MathF.Sqrt(dx1 * dx1 + dy1 * dy1) +
                MathF.Sqrt(dx2 * dx2 + dy2 * dy2);

            float shortlen = MathF.Sqrt(dx * dx + dy * dy);
            float flatness_squared = longlen * longlen - shortlen * shortlen;

            if (flatness_squared > objspace_flatness_squared)
            {
                float x01 = (x0 + x1) * 0.5f;
                float y01 = (y0 + y1) * 0.5f;
                float x12 = (x1 + x2) * 0.5f;
                float y12 = (y1 + y2) * 0.5f;
                float x23 = (x2 + x3) * 0.5f;
                float y23 = (y2 + y3) * 0.5f;
                float xa = (x01 + x12) * 0.5f;
                float ya = (y01 + y12) * 0.5f;
                float xb = (x12 + x23) * 0.5f;
                float yb = (y12 + y23) * 0.5f;
                float mx = (xa + xb) * 0.5f;
                float my = (ya + yb) * 0.5f;

                TesselateCubic(
                    ref points, ref num_points, x0, y0, x01, y01, xa, ya, mx, my,
                    objspace_flatness_squared, n + 1);

                TesselateCubic(
                    ref points, ref num_points, mx, my, xb, yb, x23, y23, x3, y3,
                    objspace_flatness_squared, n + 1);
            }
            else
            {
                AddPoint(ref points, num_points, x3, y3);
                num_points++;
            }
        }
    }
}
