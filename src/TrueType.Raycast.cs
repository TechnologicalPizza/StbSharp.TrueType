using System;
using System.Numerics;

namespace StbSharp
{
    public partial class TrueType
    {
        public static int RayIntersectBezier(
            Vector2 ray, Vector2 orig,
            Vector2 q0, Vector2 q1, Vector2 q2,
            out Vector2 hit1, out Vector2 hit2)
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
                float rcp_len2 = 1f / ray.LengthSquared();
                Vector2 rayn = ray * rcp_len2;
                float q0d = (q0 * rayn).LengthSquared();
                float q1d = (q1 * rayn).LengthSquared();
                float q2d = (q2 * rayn).LengthSquared();
                float rod = (orig * rayn).LengthSquared();
                float q10d = q1d - q0d;
                float q20d = q2d - q0d;
                float q0rd = q0d - rod;
                hit1.X = q0rd + s0 * (2f - 2f * s0) * q10d + s0 * s0 * q20d;
                hit1.Y = a * s0 + b;
                if (num_s > 1)
                {
                    hit2.X = q0rd + s1 * (2f - 2f * s1) * q10d + s1 * s1 * q20d;
                    hit2.Y = a * s1 + b;
                    return 2;
                }
                else
                {
                    hit2 = default;
                    return 1;
                }
            }
        }

        public static int ComputeCrossingsX(Vector2 point, ReadOnlySpan<Vertex> vertices)
        {
            Vector2 origin = point;
            Vector2 ray = new Vector2(1, 0);

            float y_frac = point.Y % 1f;
            if (y_frac < 0.01f)
                point.Y += 0.01f;
            else if (y_frac > 0.99f)
                point.Y -= 0.01f;

            int winding = 0;
            Vector2 q0;
            Vector2 q1;
            Vector2 q2;

            for (int i = 1; i < vertices.Length; i++)
            {
                ref readonly Vertex pvertex = ref vertices[i - 1];
                ref readonly Vertex vertex = ref vertices[i];

                if (vertex.type == VertexType.Line)
                {
                    short p0x = pvertex.X;
                    short p0y = pvertex.Y;
                    short p1x = vertex.X;
                    short p1y = vertex.Y;

                    if ((point.Y > (p0y < p1y ? p0y : p1y)) &&
                        (point.Y < (p0y < p1y ? p1y : p0y)) &&
                        (point.X > (p0x < p1x ? p0x : p1x)))
                    {
                        float x_inter = (point.Y - p0y) / (p1y - p0y) * (p1x - p0x) + p0x;
                        if (x_inter < point.X)
                            winding += (p0y < p1y) ? 1 : -1;
                    }
                }
                else if (vertex.type == VertexType.Curve)
                {
                    short p0x = pvertex.X;
                    short p0y = pvertex.Y;
                    short p1x = vertex.cx;
                    short p1y = vertex.cy;
                    short p2x = vertex.X;
                    short p2y = vertex.Y;
                    short ax = p0x < (p1x < p2x ? p1x : p2x) ? p0x : (p1x < p2x ? p1x : p2x);
                    short ay = p0y < (p1y < p2y ? p1y : p2y) ? p0y : (p1y < p2y ? p1y : p2y);
                    short by = p0y < (p1y < p2y ? p2y : p1y) ? (p1y < p2y ? p2y : p1y) : p0y;

                    if ((point.Y > ay) &&
                        (point.Y < by) &&
                        (point.X > ax))
                    {
                        q0 = new Vector2(p0x, p0y);
                        q1 = new Vector2(p1x, p1y);
                        q2 = new Vector2(p2x, p2y);

                        if (q0 == q1 || q1 == q2)
                        {
                            if ((point.Y > (p0y < p2y ? p0y : p2y)) &&
                                (point.Y < (p0y < p2y ? p2y : p0y)) &&
                                (point.X > (p0x < p2x ? p0x : p2x)))
                            {
                                float x_inter = (point.Y - p0y) / (p2y - p0y) * (p2x - p0x) + p0x;
                                if (x_inter < point.X)
                                    winding += (p0y < p2y) ? 1 : -1;
                            }
                        }
                        else
                        {
                            int num_hits = RayIntersectBezier(
                                ray, origin, q0, q1, q2, out Vector2 hit1, out Vector2 hit2);

                            if (num_hits >= 1)
                            {
                                if (hit1.X < 0)
                                    winding += hit1.Y < 0 ? -1 : 1;
                            }

                            if (num_hits >= 2)
                            {
                                if (hit2.X < 0)
                                    winding += hit2.Y < 0 ? -1 : 1;
                            }
                        }
                    }
                }
            }

            return winding;
        }
    }
}
