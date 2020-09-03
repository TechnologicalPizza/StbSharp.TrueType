using System;
using System.Numerics;

namespace StbSharp
{
    public partial class TrueType
    {
        public static byte[]? GetGlyphSDF(
            FontInfo info, Vector2 scale, int glyph, int padding,
            byte onEdgeValue, float pixelDistScale,
            out int width, out int height, out IntPoint offset)
        {
            width = 0;
            height = 0;
            offset = IntPoint.Zero;

            if (scale.X == 0)
                scale.X = scale.Y;
            if (scale.Y == 0)
            {
                if (scale.X == 0)
                    return null;
                scale.Y = scale.X;
            }

            GetGlyphBitmapBoxSubpixel(
                info, glyph, scale, Vector2.Zero, out var glyphBox);

            if (glyphBox.W == 0 || glyphBox.Y == 0)
                return null;

            glyphBox.X -= padding;
            glyphBox.Y -= padding;
            glyphBox.W += padding;
            glyphBox.H += padding;

            width = glyphBox.W;
            height = glyphBox.H;
            offset = glyphBox.Position;
            scale.Y = -scale.Y;

            int num_verts = GetGlyphShape(info, glyph, out Vertex[] src_verts);
            var vertices = src_verts.AsSpan(0, num_verts);

            int precomputeSize = vertices.Length * sizeof(float);
            Span<float> precompute = precomputeSize > 2048
                ? new float[precomputeSize]
                : stackalloc float[precomputeSize];

            int x = 0;
            int y = 0;

            int i = 0;
            int j = 0;
            for (i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
            {
                ref Vertex vertex = ref vertices[i];
                if (vertex.type == VertexType.Line)
                {
                    var pos0 = new Vector2(vertex.X, vertex.Y) * scale;
                    var pos1 = new Vector2(vertices[j].X, vertices[j].Y) * scale;
                    float dist = Vector2.Distance(pos0, pos1);
                    precompute[i] = (dist == 0) ? 0f : 1f / dist;
                }
                else if (vertex.type == VertexType.Curve)
                {
                    var pos2 = new Vector2(vertices[j].X, vertices[j].Y) * scale;
                    var pos1 = new Vector2(vertex.cx, vertex.cy) * scale;
                    var pos0 = new Vector2(vertex.X, vertex.Y) * scale;

                    var b = pos0 - pos1 * 2 + pos2;
                    float len2 = b.LengthSquared();
                    if (len2 != 0f)
                        precompute[i] = 1f / len2;
                    else
                        precompute[i] = 0f;
                }
                else
                {
                    precompute[i] = 0f;
                }
            }

            var pixels = new byte[glyphBox.W * glyphBox.H];
            Span<float> res = stackalloc float[3];

            var glyphBr = glyphBox.BottomRight;
            for (y = glyphBox.Y; y < glyphBr.Y; ++y)
            {
                for (x = glyphBox.X; x < glyphBr.X; ++x)
                {
                    float val = 0;
                    float min_dist = 999999f;
                    var s = new Vector2(x + 0.5f, y + 0.5f);
                    var gspace = s / scale;
                    int winding = ComputeCrossingsX(gspace, vertices);
                    for (i = 0; i < num_verts; ++i)
                    {
                        var pos0 = new Vector2(vertices[i].X, vertices[i].Y) * scale;
                        float dist2 = Vector2.DistanceSquared(pos0, s);
                        if (dist2 < (min_dist * min_dist))
                            min_dist = MathF.Sqrt(dist2);

                        if (vertices[i].type == VertexType.Line)
                        {
                            var pos1 = new Vector2(vertices[i - 1].X, vertices[i - 1].Y) * scale;
                            float dist = Math.Abs(
                                (pos1.X - pos0.X) * (pos0.Y - s.Y) -
                                (pos1.Y - pos0.Y) * (pos0.X - s.X)) * precompute[i];

                            if (dist < min_dist)
                            {
                                var d = pos1 - pos0;
                                var p = pos0 - s;
                                float t = -Vector2.Dot(p, d) / d.LengthSquared();
                                if ((t >= 0f) && (t <= 1f))
                                    min_dist = dist;
                            }
                        }
                        else if (vertices[i].type == VertexType.Curve)
                        {
                            var pos2 = new Vector2(vertices[i - 1].X, vertices[i - 1].Y) * scale;
                            var pos1 = new Vector2(vertices[i].X, vertices[i].Y) * scale;

                            float box_x0 = (pos0.X < pos1.X ? pos0.X : pos1.X) < pos2.X
                                ? (pos0.X < pos1.X ? pos0.X : pos1.X)
                                : pos2.X;

                            float box_y0 = (pos0.Y < pos1.Y ? pos0.Y : pos1.Y) < pos2.Y
                                ? (pos0.Y < pos1.Y ? pos0.Y : pos1.Y)
                                : pos2.Y;

                            float box_x1 = (pos0.X < pos1.X ? pos1.X : pos0.X) < pos2.X
                                ? pos2.X
                                : (pos0.X < pos1.X ? pos1.X : pos0.X);

                            float box_y1 = (pos0.Y < pos1.Y ? pos1.Y : pos0.Y) < pos2.Y
                                ? pos2.Y
                                : (pos0.Y < pos1.Y ? pos1.Y : pos0.Y);

                            if (s.X > (box_x0 - min_dist) &&
                                s.X < (box_x1 + min_dist) &&
                                s.Y > (box_y0 - min_dist) &&
                                s.Y < (box_y1 + min_dist))
                            {
                                int num = 0;
                                var a = pos1 - pos0;
                                var b = pos0 - pos1 * 2 + pos2;
                                var m = pos0 - s;
                                var p = Vector2.Zero;
                                float t = 0;
                                float it = 0;
                                float a_inv = precompute[i];

                                if (a_inv == 0)
                                {
                                    float fa = 3 * Vector2.Dot(a, b);
                                    float fb = 2 * a.LengthSquared() + Vector2.Dot(m, b);
                                    float fc = Vector2.Dot(m, a);
                                    if (fa == 0)
                                    {
                                        if (fb != 0)
                                            res[num++] = -fc / fb;
                                    }
                                    else
                                    {
                                        float discriminant = fb * fb - 4 * fa * fc;
                                        if (discriminant < 0)
                                            num = 0;
                                        else
                                        {
                                            float root = MathF.Sqrt(discriminant);
                                            res[0] = (-fb - root) / (2 * fa);
                                            res[1] = (-fb + root) / (2 * fa);
                                            num = 2;
                                        }
                                    }
                                }
                                else
                                {
                                    float fb = 3 * Vector2.Dot(a, b) * a_inv;
                                    float fc = 2 * a.LengthSquared() + Vector2.Dot(m, b) * a_inv;
                                    float fd = Vector2.Dot(m, a) * a_inv;
                                    num = SolveCubic(fb, fc, fd, res);
                                }

                                if ((num >= 1) && (res[0] >= 0f) && (res[0] <= 1f))
                                {
                                    t = res[0];
                                    it = 1f - t;
                                    p.X = it * it * pos0.X + 2 * t * it * pos1.X + t * t * pos2.X;
                                    p.Y = it * it * pos0.Y + 2 * t * it * pos1.Y + t * t * pos2.Y;
                                    dist2 = Vector2.DistanceSquared(p, s);

                                    if (dist2 < (min_dist * min_dist))
                                        min_dist = MathF.Sqrt(dist2);
                                }

                                if ((num >= 2) && (res[1] >= 0f) && (res[1] <= 1f))
                                {
                                    t = res[1];
                                    it = 1f - t;
                                    p.X = it * it * pos0.X + 2 * t * it * pos1.X + t * t * pos2.X;
                                    p.Y = it * it * pos0.Y + 2 * t * it * pos1.Y + t * t * pos2.Y;
                                    dist2 = Vector2.DistanceSquared(p, s);

                                    if (dist2 < (min_dist * min_dist))
                                        min_dist = MathF.Sqrt(dist2);
                                }

                                if ((num >= 3) && (res[2] >= 0f) && (res[2] <= 1f))
                                {
                                    t = res[2];
                                    it = 1f - t;
                                    p.X = it * it * pos0.X + 2 * t * it * pos1.X + t * t * pos2.X;
                                    p.Y = it * it * pos0.Y + 2 * t * it * pos1.Y + t * t * pos2.Y;
                                    dist2 = Vector2.DistanceSquared(p, s);

                                    if (dist2 < (min_dist * min_dist))
                                        min_dist = MathF.Sqrt(dist2);
                                }
                            }
                        }
                    }

                    if (winding == 0)
                        min_dist = -min_dist;
                    val = onEdgeValue + pixelDistScale * min_dist;
                    if (val < 0)
                        val = 0f;
                    else if (val > 255)
                        val = 255;
                    pixels[(y - glyphBox.Y) * glyphBox.W + (x - glyphBox.X)] = (byte)val;
                }
            }

            return pixels;
        }

        public static byte[]? GetCodepointSDF(
            FontInfo info, Vector2 scale, int codepoint, int padding,
            byte onEdgeValue, float pixelDistScale,
            out int width, out int height, out IntPoint offset)
        {
            int glyph = FindGlyphIndex(info, codepoint);
            return GetGlyphSDF(
                info, scale, glyph, padding, onEdgeValue, pixelDistScale,
                out width, out height, out offset);
        }
    }
}
