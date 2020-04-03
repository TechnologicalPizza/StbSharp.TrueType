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
        public static int GetGlyphShapeTT(FontInfo info, int glyph_index, out Vertex[] pvertices)
        {
            int g = GetGlyphOffset(info, glyph_index);
            if (g < 0)
            {
                pvertices = null;
                return 0;
            }

            Vertex[] vertices = null;
            int num_vertices = 0;
            var data = info.data.Span;
            short numberOfContours = ReadInt16(data.Slice(g));
            if (numberOfContours > 0)
            {
                var endPtsOfContours = data.Slice(g + 10);
                int ins = ReadUInt16(data.Slice(g + 10 + numberOfContours * 2));
                int n = 1 + ReadUInt16(endPtsOfContours.Slice(numberOfContours * 2 - 2));
                int m = n + 2 * numberOfContours;
                vertices = new Vertex[m];
                
                byte flags = 0;
                int i = 0;
                int j = 0;
                int was_off = 0;
                int start_off = 0;
                int x = 0;
                int y = 0;
                int cx = 0;
                int cy = 0;
                int sx = 0;
                int sy = 0;
                int scx = 0;
                int scy = 0;
                int next_move = 0;
                int flagcount = 0;
                int off = m - n;
                var offVertices = vertices.AsSpan(off);
                var points = data.Slice(g + 10 + numberOfContours * 2 + 2 + ins);

                for (i = 0; i < n; ++i)
                {
                    if (flagcount == 0)
                    {
                        flags = points[0];
                        points = points.Slice(1);

                        if ((flags & 8) != 0)
                        {
                            flagcount = points[0];
                            points = points.Slice(1);
                        }
                    }
                    else
                        --flagcount;

                    offVertices[i].type = (VertexType)flags;
                }

                x = 0;
                for (i = 0; i < n; ++i)
                {
                    flags = (byte)offVertices[i].type;
                    if ((flags & 2) != 0)
                    {
                        short dx = points[0];
                        points = points.Slice(1);
                        x += (flags & 16) != 0 ? dx : -dx;
                    }
                    else
                    {
                        if ((flags & 16) == 0)
                        {
                            x += (short)(points[0] * 256 + points[1]);
                            points = points.Slice(2);
                        }
                    }

                    offVertices[i].X = (short)x;
                }

                y = 0;
                for (i = 0; i < n; ++i)
                {
                    flags = (byte)offVertices[i].type;
                    if ((flags & 4) != 0)
                    {
                        short dy = points[0];
                        points = points.Slice(1);
                        y += (flags & 32) != 0 ? dy : -dy;
                    }
                    else
                    {
                        if ((flags & 32) == 0)
                        {
                            y += (short)(points[0] * 256 + points[1]);
                            points = points.Slice(2);
                        }
                    }

                    offVertices[i].Y = (short)y;
                }

                num_vertices = 0;
                sx = sy = cx = cy = scx = scy = 0;
                for (i = 0; i < n; ++i)
                {
                    flags = (byte)offVertices[i].type;
                    x = offVertices[i].X;
                    y = offVertices[i].Y;
                    if (next_move == i)
                    {
                        if (i != 0)
                            num_vertices = CloseShape(
                                vertices, num_vertices, was_off, start_off,
                                sx, sy, scx, scy, cx, cy);

                        start_off = (flags & 1) != 0 ? 0 : 1;
                        if (start_off != 0)
                        {
                            scx = x;
                            scy = y;
                            if (((int)offVertices[i + 1].type & 1) == 0)
                            {
                                sx = (x + offVertices[i + 1].X) >> 1;
                                sy = (y + offVertices[i + 1].Y) >> 1;
                            }
                            else
                            {
                                sx = offVertices[i + 1].X;
                                sy = offVertices[i + 1].Y;
                                ++i;
                            }
                        }
                        else
                        {
                            sx = x;
                            sy = y;
                        }

                        vertices[num_vertices++].Set(VertexType.Move, sx, sy, 0, 0);
                        was_off = 0;
                        next_move = 1 + ReadUInt16(endPtsOfContours.Slice(j * 2));
                        ++j;
                    }
                    else
                    {
                        if ((flags & 1) == 0)
                        {
                            if (was_off != 0)
                                vertices[num_vertices++].Set(
                                    VertexType.Curve, (cx + x) >> 1, (cy + y) >> 1, cx, cy);

                            cx = x;
                            cy = y;
                            was_off = 1;
                        }
                        else
                        {
                            if (was_off != 0)
                                vertices[num_vertices++].Set(VertexType.Curve, x, y, cx, cy);
                            else
                                vertices[num_vertices++].Set(VertexType.Line, x, y, 0, 0);
                            was_off = 0;
                        }
                    }
                }

                num_vertices = CloseShape(
                    vertices, num_vertices, was_off, start_off, sx, sy, scx, scy, cx, cy);
            }
            else if (numberOfContours < 0)
            {
                int more = 1;
                var comp = data.Slice(g + 10);
                num_vertices = 0;
                vertices = null;
                float* matrix = stackalloc float[6];

                while (more != 0)
                {
                    int i = 0;
                    matrix[0] = 1;
                    matrix[1] = 0f;
                    matrix[2] = 0f;
                    matrix[3] = 1;
                    matrix[4] = 0f;
                    matrix[5] = 0f;

                    float m = 0;
                    float n = 0;
                    ushort flags = (ushort)ReadInt16(comp);
                    comp = comp.Slice(2);
                    ushort gidx = (ushort)ReadInt16(comp);
                    comp = comp.Slice(2);

                    if ((flags & 2) != 0)
                    {
                        if ((flags & 1) != 0)
                        {
                            matrix[4] = ReadInt16(comp);
                            comp = comp.Slice(2);
                            matrix[5] = ReadInt16(comp);
                            comp = comp.Slice(2);
                        }
                        else
                        {
                            matrix[4] = (sbyte)comp[0];
                            comp = comp.Slice(1);
                            matrix[5] = (sbyte)comp[0];
                            comp = comp.Slice(1);
                        }
                    }

                    if ((flags & (1 << 3)) != 0)
                    {
                        matrix[0] = matrix[3] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                        matrix[1] = matrix[2] = 0f;
                    }
                    else if ((flags & (1 << 6)) != 0)
                    {
                        matrix[0] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                        matrix[1] = matrix[2] = 0f;
                        matrix[3] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                    }
                    else if ((flags & (1 << 7)) != 0)
                    {
                        matrix[0] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                        matrix[1] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                        matrix[2] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                        matrix[3] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                    }

                    m = MathF.Sqrt(matrix[0] * matrix[0] + matrix[1] * matrix[1]);
                    n = MathF.Sqrt(matrix[2] * matrix[2] + matrix[3] * matrix[3]);
                    int comp_num_verts = GetGlyphShape(info, gidx, out Vertex[] comp_verts);
                    var compVerts = comp_verts.AsSpan(0, comp_num_verts);
                    
                    if (compVerts.Length > 0)
                    {
                        // TODO: optimize this?
                        for (i = 0; i < compVerts.Length; ++i)
                        {
                            ref Vertex v = ref compVerts[i];
                            
                            short x = v.X;
                            short y = v.Y;
                            v.X = (short)(m * (matrix[0] * x + matrix[2] * y + matrix[4]));
                            v.Y = (short)(n * (matrix[1] * x + matrix[3] * y + matrix[5]));

                            x = v.cx;
                            y = v.cy;
                            v.cx = (short)(m * (matrix[0] * x + matrix[2] * y + matrix[4]));
                            v.cy = (short)(n * (matrix[1] * x + matrix[3] * y + matrix[5]));
                        }

                        var tmp = new Vertex[num_vertices + compVerts.Length];

                        vertices.AsSpan(0, num_vertices).CopyTo(tmp);
                        compVerts.CopyTo(tmp.AsSpan(num_vertices));

                        vertices = tmp;
                        num_vertices += compVerts.Length;
                    }

                    more = flags & (1 << 5);
                }
            }

            pvertices = vertices;
            return num_vertices;
        }

    }
}
