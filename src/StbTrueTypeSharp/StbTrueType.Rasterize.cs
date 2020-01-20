
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
        public static TTPoint* FlattenCurves(
            TTVertex* vertices, int num_verts, float objspace_flatness,
            out int* contour_lengths, out int num_contours)
        {
            int n = 0;
            int i = 0;
            for (i = 0; i < num_verts; ++i)
            {
                if (vertices[i].type == STBTT_vmove)
                    n++;
            }

            num_contours = n;
            if (n == 0)
            {
                contour_lengths = null;
                return null;
            }

            contour_lengths = (int*)CRuntime.malloc(sizeof(int) * n);
            if (contour_lengths == null)
            {
                num_contours = 0;
                return null;
            }

            TTPoint* points = null;
            float objspace_flatness_squared = objspace_flatness * objspace_flatness;

            int num_points = 0;
            int start = 0;
            int pass = 0;
            for (pass = 0; pass < 2; ++pass)
            {
                float x = 0f;
                float y = 0f;
                if (pass == 1)
                {
                    points = (TTPoint*)CRuntime.malloc(num_points * sizeof(TTPoint));
                    if (points == null)
                        goto error;
                }

                num_points = 0;
                n = -1;
                for (i = 0; i < num_verts; ++i)
                {
                    switch (vertices[i].type)
                    {
                        case STBTT_vmove:
                            if (n >= 0)
                                contour_lengths[n] = num_points - start;
                            ++n;
                            start = num_points;
                            x = vertices[i].x;
                            y = vertices[i].y;
                            AddPoint(points, num_points++, x, y);
                            break;

                        case STBTT_vline:
                            x = vertices[i].x;
                            y = vertices[i].y;
                            AddPoint(points, num_points++, x, y);
                            break;

                        case STBTT_vcurve:
                            TesselateCurve(points, &num_points, x, y,
                                vertices[i].cx, vertices[i].cy, vertices[i].x,
                                vertices[i].y, objspace_flatness_squared, 0);
                            x = vertices[i].x;
                            y = vertices[i].y;
                            break;

                        case STBTT_vcubic:
                            TesselateCubic(points, &num_points, x, y,
                                vertices[i].cx, vertices[i].cy, vertices[i].cx1,
                                vertices[i].cy1, vertices[i].x, vertices[i].y,
                                objspace_flatness_squared, 0);
                            x = vertices[i].x;
                            y = vertices[i].y;
                            break;
                    }
                }

                contour_lengths[n] = num_points - start;
            }

            return points;

            error:
            CRuntime.free(points);
            CRuntime.free(contour_lengths);
            contour_lengths = null;
            num_contours = 0;
            return null;
        }

        public static void Rasterize(
            TTBitmap result, float flatness_in_pixels, TTVertex* vertices, int num_verts,
            TTPoint scale, TTPoint shift, TTIntPoint offset, TTIntPoint pixelOffset, bool invert)
        {
            float scaleValue = scale.x > scale.y ? scale.y : scale.x;
            float objspace_flatness = flatness_in_pixels / scaleValue;
            TTPoint* windings = FlattenCurves(
                vertices, num_verts, objspace_flatness, out int* winding_lengths, out int winding_count);

            if (windings != null)
            {
                Rasterize(
                    result, windings, winding_lengths, winding_count, scale, shift, offset, pixelOffset, invert);
                
                CRuntime.free(winding_lengths);
                CRuntime.free(windings);
            }
        }

        public static void RasterizeSortedEdges(
            TTBitmap result, TTEdge* e, int n, int vsubsample,
            TTIntPoint offset, TTIntPoint pixelOffset)
        {
            var hh = new TTHeap();
            TTActiveEdge* active = null;
            int y = 0;
            int j = 0;
            int i = 0;

            float* scanline_data = stackalloc float[129];
            float* scanline;
            if (result.w > 64)
                scanline = (float*)CRuntime.malloc((result.w * 2 + 1) * sizeof(float));
            else
                scanline = scanline_data;

            float* scanline2 = scanline + result.w;
            y = offset.y;
            e[n].p0.y = (float)(offset.y + result.h) + 1;
            while (j < result.h)
            {
                float scan_y_top = y + 0f;
                float scan_y_bottom = y + 1f;
                TTActiveEdge** step = &active;
                CRuntime.memset(scanline, 0, result.w * sizeof(float));
                CRuntime.memset(scanline2, 0, (result.w + 1) * sizeof(float));
                while ((*step) != null)
                {
                    TTActiveEdge* z = *step;
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

                while (e->p0.y <= scan_y_bottom)
                {
                    if (e->p0.y != e->p1.y)
                    {
                        TTActiveEdge* z = NewActive(&hh, e, offset.x, scan_y_top);
                        if (z != null)
                        {
                            if (j == 0 && offset.y != 0)
                                if (z->ey < scan_y_top)
                                    z->ey = scan_y_top;

                            z->next = active;
                            active = z;
                        }
                    }
                    e++;
                }

                if (active != null)
                    FillActiveEdgesNew(scanline, scanline2 + 1, result.w, active, scan_y_top);

                float sum = 0f;
                for (i = 0; i < result.w; ++i)
                {
                    sum += scanline2[i];
                    float k = scanline[i] + sum;
                    k = Math.Abs(k) * 255 + 0.5f;

                    if (k > 255)
                        k = 255;
                    byte m = (byte)k;
                    result.pixels[(j + pixelOffset.y) * result.stride + i + pixelOffset.x] = m;
                }

                step = &active;
                while ((*step) != null)
                {
                    TTActiveEdge* z = *step;
                    z->fx += z->fdx;
                    step = &(*step)->next;
                }

                y++;
                j++;
            }

            HeapCleanup(&hh);
            if (scanline != scanline_data)
                CRuntime.free(scanline);
        }

        public static void Rasterize(
            TTBitmap result, TTPoint* pts, int* wcount, int windings,
            TTPoint scale, TTPoint shift, TTIntPoint offset, TTIntPoint pixelOffset, bool invert)
        {
            int n = 0;
            int i = 0;
            for (i = 0; i < windings; ++i)
                n += wcount[i];

            var e = (TTEdge*)CRuntime.malloc(sizeof(TTEdge) * (n + 1));
            if (e == null)
                return;

            float y_scale_inv = invert ? -scale.y : scale.y;
            int vsubsample = 1;
            int j = 0;
            int k = 0;
            int m = 0;
            n = 0;
            for (i = 0; i < windings; ++i)
            {
                TTPoint* p = pts + m;
                m += wcount[i];
                j = wcount[i] - 1;

                for (k = 0; k < wcount[i]; j = k++)
                {
                    int a = k;
                    int b = j;
                    if (p[j].y == p[k].y)
                        continue;

                    if ((invert && (p[j].y > p[k].y)) || (!invert && (p[j].y < p[k].y)))
                    {
                        e[n].invert = true;
                        a = j;
                        b = k;
                    }
                    else
                        e[n].invert = false;

                    e[n].p0.x = p[a].x * scale.x + shift.x;
                    e[n].p0.y = (p[a].y * y_scale_inv + shift.y) * vsubsample;
                    e[n].p1.x = p[b].x * scale.x + shift.x;
                    e[n].p1.y = (p[b].y * y_scale_inv + shift.y) * vsubsample;
                    ++n;
                }
            }

            SortEdges(e, n);
            RasterizeSortedEdges(result, e, n, vsubsample, offset, pixelOffset);
            CRuntime.free(e);
        }
    }
}
