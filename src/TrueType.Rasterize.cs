using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace StbSharp
{
    public partial class TrueType
    {
        public static Vector2[]? FlattenCurves(
            ReadOnlySpan<Vertex> vertices, float objspaceFlatness,
            out int[]? contourLengths, out int contourCount)
        {
            int n = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].type == VertexType.Move)
                    n++;
            }

            contourCount = n;
            if (n == 0)
            {
                contourLengths = null;
                return null;
            }

            contourLengths = new int[n];
            Vector2[]? points = null;

            float objspace_flatness_squared = objspaceFlatness * objspaceFlatness;
            int num_points = 0;
            int start = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                ref Vector2 pointDst = ref Unsafe.NullRef<Vector2>();

                if (pass == 1)
                {
                    points = new Vector2[num_points];
                    pointDst = ref points[0];
                }

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
                                contourLengths[n] = num_points - start;
                            n++;
                            start = num_points;
                            pos.X = vertex.X;
                            pos.Y = vertex.Y;
                            AddPoint(ref pointDst, num_points++, pos.X, pos.Y);
                            break;

                        case VertexType.Line:
                            pos.X = vertex.X;
                            pos.Y = vertex.Y;
                            AddPoint(ref pointDst, num_points++, pos.X, pos.Y);
                            break;

                        case VertexType.Curve:
                            TesselateCurve(
                                ref pointDst, ref num_points,
                                pos.X, pos.Y,
                                vertex.cx, vertex.cy,
                                vertex.X, vertex.Y,
                                objspace_flatness_squared, 0);
                            pos.X = vertex.X;
                            pos.Y = vertex.Y;
                            break;

                        case VertexType.Cubic:
                            TesselateCubic(
                                ref pointDst, ref num_points,
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

                contourLengths[n] = num_points - start;
            }

            return points;
        }

        public static void Rasterize(
            Bitmap result, float pixelFlatness, ReadOnlySpan<Vertex> vertices,
            Vector2 scale, Vector2 shift, Vector2 offset, IntPoint pixelOffset, bool invert)
        {
            float scaleValue = scale.X > scale.Y ? scale.Y : scale.X;
            float objspaceFlatness = pixelFlatness / scaleValue;

            Vector2[]? windings = FlattenCurves(
                vertices, objspaceFlatness,
                out int[]? winding_lengths, out int winding_count);

            if (windings == null)
                return;

            Rasterize(
                result, windings, winding_lengths.AsSpan(0, winding_count),
                scale, shift, offset, pixelOffset, invert);
        }

        [SkipLocalsInit]
        public static void RasterizeSortedEdges(
            Bitmap result, Span<Edge> e, int n, Vector2 offset, IntPoint pixelOffset)
        {
            const int MaxScanlineStack = 1024;

            int fullScanlineLength = result.Width * 2 + 1;
            Span<float> scanlineBuffer = fullScanlineLength <= MaxScanlineStack
                ? stackalloc float[fullScanlineLength]
                : new float[fullScanlineLength];

            var scanline = scanlineBuffer.Slice(0, result.Width);
            var scanline_fill = scanlineBuffer.Slice(scanline.Length, result.Width + 1);

            float offY = offset.Y;
            e[n].p0.Y = offset.Y + result.Height + 1;

            ActiveEdge? active = null;

            int bmpY = 0;
            while (bmpY < result.Height)
            {
                scanlineBuffer.Clear();

                // find center of pixel for this scanline
                float scan_y_top = offY;
                float scan_y_bottom = offY + 1f;

                ActiveEdge? step = active;
                while (step != null)
                {
                    ActiveEdge z = step;
                    if (z.ey <= scan_y_top)
                    {
                        step = z.next; // delete from list
                        z.direction = 0;
                        // return z object
                        //HeapFree(ref hh, z);
                    }
                    else
                    {
                        step = step.next; // advance through list
                    }
                }

                // insert all edges that start before the bottom of this scanline
                int ie = 0;
                while (e[ie].p0.Y <= scan_y_bottom)
                {
                    if (e[ie].p0.Y != e[ie].p1.Y)
                    {
                        ActiveEdge z = NewActive(e[ie], offset.X, scan_y_top);
                        if (bmpY == 0 && offset.Y != 0)
                        {
                            if (z.ey < scan_y_top)
                            {
                                // this can happen due to subpixel positioning and 
                                // some kind of fp rounding error i think
                                z.ey = scan_y_top;
                            }
                        }
                        z.next = active;
                        active = z;
                    }
                    ie++;
                }
                e = e[ie..];

                if (active != null)
                    FillActiveEdges(ref scanline[0], ref scanline_fill[0], scanline.Length, active, scan_y_top);

                // TODO: output pixel rows instead
                Span<byte> pixel_row = result.Pixels.Slice(
                    ((bmpY + pixelOffset.Y) * result.ByteStride + pixelOffset.X),
                    scanline.Length);

                // TODO: vectorize?
                float sum = 0f;
                for (int x = 0; x < scanline.Length; x++)
                {
                    sum += scanline_fill[x];
                    float k = scanline[x] + sum;
                    k = Math.Abs(k) * byte.MaxValue;
                    pixel_row[x] = k > 255 ? 255 : (byte)k;
                }

                step = active;
                while (step != null)
                {
                    ActiveEdge z = step;
                    z.fx += z.fd.X;
                    step = step.next;
                }

                offY++;
                bmpY++;
            }
        }

        public static void Rasterize(
            Bitmap result, ReadOnlySpan<Vector2> points, ReadOnlySpan<int> windings,
            Vector2 scale, Vector2 shift, Vector2 offset, IntPoint pixelOffset, bool invert)
        {
            int winding_sum = 0;
            for (int i = 0; i < windings.Length; i++)
                winding_sum += windings[i];

            int edgeCount = winding_sum + 1;
            int totalEdgeBytes = edgeCount * Unsafe.SizeOf<Edge>();
            Span<Edge> e = totalEdgeBytes <= 4096 ? stackalloc Edge[edgeCount] : new Edge[edgeCount];

            float y_scale_inv = invert ? -scale.Y : scale.Y;
            int vsubsample = 1;
            int m = 0;
            int n = 0;
            for (int i = 0; i < windings.Length; ++i)
            {
                ReadOnlySpan<Vector2> p = points[m..];
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
                    n++;
                }
            }

            SortEdges(e.Slice(0, n));
            RasterizeSortedEdges(result, e, n, offset, pixelOffset);
        }
    }
}
