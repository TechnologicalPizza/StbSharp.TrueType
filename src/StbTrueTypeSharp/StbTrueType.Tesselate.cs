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
        public static void AddPoint(TTPoint* points, int n, float x, float y)
        {
            if (points == null)
                return;

            points[n].x = x;
            points[n].y = y;
        }

        public static int TesselateCurve(
            TTPoint* points, int* num_points, float x0, float y0, float x1,
            float y1, float x2, float y2, float objspace_flatness_squared, int n)
        {
            float mx = (x0 + 2 * x1 + x2) / 4;
            float my = (y0 + 2 * y1 + y2) / 4;
            float dx = (x0 + x2) / 2 - mx;
            float dy = (y0 + y2) / 2 - my;
            if (n > 16)
                return 1;

            if ((dx * dx + dy * dy) > objspace_flatness_squared)
            {
                TesselateCurve(points, num_points, x0, y0, (x0 + x1) / 2f,
                    (y0 + y1) / 2f, mx, my, objspace_flatness_squared,
                    n + 1);

                TesselateCurve(points, num_points, mx, my, (x1 + x2) / 2f,
                    (y1 + y2) / 2f, x2, y2, objspace_flatness_squared,
                    n + 1);
            }
            else
            {
                AddPoint(points, *num_points, x2, y2);
                *num_points = *num_points + 1;
            }

            return 1;
        }

        public static void TesselateCubic(
            TTPoint* points, int* num_points, float x0, float y0, float x1,
            float y1, float x2, float y2, float x3, float y3, float objspace_flatness_squared, int n)
        {
            float dx0 = x1 - x0;
            float dy0 = y1 - y0;
            float dx1 = x2 - x1;
            float dy1 = y2 - y1;
            float dx2 = x3 - x2;
            float dy2 = y3 - y2;
            float dx = x3 - x0;
            float dy = y3 - y0;
            float longlen = (float)(
                Math.Sqrt(dx0 * dx0 + dy0 * dy0) +
                Math.Sqrt(dx1 * dx1 + dy1 * dy1) +
                Math.Sqrt(dx2 * dx2 + dy2 * dy2));

            float shortlen = (float)Math.Sqrt(dx * dx + dy * dy);
            float flatness_squared = longlen * longlen - shortlen * shortlen;
            if (n > 16)
                return;

            if (flatness_squared > objspace_flatness_squared)
            {
                float x01 = (x0 + x1) / 2;
                float y01 = (y0 + y1) / 2;
                float x12 = (x1 + x2) / 2;
                float y12 = (y1 + y2) / 2;
                float x23 = (x2 + x3) / 2;
                float y23 = (y2 + y3) / 2;
                float xa = (x01 + x12) / 2;
                float ya = (y01 + y12) / 2;
                float xb = (x12 + x23) / 2;
                float yb = (y12 + y23) / 2;
                float mx = (xa + xb) / 2;
                float my = (ya + yb) / 2;

                TesselateCubic(points, num_points, x0, y0, x01, y01,
                    xa, ya, mx, my, objspace_flatness_squared,
                    n + 1);

                TesselateCubic(points, num_points, mx, my, xb, yb,
                    x23, y23, x3, y3, objspace_flatness_squared,
                    n + 1);
            }
            else
            {
                AddPoint(points, *num_points, x3, y3);
                *num_points = *num_points + 1;
            }
        }
    }
}
