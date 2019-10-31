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
        public static byte* GetGlyphSDF(
            TTFontInfo info, float scale, int glyph, int padding,
            byte onedge_value, float pixel_dist_scale,
            out int width, out int height, out int xoff, out int yoff)
        {
            width = 0;
            height = 0;
            xoff = 0;
            yoff = 0;

            float scale_x = scale;
            float scale_y = scale;

            if (scale_x == 0)
                scale_x = scale_y;
            if (scale_y == 0)
            {
                if (scale_x == 0)
                    return null;
                scale_y = scale_x;
            }

            GetGlyphBitmapBoxSubpixel(
                info, glyph, scale, scale, 0f, 0f,
                out int ix0, out int iy0, out int ix1, out int iy1);

            if ((ix0 == ix1) || (iy0 == iy1))
                return null;

            ix0 -= padding;
            iy0 -= padding;
            ix1 += padding;
            iy1 += padding;
            int w = ix1 - ix0;
            int h = iy1 - iy0;
            width = w;
            height = h;
            xoff = ix0;
            yoff = iy0;
            scale_y = -scale_y;

            int x = 0;
            int y = 0;

            int num_verts = GetGlyphShape(info, glyph, out TTVertex* verts);
            byte* data = (byte*)CRuntime.malloc(w * h);

            int precomputeSize = num_verts * sizeof(float);
            Span<float> precompute = precomputeSize > 2048 
                ? new float[precomputeSize] 
                : stackalloc float[precomputeSize];

            int i = 0;
            int j = 0;
            for (i = 0, j = num_verts - 1; i < num_verts; j = i++)
            {
                TTVertex vertex = verts[i];
                if (vertex.type == STBTT_vline)
                {
                    float x0 = vertex.x * scale_x;
                    float y0 = vertex.y * scale_y;
                    float x1 = verts[j].x * scale_x;
                    float y1 = verts[j].y * scale_y;
                    float dist = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                    precompute[i] = (dist == 0) ? 0f : 1f / dist;
                }
                else if (vertex.type == STBTT_vcurve)
                {
                    float x2 = verts[j].x * scale_x;
                    float y2 = verts[j].y * scale_y;
                    float x1 = vertex.cx * scale_x;
                    float y1 = vertex.cy * scale_y;
                    float x0 = vertex.x * scale_x;
                    float y0 = vertex.y * scale_y;
                    float bx = x0 - 2 * x1 + x2;
                    float by = y0 - 2 * y1 + y2;
                    float len2 = bx * bx + by * by;

                    if (len2 != 0f)
                        precompute[i] = 1f / (bx * bx + by * by);
                    else
                        precompute[i] = 0f;
                }
                else
                    precompute[i] = 0f;
            }

            float* res = stackalloc float[3];

            for (y = iy0; y < iy1; ++y)
            {
                for (x = ix0; x < ix1; ++x)
                {
                    float val = 0;
                    float min_dist = 999999f;
                    float sx = x + 0.5f;
                    float sy = y + 0.5f;
                    float x_gspace = sx / scale_x;
                    float y_gspace = sy / scale_y;
                    int winding = ComputeCrossingsX(x_gspace, y_gspace, num_verts, verts);
                    for (i = 0; i < num_verts; ++i)
                    {
                        float x0 = verts[i].x * scale_x;
                        float y0 = verts[i].y * scale_y;
                        float dist2 = (x0 - sx) * (x0 - sx) + (y0 - sy) * (y0 - sy);
                        if (dist2 < (min_dist * min_dist))
                            min_dist = (float)Math.Sqrt(dist2);

                        if (verts[i].type == STBTT_vline)
                        {
                            float x1 = verts[i - 1].x * scale_x;
                            float y1 = verts[i - 1].y * scale_y;
                            float dist = ((float)Math.Abs(
                                (double)((x1 - x0) * (y0 - sy) - (y1 - y0) * (x0 - sx)))) * precompute[i];

                            if (dist < min_dist)
                            {
                                float dx = x1 - x0;
                                float dy = y1 - y0;
                                float px = x0 - sx;
                                float py = y0 - sy;
                                float t = -(px * dx + py * dy) / (dx * dx + dy * dy);

                                if ((t >= 0f) && (t <= 1f))
                                    min_dist = dist;
                            }
                        }
                        else if (verts[i].type == STBTT_vcurve)
                        {
                            float x2 = verts[i - 1].x * scale_x;
                            float y2 = verts[i - 1].y * scale_y;
                            float x1 = verts[i].cx * scale_x;
                            float y1 = verts[i].cy * scale_y;
                            float box_x0 = (x0 < x1 ? x0 : x1) < x2
                                ? (x0 < x1 ? x0 : x1)
                                : x2;
                            float box_y0 = (y0 < y1 ? y0 : y1) < y2
                                ? (y0 < y1 ? y0 : y1)
                                : y2;
                            float box_x1 = (x0 < x1 ? x1 : x0) < x2
                                ? x2
                                : (x0 < x1 ? x1 : x0);
                            float box_y1 = (y0 < y1 ? y1 : y0) < y2
                                ? y2
                                : (y0 < y1 ? y1 : y0);

                            if (sx > (box_x0 - min_dist) &&
                                sx < (box_x1 + min_dist) &&
                                sy > (box_y0 - min_dist) &&
                                sy < (box_y1 + min_dist))
                            {
                                int num = 0;
                                float ax = x1 - x0;
                                float ay = y1 - y0;
                                float bx = x0 - 2 * x1 + x2;
                                float by = y0 - 2 * y1 + y2;
                                float mx = x0 - sx;
                                float my = y0 - sy;
                                float px = 0;
                                float py = 0;
                                float t = 0;
                                float it = 0;
                                float a_inv = precompute[i];

                                if (a_inv == 0)
                                {
                                    float a = 3 * (ax * bx + ay * by);
                                    float b = 2 * (ax * ax + ay * ay) + (mx * bx + my * by);
                                    float c = mx * ax + my * ay;
                                    if (a == 0)
                                    {
                                        if (b != 0)
                                            res[num++] = -c / b;
                                    }
                                    else
                                    {
                                        float discriminant = b * b - 4 * a * c;
                                        if (discriminant < 0)
                                            num = 0;
                                        else
                                        {
                                            float root = (float)Math.Sqrt(discriminant);
                                            res[0] = (-b - root) / (2 * a);
                                            res[1] = (-b + root) / (2 * a);
                                            num = 2;
                                        }
                                    }
                                }
                                else
                                {
                                    float b = 3 * (ax * bx + ay * by) * a_inv;
                                    float c = (2 * (ax * ax + ay * ay) + (mx * bx + my * by)) * a_inv;
                                    float d = (mx * ax + my * ay) * a_inv;
                                    num = SolveCubic(b, c, d, res);
                                }

                                if ((num >= 1) && (res[0] >= 0f) && (res[0] <= 1f))
                                {
                                    t = res[0];
                                    it = 1f - t;
                                    px = it * it * x0 + 2 * t * it * x1 + t * t * x2;
                                    py = it * it * y0 + 2 * t * it * y1 + t * t * y2;
                                    dist2 = (px - sx) * (px - sx) + (py - sy) * (py - sy);

                                    if (dist2 < (min_dist * min_dist))
                                        min_dist = (float)Math.Sqrt(dist2);
                                }

                                if ((num >= 2) && (res[1] >= 0f) && (res[1] <= 1f))
                                {
                                    t = res[1];
                                    it = 1f - t;
                                    px = it * it * x0 + 2 * t * it * x1 + t * t * x2;
                                    py = it * it * y0 + 2 * t * it * y1 + t * t * y2;
                                    dist2 = (px - sx) * (px - sx) + (py - sy) * (py - sy);

                                    if (dist2 < (min_dist * min_dist))
                                        min_dist = (float)Math.Sqrt(dist2);
                                }

                                if ((num >= 3) && (res[2] >= 0f) && (res[2] <= 1f))
                                {
                                    t = res[2];
                                    it = 1f - t;
                                    px = it * it * x0 + 2 * t * it * x1 + t * t * x2;
                                    py = it * it * y0 + 2 * t * it * y1 + t * t * y2;
                                    dist2 = (px - sx) * (px - sx) + (py - sy) * (py - sy);

                                    if (dist2 < (min_dist * min_dist))
                                        min_dist = (float)Math.Sqrt(dist2);
                                }
                            }
                        }
                    }

                    if (winding == 0)
                        min_dist = -min_dist;
                    val = onedge_value + pixel_dist_scale * min_dist;
                    if (val < 0)
                        val = 0f;
                    else if (val > 255)
                        val = 255;
                    data[(y - iy0) * w + (x - ix0)] = (byte)val;
                }
            }

            FreeShape(verts);

            return data;
        }

        public static byte* GetCodepointSDF(
            TTFontInfo info, float scale, int codepoint, int padding,
            byte onedge_value, float pixel_dist_scale,
            out int width, out int height, out int xoff, out int yoff)
        {
            return GetGlyphSDF(
                info, scale, FindGlyphIndex(info, codepoint),
                padding, onedge_value, pixel_dist_scale,
                out width, out height, out xoff, out yoff);
        }

        public static void FreeSDF(byte* bitmap)
        {
            CRuntime.free(bitmap);
        }
    }
}
