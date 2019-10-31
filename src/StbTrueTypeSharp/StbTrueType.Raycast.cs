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
        public static int RayIntersectBezier(
            in TTPoint ray, in TTPoint orig, in TTPoint q0, in TTPoint q1, in TTPoint q2, float* hits)
        {
            float q0perp = q0.Y * ray.X - q0.X * ray.Y;
            float q1perp = q1.Y * ray.X - q1.X * ray.Y;
            float q2perp = q2.Y * ray.X - q2.X * ray.Y;
            float roperp = orig.Y * ray.X - orig.X * ray.Y;
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
                    float d = (float)Math.Sqrt(discr);
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
                return 0;
            else
            {
                float rcp_len2 = 1 / (ray.X * ray.X + ray.Y * ray.Y);
                float rayn_x = ray.X * rcp_len2;
                float rayn_y = ray.Y * rcp_len2;
                float q0d = q0.X * rayn_x + q0.Y * rayn_y;
                float q1d = q1.X * rayn_x + q1.Y * rayn_y;
                float q2d = q2.X * rayn_x + q2.Y * rayn_y;
                float rod = orig.X * rayn_x + orig.Y * rayn_y;
                float q10d = q1d - q0d;
                float q20d = q2d - q0d;
                float q0rd = q0d - rod;
                hits[0] = q0rd + s0 * (2f - 2f * s0) * q10d + s0 * s0 * q20d;
                hits[1] = a * s0 + b;
                if (num_s > 1)
                {
                    hits[2] = q0rd + s1 * (2f - 2f * s1) * q10d + s1 * s1 * q20d;
                    hits[3] = a * s1 + b;
                    return 2;
                }
                else
                    return 1;
            }
        }

        public static int ComputeCrossingsX(float x, float y, int nverts, TTVertex* verts)
        {
            TTPoint ray;
            ray.X = 1f;
            ray.Y = 0f;

            int winding = 0;
            float y_frac = (float)(y % 1.0);
            if (y_frac < 0.01f)
                y += 0.01f;
            else if (y_frac > 0.99f)
                y -= 0.01f;

            TTPoint orig;
            orig.X = x;
            orig.Y = y;

            TTPoint q0;
            TTPoint q1;
            TTPoint q2;
            float* hits = stackalloc float[4];

            for (int i = 0; i < nverts; ++i)
            {
                if (verts[i].type == STBTT_vline)
                {
                    int x0 = verts[i - 1].x;
                    int y0 = verts[i - 1].y;
                    int x1 = verts[i].x;
                    int y1 = verts[i].y;
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
                    int x1 = verts[i].cx;
                    int y1 = verts[i].cy;
                    int x2 = verts[i].x;
                    int y2 = verts[i].y;
                    int ax = x0 < (x1 < x2 ? x1 : x2) ? x0 : (x1 < x2 ? x1 : x2);
                    int ay = y0 < (y1 < y2 ? y1 : y2) ? y0 : (y1 < y2 ? y1 : y2);
                    int by = y0 < (y1 < y2 ? y2 : y1) ? (y1 < y2 ? y2 : y1) : y0;
                    if ((y > ay) && (y < by) && (x > ax))
                    {
                        q0.X = x0;
                        q0.Y = y0;
                        q1.X = x1;
                        q1.Y = y1;
                        q2.X = x2;
                        q2.Y = y2;

                        if (TTPoint.Equals(q0, q1) || TTPoint.Equals(q1, q2))
                        {
                            x0 = verts[i - 1].x;
                            y0 = verts[i - 1].y;
                            x1 = verts[i].x;
                            y1 = verts[i].y;
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
                            int num_hits = RayIntersectBezier(ray, orig, q0, q1, q2, hits);
                            if (num_hits >= 1)
                                if (hits[0] < 0)
                                    winding += hits[1] < 0 ? -1 : 1;
                            if (num_hits >= 2)
                                if (hits[2] < 0)
                                    winding += hits[3] < 0 ? -1 : 1;
                        }
                    }
                }
            }

            return winding;
        }
    }
}
