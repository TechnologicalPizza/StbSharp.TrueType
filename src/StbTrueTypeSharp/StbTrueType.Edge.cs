
namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
	internal
#endif
    unsafe partial class StbTrueType
    {
        public static TTActiveEdge* NewActive(TTHeap* hh, TTEdge* e, int off_x, float start_point)
        {
            var z = (TTActiveEdge*)HeapAlloc(hh, sizeof(TTActiveEdge));
            float dxdy = (e->p1.x - e->p0.x) / (e->p1.y - e->p0.y);
            if (z == null)
                return z;

            z->fdx = dxdy;
            z->fdy = dxdy != 0f ? (1f / dxdy) : 0f;
            z->fx = e->p0.x + dxdy * (start_point - e->p0.y);
            z->fx -= off_x;
            z->direction = e->invert ? 1f : -1f;
            z->sy = e->p0.y;
            z->ey = e->p1.y;
            z->next = null;
            return z;
        }

        public static void HandleClippedEdge(
            float* scanline, int x, TTActiveEdge* e, float x0, float y0, float x1, float y1)
        {
            if (y0 == y1)
                return;
            if (y0 > e->ey)
                return;
            if (y1 < e->sy)
                return;

            if (y0 < e->sy)
            {
                x0 += (x1 - x0) * (e->sy - y0) / (y1 - y0);
                y0 = e->sy;
            }

            if (y1 > e->ey)
            {
                x1 += (x1 - x0) * (e->ey - y1) / (y1 - y0);
                y1 = e->ey;
            }

            //if (x0 == x)
            //{
            //}
            //else if (x0 == (x + 1))
            //{
            //}
            //else if (x0 <= x)
            //{
            //}
            //else if (x0 >= (x + 1))
            //{
            //}
            //else
            //{
            //}

            if ((x0 <= x) && (x1 <= x))
            {
                scanline[x] += e->direction * (y1 - y0);
            }
            else if ((x0 >= (x + 1)) && (x1 >= (x + 1)))
            {
            }
            else
            {
                scanline[x] += e->direction * (y1 - y0) * (1 - (x0 - x + (x1 - x)) / 2);
            }
        }

        public static void FillActiveEdgesNew(
            float* scanline, float* scanline_fill, int len, TTActiveEdge* e, float y_top)
        {
            float y_bottom = y_top + 1;
            while (e != null)
            {
                if (e->fdx == 0)
                {
                    float x0 = e->fx;
                    if (x0 < len)
                    {
                        if (x0 >= 0)
                        {
                            HandleClippedEdge(scanline, (int)x0, e, x0, y_top, x0, y_bottom);
                            HandleClippedEdge(scanline_fill - 1, (int)(x0 + 1), e, x0, y_top, x0, y_bottom);
                        }
                        else
                        {
                            HandleClippedEdge(scanline_fill - 1, 0, e, x0, y_top, x0, y_bottom);
                        }
                    }
                }
                else
                {
                    float x0 = e->fx;
                    float dx = e->fdx;
                    float xb = x0 + dx;
                    float x_top = 0;
                    float x_bottom = 0;
                    float sy0 = 0;
                    float sy1 = 0;
                    float dy = e->fdy;
                    if (e->sy > y_top)
                    {
                        x_top = x0 + dx * (e->sy - y_top);
                        sy0 = e->sy;
                    }
                    else
                    {
                        x_top = x0;
                        sy0 = y_top;
                    }

                    if (e->ey < y_bottom)
                    {
                        x_bottom = x0 + dx * (e->ey - y_top);
                        sy1 = e->ey;
                    }
                    else
                    {
                        x_bottom = xb;
                        sy1 = y_bottom;
                    }

                    if ((x_top >= 0) && (x_bottom >= 0) && (x_top < len) && (x_bottom < len))
                    {
                        if (((int)x_top) == ((int)x_bottom))
                        {
                            float height = 0;
                            int x = (int)x_top;
                            height = sy1 - sy0;
                            scanline[x] += e->direction * (1 - (x_top - x + (x_bottom - x)) / 2) * height;
                            scanline_fill[x] += e->direction * height;
                        }
                        else
                        {
                            int x = 0;
                            int x1 = 0;
                            int x2 = 0;
                            float y_crossing = 0;
                            float step = 0;
                            float sign = 0;
                            float area = 0;
                            if (x_top > x_bottom)
                            {
                                float t = 0;
                                sy0 = y_bottom - (sy0 - y_top);
                                sy1 = y_bottom - (sy1 - y_top);
                                t = sy0;
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

                            x1 = (int)x_top;
                            x2 = (int)x_bottom;
                            y_crossing = (x1 + 1 - x0) * dy + y_top;
                            sign = e->direction;
                            area = sign * (y_crossing - sy0);
                            scanline[x1] += area * (1 - (x_top - x1 + (x1 + 1 - x1)) / 2);
                            step = sign * dy;
                            for (x = x1 + 1; x < x2; ++x)
                            {
                                scanline[x] += area + step / 2;
                                area += step;
                            }

                            y_crossing += dy * (x2 - (x1 + 1));
                            scanline[x2] += area + sign * (1 - (x2 - x2 + (x_bottom - x2)) / 2) * (sy1 - y_crossing);
                            scanline_fill[x2] += sign * (sy1 - sy0);
                        }
                    }
                    else
                    {
                        int x = 0;
                        for (x = 0; x < len; ++x)
                        {
                            float y0 = y_top;
                            float x1 = x;
                            float x2 = x + 1;
                            float x3 = xb;
                            float y3 = y_bottom;
                            float y1 = (x - x0) / dx + y_top;
                            float y2 = (x + 1 - x0) / dx + y_top;
                            if ((x0 < x1) && (x3 > x2))
                            {
                                HandleClippedEdge(scanline, x, e, x0, y0, x1, y1);
                                HandleClippedEdge(scanline, x, e, x1, y1, x2, y2);
                                HandleClippedEdge(scanline, x, e, x2, y2, x3, y3);
                            }
                            else if ((x3 < x1) && (x0 > x2))
                            {
                                HandleClippedEdge(scanline, x, e, x0, y0, x2, y2);
                                HandleClippedEdge(scanline, x, e, x2, y2, x1, y1);
                                HandleClippedEdge(scanline, x, e, x1, y1, x3, y3);
                            }
                            else if ((x0 < x1) && (x3 > x1))
                            {
                                HandleClippedEdge(scanline, x, e, x0, y0, x1, y1);
                                HandleClippedEdge(scanline, x, e, x1, y1, x3, y3);
                            }
                            else if ((x3 < x1) && (x0 > x1))
                            {
                                HandleClippedEdge(scanline, x, e, x0, y0, x1, y1);
                                HandleClippedEdge(scanline, x, e, x1, y1, x3, y3);
                            }
                            else if ((x0 < x2) && (x3 > x2))
                            {
                                HandleClippedEdge(scanline, x, e, x0, y0, x2, y2);
                                HandleClippedEdge(scanline, x, e, x2, y2, x3, y3);
                            }
                            else if ((x3 < x2) && (x0 > x2))
                            {
                                HandleClippedEdge(scanline, x, e, x0, y0, x2, y2);
                                HandleClippedEdge(scanline, x, e, x2, y2, x3, y3);
                            }
                            else
                            {
                                HandleClippedEdge(scanline, x, e, x0, y0, x3, y3);
                            }
                        }
                    }
                }
                e = e->next;
            }
        }

        public static void SortEdgesInsertSort(TTEdge* p, int n)
        {
            int i = 0;
            int j = 0;
            for (i = 1; i < n; ++i)
            {
                TTEdge t = p[i];
                TTEdge* a = &t;
                j = i;
                while (j > 0)
                {
                    TTEdge* b = &p[j - 1];
                    int c = a->p0.y < b->p0.y ? 1 : 0;
                    if (c == 0)
                        break;
                    p[j] = p[j - 1];
                    --j;
                }

                if (i != j)
                    p[j] = t;
            }
        }

        public static void SortEdgesQuickSort(TTEdge* p, int n)
        {
            while (n > 12)
            {
                var t = new TTEdge();
                int c01 = 0;
                int c12 = 0;
                int c = 0;
                int m = 0;
                int i = 0;
                int j = 0;
                m = n >> 1;
                c01 = (&p[0])->p0.y < (&p[m])    ->p0.y ? 1 : 0;
                c12 = (&p[m])->p0.y < (&p[n - 1])->p0.y ? 1 : 0;
                if (c01 != c12)
                {
                    int z = 0;
                    c = (&p[0])->p0.y < (&p[n - 1])->p0.y ? 1 : 0;
                    z = (c == c12) ? 0 : n - 1;
                    t = p[z];
                    p[z] = p[m];
                    p[m] = t;
                }

                t = p[0];
                p[0] = p[m];
                p[m] = t;
                i = 1;
                j = n - 1;
                for (; ; )
                {
                    for (; ; ++i)
                    {
                        if (!((&p[i])->p0.y < (&p[0])->p0.y))
                            break;
                    }

                    for (; ; --j)
                    {
                        if (!((&p[0])->p0.y < (&p[j])->p0.y))
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
                    p += i;
                    n -= i;
                }
                else
                {
                    SortEdgesQuickSort(p + i, n - i);
                    n = j;
                }
            }
        }

        public static void SortEdges(TTEdge* p, int n)
        {
            SortEdgesQuickSort(p, n);
            SortEdgesInsertSort(p, n);
        }
    }
}
