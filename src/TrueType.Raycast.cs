using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace StbSharp
{
    public partial class TrueType
    {
        public static int RayIntersectBezier(
            Vector2 ray, Vector2 orig,
            Vector2 q0, Vector2 q1, Vector2 q2,
            out Vector2 hit1,
            out Vector2 hit2)
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
                Unsafe.SkipInit(out hit1);
                Unsafe.SkipInit(out hit2);
                return 0;
            }
            else
            {
                float rcp_len2 = 1f / ray.LengthSquared();
                Vector2 rayn = ray * rcp_len2;
                float q0d = Vector2.Dot(q0, rayn);
                float q1d = Vector2.Dot(q1, rayn);
                float q2d = Vector2.Dot(q2, rayn);
                float rod = Vector2.Dot(orig, rayn);
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
                    Unsafe.SkipInit(out hit2);
                    return 1;
                }
            }
        }

        public static int ComputeCrossingsX(
            Vector2 point, ReadOnlySpan<Vertex> vertices)
        {
            Vector2 origin = point;
            Vector2 ray = new(1, 0);

            float y_frac = point.Y % 1f;
            if (y_frac < 0.01f)
                point.Y += 0.01f;
            else if (y_frac > 0.99f)
                point.Y -= 0.01f;

            int winding = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                ref readonly Vertex vertex = ref vertices[i];
                if (vertex.Type == VertexType.Line)
                {
                    ref readonly Vertex pvertex = ref vertices[i - 1];
                    Vector2 p0 = pvertex.P;
                    Vector2 p1 = vertex.P;

                    if ((point.Y > (p0.Y < p1.Y ? p0.Y : p1.Y)) &&
                        (point.Y < (p0.Y < p1.Y ? p1.Y : p0.Y)) &&
                        (point.X > (p0.X < p1.X ? p0.X : p1.X)))
                    {
                        float x_inter = (point.Y - p0.Y) / (p1.Y - p0.Y) * (p1.X - p0.X) + p0.X;
                        if (x_inter < point.X)
                            winding += (p0.Y < p1.Y) ? 1 : -1;
                    }
                }
                else if (vertex.Type == VertexType.Curve)
                {
                    ref readonly Vertex pvertex = ref vertices[i - 1];
                    Vector2 p0 = pvertex.P;
                    Vector2 p1 = vertex.C0;
                    Vector2 p2 = vertex.P;
                    float ax = p0.X < (p1.X < p2.X ? p1.X : p2.X) ? p0.X : (p1.X < p2.X ? p1.X : p2.X);
                    float ay = p0.Y < (p1.Y < p2.Y ? p1.Y : p2.Y) ? p0.Y : (p1.Y < p2.Y ? p1.Y : p2.Y);
                    float by = p0.Y < (p1.Y < p2.Y ? p2.Y : p1.Y) ? (p1.Y < p2.Y ? p2.Y : p1.Y) : p0.Y;

                    if ((point.Y > ay) &&
                        (point.Y < by) &&
                        (point.X > ax))
                    {
                        if (p0 == p1 || p1 == p2)
                        {
                            p1 = vertex.P;

                            if ((point.Y > (p0.Y < p1.Y ? p0.Y : p1.Y)) &&
                                (point.Y < (p0.Y < p1.Y ? p1.Y : p0.Y)) &&
                                (point.X > (p0.X < p1.X ? p0.X : p1.X)))
                            {
                                float x_inter = (point.Y - p0.Y) / (p1.Y - p0.Y) * (p1.X - p0.X) + p0.X;
                                if (x_inter < point.X)
                                    winding += (p0.Y < p1.Y) ? 1 : -1;
                            }
                        }
                        else
                        {
                            int num_hits = RayIntersectBezier(
                                ray, origin, p0, p1, p2,
                                out Vector2 hit1,
                                out Vector2 hit2);

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
