using System;

namespace StbSharp
{
    public partial class TrueType
    {
        public static ActiveEdge NewActive(in Edge e, float off_x, float start_point)
        {
            // TODO: pool
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

        public static void HandleClippedEdge(
            Span<float> scanline, int x,
            float eey, float esy, float edirection,
            float x0, float y0, float x1, float y1)
        {
            if (y0 == y1 || y0 > eey || y1 < esy)
                return;

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

            HandleClippedEdge(scanline, x, edirection, x0, y0, x1, y1);
        }

        public static void HandleClippedEdge(
            Span<float> scanline, int x,
            float edirection,
            float x0, float y0, float x1, float y1)
        {
            if ((x0 <= x) && (x1 <= x))
            {
                scanline[x] += edirection * (y1 - y0);
            }
            else
            {
                if ((x0 >= (x + 1)) && (x1 >= (x + 1)))
                    return;

                scanline[x] += edirection * (y1 - y0) * (1 - (x0 - x + (x1 - x)) / 2);
            }
        }

        public static void FillActiveEdgesNew(
            Span<float> scanline, Span<float> scanlineFill, ActiveEdge? active, float y_top)
        {
            float y_bottom = y_top + 1;

            ActiveEdge? e = active;
            while (e != null)
            {
                if (e.fd.X == 0)
                {
                    float x0 = e.fx;
                    if (x0 < scanline.Length)
                    {
                        float eey = e.ey;
                        float esy = e.sy;
                        float edir = e.direction;

                        if (x0 >= 0)
                        {
                            HandleClippedEdge(scanline, (int)x0, eey, esy, edir, x0, y_top, x0, y_bottom);
                            HandleClippedEdge(scanlineFill, (int)(x0 + 1), eey, esy, edir, x0, y_top, x0, y_bottom);
                        }
                        else
                        {
                            HandleClippedEdge(scanlineFill, 0, eey, esy, edir, x0, y_top, x0, y_bottom);
                        }
                    }
                }
                else
                {
                    float x0 = e.fx;
                    float dx = e.fd.X;
                    float dy = e.fd.Y;
                    float xb = x0 + dx;
                    float x_top;
                    float x_bottom;
                    float sy0;
                    float sy1;

                    if (e.sy > y_top)
                    {
                        x_top = x0 + dx * (e.sy - y_top);
                        sy0 = e.sy;
                    }
                    else
                    {
                        x_top = x0;
                        sy0 = y_top;
                    }

                    if (e.ey < y_bottom)
                    {
                        x_bottom = x0 + dx * (e.ey - y_top);
                        sy1 = e.ey;
                    }
                    else
                    {
                        x_bottom = xb;
                        sy1 = y_bottom;
                    }

                    if ((x_top >= 0) &&
                        (x_bottom >= 0) &&
                        (x_top < scanline.Length) &&
                        (x_bottom < scanline.Length))
                    {
                        if (((int)x_top) == ((int)x_bottom))
                        {
                            int x = (int)x_top;
                            float height = sy1 - sy0;
                            scanline[x] += e.direction * (1 - (x_top - x + (x_bottom - x)) / 2) * height;
                            scanlineFill[x + 1] += e.direction * height;
                        }
                        else
                        {
                            if (x_top > x_bottom)
                            {
                                sy0 = y_bottom - (sy0 - y_top);
                                sy1 = y_bottom - (sy1 - y_top);
                                float t = sy0;
                                sy0 = sy1;
                                sy1 = t;
                                t = x_bottom;
                                x_bottom = x_top;
                                x_top = t;
                                dx = -dx;
                                dy = -dy;
                                t = x0;
                                x0 = xb;
                                xb = t;
                            }

                            int x1 = (int)x_top;
                            int x2 = (int)x_bottom;
                            float y_crossing = (x1 + 1 - x0) * dy + y_top;
                            float sign = e.direction;
                            float area = sign * (y_crossing - sy0);
                            scanline[x1] += area * (1 - (x_top - x1 + (x1 + 1 - x1)) / 2);

                            float step = sign * dy;
                            for (int x = x1 + 1; x < x2; ++x)
                            {
                                scanline[x] += area + step / 2;
                                area += step;
                            }

                            y_crossing += dy * (x2 - (x1 + 1));
                            scanline[x2] += area + sign * (1 - (x2 - x2 + (x_bottom - x2)) / 2) * (sy1 - y_crossing);
                            scanlineFill[x2 + 1] += sign * (sy1 - sy0);
                        }
                    }
                    else
                    {
                        float y0 = y_top;
                        float x3 = xb;
                        float y3 = y_bottom;
                        float dx_fac = 1f / dx;

                        float eey = e.ey;
                        float esy = e.sy;
                        float edir = e.direction;

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

                        for (int x = 0; x < scanline.Length; x++)
                        {
                            float x1 = x;
                            float x2 = x + 1;

                            if (x0 < x1 && x3 > x2)
                            {
                                float y1 = (x1 - x0) * dx_fac + y_top;
                                float y2 = (x2 - x0) * dx_fac + y_top;
                                HandleClippedEdge(scanline, x, eey, esy, edir, x0, y0, x1, y1);
                                HandleClippedEdge(scanline, x, eey, esy, edir, x1, y1, x2, y2);
                                HandleClippedEdge(scanline, x, eey, esy, edir, x2, y2, x3, y3);
                            }
                            else if (x3 < x1 && x0 > x2)
                            {
                                float y1 = (x1 - x0) * dx_fac + y_top;
                                float y2 = (x2 - x0) * dx_fac + y_top;
                                HandleClippedEdge(scanline, x, eey, esy, edir, x0, y0, x2, y2);
                                HandleClippedEdge(scanline, x, eey, esy, edir, x2, y2, x1, y1);
                                HandleClippedEdge(scanline, x, eey, esy, edir, x1, y1, x3, y3);
                            }
                            else if (x0 < x1 && x3 > x1)
                            {
                                float y1 = (x1 - x0) * dx_fac + y_top;
                                HandleClippedEdge(scanline, x, eey, esy, edir, x0, y0, x1, y1);
                                HandleClippedEdge(scanline, x, eey, esy, edir, x1, y1, x3, y3);
                            }
                            else if (x3 < x1 && x0 > x1)
                            {
                                float y1 = (x1 - x0) * dx_fac + y_top;
                                HandleClippedEdge(scanline, x, eey, esy, edir, x0, y0, x1, y1);
                                HandleClippedEdge(scanline, x, eey, esy, edir, x1, y1, x3, y3);
                            }
                            else if (x0 < x2 && x3 > x2)
                            {
                                float y2 = (x2 - x0) * dx_fac + y_top;
                                HandleClippedEdge(scanline, x, eey, esy, edir, x0, y0, x2, y2);
                                HandleClippedEdge(scanline, x, eey, esy, edir, x2, y2, x3, y3);
                            }
                            else if (x3 < x2 && x0 > x2)
                            {
                                float y2 = (x2 - x0) * dx_fac + y_top;
                                HandleClippedEdge(scanline, x, eey, esy, edir, x0, y0, x2, y2);
                                HandleClippedEdge(scanline, x, eey, esy, edir, x2, y2, x3, y3);
                            }
                            else if (executeLast)
                            {
                                HandleClippedEdge(scanline, x, edir, lastX0, lastY0, lastX1, lastY1);
                            }
                        }
                    }
                }

                e = e.next;
            }
        }

        public static void SortEdgesInsertSort(Span<Edge> p, int n)
        {
            for (int i = 1; i < n; ++i)
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

        public static void SortEdgesQuickSort(Span<Edge> p, int n)
        {
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
                    SortEdgesQuickSort(p, j);
                    p = p.Slice(i);
                    n -= i;
                }
                else
                {
                    SortEdgesQuickSort(p.Slice(i), n - i);
                    n = j;
                }
            }
        }

        public static void SortEdges(Span<Edge> p, int n)
        {
            SortEdgesQuickSort(p, n);
            SortEdgesInsertSort(p, n);
        }
    }
}
