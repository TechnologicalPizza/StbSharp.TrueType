using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace StbSharp
{
    public partial class TrueType
    {
        public static ActiveEdge NewActive(in Edge e, float off_x, float start_point)
        {
            // TODO: pool?
            var z = new ActiveEdge();

            float dxdy = (e.p1.X - e.p0.X) / (e.p1.Y - e.p0.Y);
            z.fd.X = dxdy;
            z.fd.Y = dxdy != 0f ? (1f / dxdy) : 0f;
            z.fx = e.p0.X + dxdy * (start_point - e.p0.Y);
            z.fx -= off_x;
            z.direction = e.invert ? 1f : -1f;
            z.sy = e.p0.Y;
            z.ey = e.p1.Y;
            return z;
        }

        public static float HandleClippedEdge(
            float x, float edir,
            float eey, float esy,
            float x0, float y0, float x1, float y1)
        {
            if (y0 == y1 || y0 > eey || y1 < esy)
                return 0;

            if (y0 < esy)
            {
                x0 += (x1 - x0) * (esy - y0) / (y1 - y0);
                y0 = esy;
            }

            if (y1 > eey)
            {
                x1 += (x1 - x0) * (eey - y1) / (y1 - y0);
                y1 = eey;
            }

            return HandleClippedEdge(x, edir, x0, y0, x1, y1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float HandleClippedEdge(
            float x, float edir,
            float x0, float y0, float x1, float y1)
        {
            if ((x0 <= x) && (x1 <= x))
            {
                return edir * (y1 - y0);
            }
            else
            {
                float fx1 = x + 1;
                if ((x0 >= fx1) && (x1 >= fx1))
                    return 0;

                return edir * (y1 - y0) * MathF.FusedMultiplyAdd(x0 - (x1 - x) + x, -0.5f, 1);
            }
        }

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void FillActiveEdges(
            ref float scanline, ref float scanlineFill, int scanlineLength,
            ActiveEdge? active, float yTop)
        {
            float yBottom = yTop + 1;

            ActiveEdge? e = active;
            while (e != null)
            {
                float dx = e.fd.X;
                float dy = e.fd.Y;
                float x0 = e.fx;
                float eey = e.ey;
                float esy = e.sy;
                float edir = e.direction;

                if (dx == 0)
                {
                    if (x0 < scanlineLength)
                    {
                        if (x0 >= 0)
                        {
                            int x = (int)x0;
                            ref float sc = ref Unsafe.Add(ref scanline, x);
                            ref float scf = ref Unsafe.Add(ref scanlineFill, x + 1);
                            sc += HandleClippedEdge(x0, edir, eey, esy, x0, yTop, x0, yBottom);
                            scf += HandleClippedEdge(x0 + 1, edir, eey, esy, x0, yTop, x0, yBottom);
                        }
                        else
                        {
                            scanlineFill += edir * HandleClippedEdge(0, edir, eey, esy, x0, yTop, x0, yBottom);
                        }
                    }
                }
                else
                {
                    float xb = x0 + dx;
                    float xTop;
                    float xBottom;
                    float sy0;
                    float sy1;

                    if (esy > yTop)
                    {
                        xTop = x0 + dx * (esy - yTop);
                        sy0 = esy;
                    }
                    else
                    {
                        xTop = x0;
                        sy0 = yTop;
                    }

                    if (eey < yBottom)
                    {
                        xBottom = x0 + dx * (eey - yTop);
                        sy1 = eey;
                    }
                    else
                    {
                        xBottom = xb;
                        sy1 = yBottom;
                    }

                    if ((xTop >= 0) &&
                        (xBottom >= 0) &&
                        (xTop < scanlineLength) &&
                        (xBottom < scanlineLength))
                    {
                        int x1 = (int)xTop;
                        int x2 = (int)xBottom;
                        if (x1 == x2)
                        {
                            float height = sy1 - sy0;
                            Unsafe.Add(ref scanline, x1) += edir * (1 - (xTop - x1 + (xBottom - x1)) * 0.5f) * height;
                            Unsafe.Add(ref scanlineFill, x1 + 1) += edir * height;
                        }
                        else
                        {
                            if (xTop > xBottom)
                            {
                                sy0 = yBottom - (sy0 - yTop);
                                sy1 = yBottom - (sy1 - yTop);
                                float t = sy0;
                                sy0 = sy1;
                                sy1 = t;
                                t = xBottom;
                                xBottom = xTop;
                                xTop = t;
                                dx = -dx;
                                dy = -dy;
                                t = x0;
                                x0 = xb;
                                xb = t;
                                x1 = (int)xTop;
                                x2 = (int)xBottom;
                            }

                            float y_crossing = (x1 + 1 - x0) * dy + yTop;
                            float sign = edir;
                            float area = sign * (y_crossing - sy0);
                            Unsafe.Add(ref scanline, x1) += area * (1 - (xTop - x1 + (x1 + 1 - x1)) * 0.5f);

                            float step = sign * dy;
                            for (int x = x1 + 1; x < x2; ++x)
                            {
                                Unsafe.Add(ref scanline, x) += area + step * 0.5f;
                                area += step;
                            }

                            y_crossing += dy * (x2 - (x1 + 1));
                            Unsafe.Add(ref scanline, x2) += area + sign * (1 - (x2 - x2 + (xBottom - x2)) * 0.5f) * (sy1 - y_crossing);
                            Unsafe.Add(ref scanlineFill, x2 + 1) += sign * (sy1 - sy0);
                        }
                    }
                    else
                    {
                        float y0 = yTop;
                        float x3 = xb;
                        float y3 = yBottom;
                        float dx_fac = 1f / dx;

                        float lastX0 = x0;
                        float lastY0 = y0;
                        float lastX1 = x3;
                        float lastY1 = y3;
                        bool executeLast = !(lastY0 == lastY1 || lastY0 > eey || lastY1 < esy);

                        if (lastY0 < esy)
                        {
                            lastX0 += (lastX1 - lastX0) * (esy - lastY0) / (lastY1 - lastY0);
                            lastY0 = esy;
                        }

                        if (lastY1 > eey)
                        {
                            lastX1 += (lastX1 - lastX0) * (eey - lastY1) / (lastY1 - lastY0);
                            lastY1 = eey;
                        }

                        // Having duplicated code allows for early elimination of branches,
                        // greatly increasing performance (especially for large fills).
                        if (executeLast)
                        {
                            for (int x = 0; x < scanlineLength; x++)
                            {
                                ref float sc = ref Unsafe.Add(ref scanline, x);
                                float x1 = x;
                                float x2 = x1 + 1;

                                if (x0 < x1 && x3 > x2)
                                {
                                    float y1 = (x1 - x0) * dx_fac + yTop;
                                    float y2 = (x2 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x1, y1);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x1, y1, x2, y2);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x2, y2, x3, y3);
                                }
                                else if (x3 < x1 && x0 > x2)
                                {
                                    float y1 = (x1 - x0) * dx_fac + yTop;
                                    float y2 = (x2 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x2, y2);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x2, y2, x1, y1);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x1, y1, x3, y3);
                                }
                                else if (x0 < x1 && x3 > x1)
                                {
                                    float y1 = (x1 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x1, y1);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x1, y1, x3, y3);
                                }
                                else if (x3 < x1 && x0 > x1)
                                {
                                    float y1 = (x1 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x1, y1);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x1, y1, x3, y3);
                                }
                                else if (x0 < x2 && x3 > x2)
                                {
                                    float y2 = (x2 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x2, y2);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x2, y2, x3, y3);
                                }
                                else if (x3 < x2 && x0 > x2)
                                {
                                    float y2 = (x2 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x2, y2);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x2, y2, x3, y3);
                                }
                                else
                                {
                                    sc += HandleClippedEdge(x1, edir, lastX0, lastY0, lastX1, lastY1);
                                }
                            }
                        }
                        else
                        {
                            for (int x = 0; x < scanlineLength; x++)
                            {
                                ref float sc = ref Unsafe.Add(ref scanline, x);
                                float x1 = x;
                                float x2 = x1 + 1;

                                if (x0 < x1 && x3 > x2)
                                {
                                    float y1 = (x1 - x0) * dx_fac + yTop;
                                    float y2 = (x2 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x1, y1);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x1, y1, x2, y2);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x2, y2, x3, y3);
                                }
                                else if (x3 < x1 && x0 > x2)
                                {
                                    float y1 = (x1 - x0) * dx_fac + yTop;
                                    float y2 = (x2 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x2, y2);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x2, y2, x1, y1);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x1, y1, x3, y3);
                                }
                                else if (x0 < x1 && x3 > x1)
                                {
                                    float y1 = (x1 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x1, y1);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x1, y1, x3, y3);
                                }
                                else if (x3 < x1 && x0 > x1)
                                {
                                    float y1 = (x1 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x1, y1);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x1, y1, x3, y3);
                                }
                                else if (x0 < x2 && x3 > x2)
                                {
                                    float y2 = (x2 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x2, y2);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x2, y2, x3, y3);
                                }
                                else if (x3 < x2 && x0 > x2)
                                {
                                    float y2 = (x2 - x0) * dx_fac + yTop;
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x0, y0, x2, y2);
                                    sc += HandleClippedEdge(x1, edir, eey, esy, x2, y2, x3, y3);
                                }
                            }
                        }
                    }
                }

                e = e.next;
            }
        }

        public static void SortEdgesInsertSort(Span<Edge> p)
        {
            for (int i = 1; i < p.Length; ++i)
            {
                Edge a = p[i]; // don't take by-ref

                int j = i;
                while (j > 0)
                {
                    ref Edge b = ref p[j - 1];
                    if (!(a.p0.Y < b.p0.Y))
                        break;

                    p[j] = b;
                    --j;
                }

                if (i != j)
                    p[j] = a;
            }
        }

        public static void SortEdgesQuickSort(Span<Edge> p)
        {
            int n = p.Length;
            while (n > 12)
            {
                Edge t;
                int m = n >> 1;
                int c01 = p[0].p0.Y < p[m].p0.Y ? 1 : 0;
                int c12 = p[m].p0.Y < p[n - 1].p0.Y ? 1 : 0;
                if (c01 != c12)
                {
                    int c = p[0].p0.Y < p[n - 1].p0.Y ? 1 : 0;
                    int z = (c == c12) ? 0 : n - 1;
                    t = p[z];
                    p[z] = p[m];
                    p[m] = t;
                }

                t = p[0];
                p[0] = p[m];
                p[m] = t;
                int i = 1;
                int j = n - 1;
                for (; ; )
                {
                    for (; ; ++i)
                    {
                        if (!(p[i].p0.Y < p[0].p0.Y))
                            break;
                    }

                    for (; ; --j)
                    {
                        if (!(p[0].p0.Y < p[j].p0.Y))
                            break;
                    }

                    if (i >= j)
                        break;
                    t = p[i];
                    p[i] = p[j];
                    p[j] = t;
                    ++i;
                    --j;
                }

                if (j < (n - i))
                {
                    SortEdgesQuickSort(p.Slice(0, j));
                    p = p[i..];
                    n -= i;
                }
                else
                {
                    SortEdgesQuickSort(p[i..n]);
                    n = j;
                }
            }
        }

        public static void SortEdges(Span<Edge> p)
        {
            SortEdgesQuickSort(p);
            SortEdgesInsertSort(p);
        }
    }
}
