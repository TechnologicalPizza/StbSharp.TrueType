
using System;
using System.Numerics;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    partial class TrueType
    {
        public static Vector2[] FlattenCurves(
            ReadOnlySpan<Vertex> vertices, float objspace_flatness,
            out int[] contour_lengths, out int num_contours)
        {
            int n = 0;
            for (int i = 0; i < vertices.Length; i++)
                if (vertices[i].type == VertexType.Move)
                    n++;

            num_contours = n;
            if (n == 0)
            {
                contour_lengths = null;
                return null;
            }

            contour_lengths = new int[n];
            Vector2[] points = null;

            float objspace_flatness_squared = objspace_flatness * objspace_flatness;
            int num_points = 0;
            int start = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1)
                    points = new Vector2[num_points];

                var pos = Vector2.Zero;
                num_points = 0;
                n = -1;
                for (int i = 0; i < vertices.Length; ++i)
                {
                    ref readonly Vertex vertex = ref vertices[i];
                    switch (vertex.type)
                    {
                        case VertexType.Move:
                            if (n >= 0)
                                contour_lengths[n] = num_points - start;
                            n++;
                            start = num_points;
                            pos.X = vertex.X;
                            pos.Y = vertex.Y;
                            AddPoint(points, num_points++, pos.X, pos.Y);
                            break;

                        case VertexType.Line:
                            pos.X = vertex.X;
                            pos.Y = vertex.Y;
                            AddPoint(points, num_points++, pos.X, pos.Y);
                            break;

                        case VertexType.Curve:
                            TesselateCurve(
                                points, ref num_points,
                                pos.X, pos.Y,
                                vertex.cx, vertex.cy,
                                vertex.X, vertex.Y,
                                objspace_flatness_squared, 0);
                            pos.X = vertex.X;
                            pos.Y = vertex.Y;
                            break;

                        case VertexType.Cubic:
                            TesselateCubic(
                                points, ref num_points, 
                                pos.X, pos.Y,
                                vertex.cx, vertex.cy,
                                vertex.cx1, vertex.cy1,
                                vertex.X, vertex.Y,
                                objspace_flatness_squared, 0);
                            pos.X = vertex.X;
                            pos.Y = vertex.Y;
                            break;
                    }
                }

                contour_lengths[n] = num_points - start;
            }

            return points;
        }

        public static void Rasterize(
            Bitmap result, float flatness_in_pixels, ReadOnlySpan<Vertex> vertices,
            Vector2 scale, Vector2 shift, Vector2 offset, IntPoint pixelOffset, bool invert)
        {
            float scaleValue = scale.X > scale.Y ? scale.Y : scale.X;
            float objspace_flatness = flatness_in_pixels / scaleValue;

            Vector2[] windings = FlattenCurves(
                vertices, objspace_flatness,
                out int[] winding_lengths, out int winding_count);

            if (windings == null)
                return;

            Rasterize(
                result, windings, winding_lengths.AsSpan(0, winding_count),
                scale, shift, offset, pixelOffset, invert);
        }

        public static unsafe void RasterizeSortedEdges(
            Bitmap result, Span<Edge> e, int n, int vsubsample,
            Vector2 offset, IntPoint pixelOffset)
        {
            var hh = new Heap();
            ActiveEdge* active = null;

            const int maxScanlineStackHalf = 256;

            Span<float> scanline_full = result.w <= maxScanlineStackHalf
                ? stackalloc float[result.w * 2 + 1]
                : new float[result.w * 2 + 1];

            try
            {
                var scanline = scanline_full.Slice(0, result.w);
                var scanline_fill = scanline_full.Slice(result.w);

                float y = offset.Y;
                e[n].p0.Y = offset.Y + result.h + 1;

                int j = 0;
                while (j < result.h)
                {
                    CRuntime.MemSet(scanline_full, 0);

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
                    while (e[ie].p0.Y <= scan_y_bottom)
                    {
                        if (e[ie].p0.Y != e[ie].p1.Y)
                        {
                            ActiveEdge* z = NewActive(ref hh, e[ie], offset.X, scan_y_top);
                            if (z != null)
                            {
                                if (j == 0 && offset.Y != 0)
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
                    var row = result.pixels.Slice((j + pixelOffset.Y) * result.stride + pixelOffset.X);
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
                        z->fx += z->fd.X;
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
            Bitmap result, ReadOnlySpan<Vector2> pts, ReadOnlySpan<int> windings,
            Vector2 scale, Vector2 shift, Vector2 offset, IntPoint pixelOffset, bool invert)
        {
            int winding_sum = 0;
            for (int i = 0; i < windings.Length; i++)
                winding_sum += windings[i];

            var e = new Edge[winding_sum + 1];

            float y_scale_inv = invert ? -scale.Y : scale.Y;
            int vsubsample = 1;
            int m = 0;
            int n = 0;
            for (int i = 0; i < windings.Length; ++i)
            {
                var p = pts.Slice(m);
                m += windings[i];
                int j = windings[i] - 1;

                for (int k = 0; k < windings[i]; j = k++)
                {
                    if (p[j].Y == p[k].Y)
                        continue;

                    int a;
                    int b;
                    if ((invert && (p[j].Y > p[k].Y)) ||
                        (!invert && (p[j].Y < p[k].Y)))
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

                    e[n].p0.X = p[a].X * scale.X + shift.X;
                    e[n].p0.Y = (p[a].Y * y_scale_inv + shift.Y) * vsubsample;
                    e[n].p1.X = p[b].X * scale.X + shift.X;
                    e[n].p1.Y = (p[b].Y * y_scale_inv + shift.Y) * vsubsample;
                    ++n;
                }
            }

            SortEdges(e, n);
            RasterizeSortedEdges(result, e, n, vsubsample, offset, pixelOffset);
        }
    }
}
