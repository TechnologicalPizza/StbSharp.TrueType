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

            int num_verts = GetGlyphShape(info, glyph, out Vertex[]? vertexArray);
            Span<Vertex> vertices = vertexArray.AsSpan(0, num_verts);

            int precomputeSize = num_verts * sizeof(float);
            Span<float> precompute = precomputeSize > 4096
                ? new float[precomputeSize]
                : stackalloc float[precomputeSize];

            for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
            {
                ref readonly Vertex vertex = ref vertices[i];
                if (vertex.type == VertexType.Line)
                {
                    ref readonly Vertex jvertex = ref vertices[j];
                    Vector2 v0 = new Vector2(vertex.X, vertex.Y) * scale;
                    Vector2 v1 = new Vector2(jvertex.X, jvertex.Y) * scale;
                    float dist = Vector2.Distance(v0, v1);
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

            Span<float> res = stackalloc float[3];
            precompute = precompute.Slice(0, vertices.Length);

            IntPoint glyphBr = glyphBox.BottomRight;
            for (int y = glyphBox.Y; y < glyphBr.Y; ++y)
            {
                for (int x = glyphBox.X; x < glyphBr.X; ++x)
                {
                    float val = 0;
                    float min_dist = 999999f;
                    Vector2 s = new Vector2(x + 0.5f, y + 0.5f);
                    Vector2 gspace = s / scale;
                    int winding = ComputeCrossingsX(gspace, vertices);

                    float sx = s.X;
                    float sy = s.Y;

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        ref readonly Vertex vertex = ref vertices[i];

                        Vector2 v0 = new Vector2(vertex.X, vertex.Y) * scale;
                        float x0 = v0.X;
                        float y0 = v0.Y;

                        float dist2 = Vector2.DistanceSquared(v0, s);
                        if (dist2 < (min_dist * min_dist))
                            min_dist = MathF.Sqrt(dist2);

                        if (vertex.type == VertexType.Line)
                        {
                            ref readonly Vertex pvertex = ref vertices[i - 1];
                            Vector2 k = new Vector2(pvertex.X, pvertex.Y) * scale;
                            float dist = Math.Abs(
                                (k.X - x0) * (y0 - sy) - (k.Y - y0) * (x0 - sx)) * precompute[i];

                            if (dist < min_dist)
                            {
                                Vector2 d = k - v0;
                                Vector2 p = v0 - s;
                                float t = -Vector2.Dot(p, d) / Vector2.Dot(d, d);

                                if ((t >= 0f) && (t <= 1f))
                                    min_dist = dist;
                            }
                        }
                        else if (vertex.type == VertexType.Curve)
                        {
                            ref readonly Vertex pvertex = ref vertices[i - 1];
                            Vector2 v2 = new Vector2(pvertex.X, pvertex.Y) * scale;
                            Vector2 v1 = new Vector2(vertex.cx, vertex.cy) * scale;
                            float x2 = pvertex.X * scale.X;
                            float y2 = pvertex.Y * scale.Y;
                            float x1 = vertex.cx * scale.X;
                            float y1 = vertex.cy * scale.Y;

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
                                Vector2 va = v1 - v0;
                                Vector2 vb = v0 - 2 * v1 + v2;
                                Vector2 vm = v0 - s;
                                float t = 0;
                                float it = 0;
                                float a_inv = precompute[i];

                                if (a_inv == 0)
                                {
                                    float a = 3 * Vector2.Dot(va, vb);
                                    float b = 2 * Vector2.Dot(va, va) + Vector2.Dot(vm, vb);
                                    float c = Vector2.Dot(vm, va);
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
                                            float root = MathF.Sqrt(discriminant);
                                            res[0] = (-b - root) / (2 * a);
                                            res[1] = (-b + root) / (2 * a);
                                            num = 2;
                                        }
                                    }
                                }
                                else
                                {
                                    float b = 3 * Vector2.Dot(va, vb) * a_inv;
                                    float c = (2 * Vector2.Dot(va, va) + Vector2.Dot(vm, vb)) * a_inv;
                                    float d = Vector2.Dot(vm, va) * a_inv;
                                    num = SolveCubic(b, c, d, out res[0], out res[1], out res[2]);
                                }

                                t = res[0];
                                if ((num >= 1) && (t >= 0f) && (t <= 1f))
                                {
                                    it = 1f - t;
                                    Vector2 vp;
                                    vp.X = it * it * x0 + 2 * t * it * x1 + t * t * x2;
                                    vp.Y = it * it * y0 + 2 * t * it * y1 + t * t * y2;
                                    dist2 = Vector2.DistanceSquared(vp, s);

                                    if (dist2 < (min_dist * min_dist))
                                        min_dist = MathF.Sqrt(dist2);
                                }

                                t = res[1];
                                if ((num >= 2) && (t >= 0f) && (t <= 1f))
                                {
                                    it = 1f - t;
                                    Vector2 vp;
                                    vp.X = it * it * x0 + 2 * t * it * x1 + t * t * x2;
                                    vp.Y = it * it * y0 + 2 * t * it * y1 + t * t * y2;
                                    dist2 = Vector2.DistanceSquared(vp, s);

                                    if (dist2 < (min_dist * min_dist))
                                        min_dist = MathF.Sqrt(dist2);
                                }

                                t = res[2];
                                if ((num >= 3) && (t >= 0f) && (t <= 1f))
                                {
                                    it = 1f - t;
                                    Vector2 vp;
                                    vp.X = it * it * x0 + 2 * t * it * x1 + t * t * x2;
                                    vp.Y = it * it * y0 + 2 * t * it * y1 + t * t * y2;
                                    dist2 = Vector2.DistanceSquared(vp, s);

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
