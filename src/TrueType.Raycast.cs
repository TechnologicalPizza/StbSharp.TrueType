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
        public static int RayIntersectBezier(
            in Point ray, in Point orig,
            in Point q0, in Point q1, in Point q2, 
            out Point hit1, out Point hit2)
        {
            float q0perp = q0.y * ray.x - q0.x * ray.y;
            float q1perp = q1.y * ray.x - q1.x * ray.y;
            float q2perp = q2.y * ray.x - q2.x * ray.y;
            float roperp = orig.y * ray.x - orig.x * ray.y;
            float a = q0perp - 2 * q1perp + q2perp;
            float b = q1perp - q0perp;
            float c = q0perp - roperp;
            float s0 = 0f;
            float s1 = 0f;
            int num_s = 0;
            if (a != 0)
            {
                float discr = b * b - a * c;
                if (discr > 0)
                {
                    float rcpna = -1 / a;
                    float d = MathF.Sqrt(discr);
                    s0 = (b + d) * rcpna;
                    s1 = (b - d) * rcpna;
                    if ((s0 >= 0) && (s0 <= 1))
                        num_s = 1;
                    if ((d > 0) && (s1 >= 0) && (s1 <= 1))
                    {
                        if (num_s == 0)
                            s0 = s1;
                        ++num_s;
                    }
                }
            }
            else
            {
                s0 = c / (-2 * b);
                if ((s0 >= 0) && (s0 <= 1))
                    num_s = 1;
            }

            if (num_s == 0)
            {
                hit1 = default;
                hit2 = default;
                return 0;
            }
            else
            {
                float rcp_len2 = 1 / (ray.x * ray.x + ray.y * ray.y);
                float rayn_x = ray.x * rcp_len2;
                float rayn_y = ray.y * rcp_len2;
                float q0d = q0.x * rayn_x + q0.y * rayn_y;
                float q1d = q1.x * rayn_x + q1.y * rayn_y;
                float q2d = q2.x * rayn_x + q2.y * rayn_y;
                float rod = orig.x * rayn_x + orig.y * rayn_y;
                float q10d = q1d - q0d;
                float q20d = q2d - q0d;
                float q0rd = q0d - rod;
                hit1.x = q0rd + s0 * (2f - 2f * s0) * q10d + s0 * s0 * q20d;
                hit1.y = a * s0 + b;
                if (num_s > 1)
                {
                    hit2.x = q0rd + s1 * (2f - 2f * s1) * q10d + s1 * s1 * q20d;
                    hit2.y = a * s1 + b;
                    return 2;
                }
                else
                {
                    hit2 = default;
                    return 1;
                }
            }
        }

        public static int ComputeCrossingsX(
            float x, float y, int nverts, ReadOnlySpan<Vertex> verts)
        {
            int winding = 0;
            float y_frac = y % 1f;
            if (y_frac < 0.01f)
                y += 0.01f;
            else if (y_frac > 0.99f)
                y -= 0.01f;

            Point ray;
            ray.x = 1f;
            ray.y = 0f;

            Point orig;
            orig.x = x;
            orig.y = y;

            Point q0;
            Point q1;
            Point q2;
            Span<float> hits = stackalloc float[4];

            for (int i = 0; i < nverts; ++i)
            {
                ref readonly Vertex vert = ref verts[i];
                if (vert.type == STBTT_vline)
                {
                    int x0 = verts[i - 1].x;
                    int y0 = verts[i - 1].y;
                    int x1 = vert.x;
                    int y1 = vert.y;
                    if ((y > (y0 < y1 ? y0 : y1)) && (y < (y0 < y1 ? y1 : y0)) &&
                        (x > (x0 < x1 ? x0 : x1)))
                    {
                        float x_inter = (y - y0) / (y1 - y0) * (x1 - x0) + x0;
                        if (x_inter < x)
                            winding += (y0 < y1) ? 1 : -1;
                    }
                }

                if (verts[i].type == STBTT_vcurve)
                {
                    int x0 = verts[i - 1].x;
                    int y0 = verts[i - 1].y;
                    int x1 = vert.cx;
                    int y1 = vert.cy;
                    int x2 = vert.x;
                    int y2 = vert.y;
                    int ax = x0 < (x1 < x2 ? x1 : x2) ? x0 : (x1 < x2 ? x1 : x2);
                    int ay = y0 < (y1 < y2 ? y1 : y2) ? y0 : (y1 < y2 ? y1 : y2);
                    int by = y0 < (y1 < y2 ? y2 : y1) ? (y1 < y2 ? y2 : y1) : y0;

                    if ((y > ay) && (y < by) && (x > ax))
                    {
                        q0.x = x0;
                        q0.y = y0;
                        q1.x = x1;
                        q1.y = y1;
                        q2.x = x2;
                        q2.y = y2;

                        if (Point.Equals(q0, q1) || Point.Equals(q1, q2))
                        {
                            x0 = verts[i - 1].x;
                            y0 = verts[i - 1].y;
                            x1 = vert.x;
                            y1 = vert.y;
                            if ((y > (y0 < y1 ? y0 : y1)) && (y < (y0 < y1 ? y1 : y0)) &&
                                (x > (x0 < x1 ? x0 : x1)))
                            {
                                float x_inter = (y - y0) / (y1 - y0) * (x1 - x0) + x0;
                                if (x_inter < x)
                                    winding += (y0 < y1) ? 1 : -1;
                            }
                        }
                        else
                        {
                            int num_hits = RayIntersectBezier(
                                ray, orig, q0, q1, q2, out var hit1, out var hit2);

                            if (num_hits >= 1)
                                if (hit1.x < 0)
                                    winding += hit1.y < 0 ? -1 : 1;

                            if (num_hits >= 2)
                                if (hit2.x < 0)
                                    winding += hit2.y < 0 ? -1 : 1;
                        }
                    }
                }
            }

            return winding;
        }
    }
}
