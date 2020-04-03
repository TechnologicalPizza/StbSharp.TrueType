using System;
using System.Numerics;

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
            in Vector2 ray, in Vector2 orig,
            in Vector2 q0, in Vector2 q1, in Vector2 q2,
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

        public static int ComputeCrossingsX(
            Vector2 point, ReadOnlySpan<Vertex> vertices)
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

            for (int i = 0; i < vertices.Length; ++i)
            {
                ref readonly Vertex vertex = ref vertices[i];
                if (vertex.type == VertexType.Line)
                {
                    var pos0 = new Vector2(vertices[i - 1].X, vertices[i - 1].Y);
                    var pos1 = new Vector2(vertex.X, vertex.Y);

                    if ((point.Y > (pos0.Y < pos1.Y ? pos0.Y : pos1.Y)) &&
                        (point.Y < (pos0.Y < pos1.Y ? pos1.Y : pos0.Y)) &&
                        (point.X > (pos0.X < pos1.X ? pos0.X : pos1.X)))
                    {
                        float x_inter = (point.Y - pos0.Y) / (pos1.Y - pos0.Y) * (pos1.X - pos0.X) + pos0.X;
                        if (x_inter < point.X)
                            winding += (pos0.Y < pos1.Y) ? 1 : -1;
                    }
                }

                if (vertices[i].type == VertexType.Curve)
                {
                    var pos0 = new Vector2(vertices[i - 1].X, vertices[i - 1].Y);
                    var pos1 = new Vector2(vertex.cx, vertex.cy);
                    var pos2 = new Vector2(vertex.X, vertex.Y);
                    float ax = pos0.X < (pos1.X < pos2.X ? pos1.X : pos2.X) ? pos0.X : (pos1.X < pos2.X ? pos1.X : pos2.X);
                    float ay = pos0.Y < (pos1.Y < pos2.Y ? pos1.Y : pos2.Y) ? pos0.Y : (pos1.Y < pos2.Y ? pos1.Y : pos2.Y);
                    float by = pos0.Y < (pos1.Y < pos2.Y ? pos2.Y : pos1.Y) ? (pos1.Y < pos2.Y ? pos2.Y : pos1.Y) : pos0.Y;

                    if ((point.Y > ay) &&
                        (point.Y < by) &&
                        (point.X > ax))
                    {
                        q0 = pos0;
                        q1 = pos1;
                        q2 = pos2;

                        if (q0 == q1 || q1 == q2)
                        {
                            pos0 = new Vector2(vertices[i - 1].X, vertices[i - 1].Y);
                            pos1 = new Vector2(vertex.X, vertex.Y);

                            if ((point.Y > (pos0.Y < pos1.Y ? pos0.Y : pos1.Y)) &&
                                (point.Y < (pos0.Y < pos1.Y ? pos1.Y : pos0.Y)) &&
                                (point.X > (pos0.X < pos1.X ? pos0.X : pos1.X)))
                            {
                                float x_inter = (point.Y - pos0.Y) / (pos1.Y - pos0.Y) * (pos1.X - pos0.X) + pos0.X;
                                if (x_inter < point.X)
                                    winding += (pos0.Y < pos1.Y) ? 1 : -1;
                            }
                        }
                        else
                        {
                            int num_hits = RayIntersectBezier(
                                ray, origin, q0, q1, q2, out var hit1, out var hit2);

                            if (num_hits >= 1)
                                if (hit1.X < 0)
                                    winding += hit1.Y < 0 ? -1 : 1;

                            if (num_hits >= 2)
                                if (hit2.X < 0)
                                    winding += hit2.Y < 0 ? -1 : 1;
                        }
                    }
                }
            }

            return winding;
        }
    }
}
