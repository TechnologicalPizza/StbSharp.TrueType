
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
        public static Point[] FlattenCurves(
            ReadOnlySpan<Vertex> vertices, float objspace_flatness,
            out int[] contour_lengths, out int num_contours)
        {
            int n = 0;
            for (int i = 0; i < vertices.Length; ++i)
                if (vertices[i].type == STBTT_vmove)
                    n++;

            num_contours = n;
            if (n == 0)
            {
                contour_lengths = null;
                return null;
            }

            contour_lengths = new int[n];

            Point[] points = null;
            float objspace_flatness_squared = objspace_flatness * objspace_flatness;

            int num_points = 0;
            int start = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                float x = 0f;
                float y = 0f;
                if (pass == 1)
                    points = new Point[num_points];

                num_points = 0;
                n = -1;
                for (int i = 0; i < vertices.Length; ++i)
                {
                    ref readonly Vertex vert = ref vertices[i];
                    switch (vert.type)
                    {
                        case STBTT_vmove:
                            if (n >= 0)
                                contour_lengths[n] = num_points - start;
                            n++;
                            start = num_points;
                            x = vert.x;
                            y = vert.y;
                            AddPoint(points, num_points++, x, y);
                            break;

                        case STBTT_vline:
                            x = vert.x;
                            y = vert.y;
                            AddPoint(points, num_points++, x, y);
                            break;

                        case STBTT_vcurve:
                            TesselateCurve(
                                points, &num_points, x, y,
                                vert.cx, vert.cy, vert.x, vert.y,
                                objspace_flatness_squared, 0);
                            x = vert.x;
                            y = vert.y;
                            break;

                        case STBTT_vcubic:
                            TesselateCubic(
                                points, ref num_points, x, y,
                                vert.cx, vert.cy, vert.cx1, vert.cy1, vert.x, vert.y,
                                objspace_flatness_squared, 0);
                            x = vert.x;
                            y = vert.y;
                            break;
                    }
                }

                contour_lengths[n] = num_points - start;
            }

            return points;
        }

        public static void Rasterize(
            Bitmap result, float flatness_in_pixels, ReadOnlySpan<Vertex> vertices,
            Point scale, Point shift, IntPoint offset, IntPoint pixelOffset, bool invert)
        {
            float scaleValue = scale.x > scale.y ? scale.y : scale.x;
            float objspace_flatness = flatness_in_pixels / scaleValue;

            Point[] windings = FlattenCurves(
                vertices, objspace_flatness,
                out int[] winding_lengths, out int winding_count);

            if (windings == null)
                return;

            Rasterize(
                result, windings, winding_lengths.AsSpan(0, winding_count),
                scale, shift, offset, pixelOffset, invert);
        }

        public static void RasterizeSortedEdges(
            Bitmap result, Span<Edge> e, int n, int vsubsample,
            IntPoint offset, IntPoint pixelOffset)
        {
            var hh = new Heap();
            ActiveEdge* active = null;
            int y = 0;
            int j = 0;

            const int maxScanlineStackHalf = 256;

            Span<float> scanline_full = result.w <= maxScanlineStackHalf
                ? stackalloc float[result.w * 2 + 1]
                : new float[result.w * 2 + 1];

            try
            {
                var scanline = scanline_full.Slice(0, result.w);
                var scanline_fill = scanline_full.Slice(result.w);

                y = offset.y;
                e[n].p0.y = (float)(offset.y + result.h) + 1;
                while (j < result.h)
                {
                    scanline_full.Fill(0);

                    float scan_y_top = y;
                    float scan_y_bottom = y + 1f;
                    ActiveEdge** step = &active;
                    while ((*step) != null)
                    {
                        ActiveEdge* z = *step;
                        if (z->ey <= scan_y_top)
                        {
                            *step = z->next;
                            z->direction = 0f;
                            HeapFree(&hh, z);
                        }
                        else
                        {
                            step = &(*step)->next;
                        }
                    }

                    int oof = 0;
                    int ie = 0;
                    while (e[ie].p0.y <= scan_y_bottom)
                    {
                        if (e[ie].p0.y != e[ie].p1.y)
                        {
                            ActiveEdge* z = NewActive(ref hh, e[ie], offset.x, scan_y_top);
                            if (z != null)
                            {
                                if (j == 0 && offset.y != 0)
                                    if (z->ey < scan_y_top)
                                        z->ey = scan_y_top;

                                z->next = active;
                                active = z;

                                oof++;
                            }
                        }
                        ie++;
                    }
                    e = e.Slice(ie);

                    if (active != null)
                        FillActiveEdgesNew(scanline, scanline_fill, active, scan_y_top);

                    float sum = 0f;
                    var row = result.pixels.Slice((j + pixelOffset.y) * result.stride + pixelOffset.x);
                    for (int i = 0; i < scanline.Length; ++i)
                    {
                        sum += scanline_fill[i];
                        float k = scanline[i] + sum;
                        k = Math.Abs(k) * 255 + 0.5f;

                        if (k > 255)
                            k = 255;
                        row[i] = (byte)k;
                    }

                    step = &active;
                    while ((*step) != null)
                    {
                        ActiveEdge* z = *step;
                        z->fx += z->fdx;
                        step = &(*step)->next;
                    }

                    y++;
                    j++;
                }
            }
            finally
            {
                HeapCleanup(&hh);
            }
        }

        public static void Rasterize(
            Bitmap result, ReadOnlySpan<Point> pts, ReadOnlySpan<int> windings,
            Point scale, Point shift, IntPoint offset, IntPoint pixelOffset, bool invert)
        {
            int n = 0;
            for (int i = 0; i < windings.Length; ++i)
                n += windings[i];

            var e = new Edge[n + 1];
            n = 0;

            float y_scale_inv = invert ? -scale.y : scale.y;
            int vsubsample = 1;
            int m = 0;
            for (int i = 0; i < windings.Length; ++i)
            {
                var p = pts.Slice(m);
                m += windings[i];
                int j = windings[i] - 1;

                for (int k = 0; k < windings[i]; j = k++)
                {
                    if (p[j].y == p[k].y)
                        continue;

                    int a;
                    int b;
                    if ((invert && (p[j].y > p[k].y)) ||
                        (!invert && (p[j].y < p[k].y)))
                    {
                        e[n].invert = true;
                        a = j;
                        b = k;
                    }
                    else
                    {
                        e[n].invert = false;
                        a = k;
                        b = j;
                    }

                    e[n].p0.x = p[a].x * scale.x + shift.x;
                    e[n].p0.y = (p[a].y * y_scale_inv + shift.y) * vsubsample;
                    e[n].p1.x = p[b].x * scale.x + shift.x;
                    e[n].p1.y = (p[b].y * y_scale_inv + shift.y) * vsubsample;
                    ++n;
                }
            }

            SortEdges(e, n);
            RasterizeSortedEdges(result, e, n, vsubsample, offset, pixelOffset);
        }
    }
}
