using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace StbSharp
{
    public partial class TrueType
    {
        [SkipLocalsInit]
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

            int num_verts = GetGlyphShape(info, glyph, out Vertex[]? src_verts);
            var vertices = src_verts.AsSpan(0, num_verts);

            int precomputeSize = vertices.Length * sizeof(float);
            Span<float> precompute = precomputeSize > 4096
                ? new float[precomputeSize]
                : stackalloc float[precomputeSize];

            for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
            {
                ref readonly Vertex vertex = ref vertices[i];
                if (vertex.type == VertexType.Line)
                {
                    ref readonly Vertex jvertex = ref vertices[j];
                    var pos0 = new Vector2(vertex.X, vertex.Y) * scale;
                    var pos1 = new Vector2(jvertex.X, jvertex.Y) * scale;
                    float dist = Vector2.Distance(pos0, pos1);
                    precompute[i] = (dist == 0) ? 0f : 1f / dist;
                }
                else if (vertex.type == VertexType.Curve)
                {
                    ref readonly Vertex jvertex = ref vertices[j];
                    var pos2 = new Vector2(jvertex.X, jvertex.Y) * scale;
                    var pos1 = new Vector2(vertex.cx, vertex.cy) * scale;
                    var pos0 = new Vector2(vertex.X, vertex.Y) * scale;

                    Vector2 b = pos0 - pos1 * 2 + pos2;
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

            byte[] pixels = new byte[glyphBox.W * glyphBox.H];
            float r0 = 0;
            float r1 = 0;
            float r2 = 0;

            IntPoint glyphBr = glyphBox.BottomRight;
            for (int y = glyphBox.Y; y < glyphBr.Y; ++y)
            {
                for (int x = glyphBox.X; x < glyphBr.X; ++x)
                {
                    float val = 0;
                    float min_dist = 999999f;
                    var s = new Vector2(x + 0.5f, y + 0.5f);

                    ReadOnlySpan<Vertex> vertexSlice = vertices.Slice(0, num_verts);
                    var pos0 = new Vector2(vertexSlice[0].X, vertexSlice[0].Y) * scale;
                    float dist2 = Vector2.DistanceSquared(pos0, s);
                    if (dist2 < (min_dist * min_dist))
                        min_dist = MathF.Sqrt(dist2);

                    for (int i = 1; i < vertexSlice.Length; i++)
                    {
                        ref readonly Vertex pvertex = ref vertexSlice[i - 1];
                        ref readonly Vertex vertex = ref vertexSlice[i];

                        pos0 = new Vector2(vertex.X, vertex.Y) * scale;
                        dist2 = Vector2.DistanceSquared(pos0, s);
                        if (dist2 < (min_dist * min_dist))
                            min_dist = MathF.Sqrt(dist2);

                        if (vertex.type == VertexType.Line)
                        {
                            var pos1 = new Vector2(pvertex.X, pvertex.Y) * scale;
                            float dist = Math.Abs(
                                (pos1.X - pos0.X) * (pos0.Y - s.Y) -
                                (pos1.Y - pos0.Y) * (pos0.X - s.X)) * precompute[i]; // TODO: dot product?

                            if (dist < min_dist)
                            {
                                Vector2 d = pos1 - pos0;
                                Vector2 p = pos0 - s;
                                float t = -Vector2.Dot(p, d) / d.LengthSquared();
                                if ((t >= 0f) && (t <= 1f))
                                    min_dist = dist;
                            }
                        }
                        else if (vertex.type == VertexType.Curve)
                        {
                            var pos2 = new Vector2(pvertex.X, pvertex.Y) * scale;
                            var pos1 = new Vector2(vertex.X, vertex.Y) * scale;

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
                                        {
                                            float rn = -fc / fb;
                                            if (num == 0)
                                                r0 = rn;
                                            else if (num == 1)
                                                r1 = rn;
                                            else
                                                r2 = rn;
                                            num++;
                                        }
                                    }
                                    else
                                    {
                                        float discriminant = fb * fb - 4 * fa * fc;
                                        if (discriminant < 0)
                                            num = 0;
                                        else
                                        {
                                            float root = MathF.Sqrt(discriminant);
                                            r0 = (-fb - root) / (2 * fa);
                                            r1 = (-fb + root) / (2 * fa);
                                            num = 2;
                                        }
                                    }
                                }
                                else
                                {
                                    float fb = 3 * Vector2.Dot(a, b) * a_inv;
                                    float fc = 2 * a.LengthSquared() + Vector2.Dot(m, b) * a_inv;
                                    float fd = Vector2.Dot(m, a) * a_inv;
                                    num = SolveCubic(fb, fc, fd, out r0, out r1, out r2);
                                }

                                if ((num >= 1) && (r0 >= 0f) && (r0 <= 1f))
                                {
                                    t = r0;
                                    it = 1f - t;
                                    p.X = it * it * pos0.X + 2 * t * it * pos1.X + t * t * pos2.X;
                                    p.Y = it * it * pos0.Y + 2 * t * it * pos1.Y + t * t * pos2.Y;
                                    dist2 = Vector2.DistanceSquared(p, s);

                                    if (dist2 < (min_dist * min_dist))
                                        min_dist = MathF.Sqrt(dist2);
                                }

                                if ((num >= 2) && (r1 >= 0f) && (r1 <= 1f))
                                {
                                    t = r1;
                                    it = 1f - t;
                                    p.X = it * it * pos0.X + 2 * t * it * pos1.X + t * t * pos2.X;
                                    p.Y = it * it * pos0.Y + 2 * t * it * pos1.Y + t * t * pos2.Y;
                                    dist2 = Vector2.DistanceSquared(p, s);

                                    if (dist2 < (min_dist * min_dist))
                                        min_dist = MathF.Sqrt(dist2);
                                }

                                if ((num >= 3) && (r2 >= 0f) && (r2 <= 1f))
                                {
                                    t = r2;
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

                    Vector2 gspace = s / scale;
                    int winding = ComputeCrossingsX(gspace, vertices);
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
