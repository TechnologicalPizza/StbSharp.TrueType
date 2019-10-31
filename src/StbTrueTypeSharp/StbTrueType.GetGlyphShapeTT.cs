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
        public static int GetGlyphShapeTT(TTFontInfo info, int glyph_index, out TTVertex* pvertices)
        {
            int g = GetGlyphOffset(info, glyph_index);
            if (g < 0)
            {
                pvertices = null;
                return 0;
            }

            TTVertex* vertices = null;
            int num_vertices = 0;
            var data = info.data.Span;
            short numberOfContours = ReadInt16(data.Slice(g));
            if (numberOfContours > 0)
            {
                byte flags = 0;
                byte flagcount = 0;
                int ins = 0;
                int i = 0;
                int j = 0;
                int m = 0;
                int n = 0;
                int next_move = 0;
                int was_off = 0;
                int off = 0;
                int start_off = 0;
                int x = 0;
                int y = 0;
                int cx = 0;
                int cy = 0;
                int sx = 0;
                int sy = 0;
                int scx = 0;
                int scy = 0;
                var endPtsOfContours = data.Slice(g + 10);
                ins = ReadUInt16(data.Slice(g + 10 + numberOfContours * 2));
                var points = data.Slice(g + 10 + numberOfContours * 2 + 2 + ins);
                n = 1 + ReadUInt16(endPtsOfContours.Slice(numberOfContours * 2 - 2));
                m = n + 2 * numberOfContours;

                vertices = (TTVertex*)CRuntime.malloc(m * sizeof(TTVertex));
                if (vertices == null)
                {
                    pvertices = null;
                    return 0;
                }

                next_move = 0;
                flagcount = 0;
                off = m - n;
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

                    vertices[off + i].type = flags;
                }

                x = 0;
                for (i = 0; i < n; ++i)
                {
                    flags = vertices[off + i].type;
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

                    vertices[off + i].x = (short)x;
                }

                y = 0;
                for (i = 0; i < n; ++i)
                {
                    flags = vertices[off + i].type;
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

                    vertices[off + i].y = (short)y;
                }

                num_vertices = 0;
                sx = sy = cx = cy = scx = scy = 0;
                for (i = 0; i < n; ++i)
                {
                    flags = vertices[off + i].type;
                    x = vertices[off + i].x;
                    y = vertices[off + i].y;
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
                            if ((vertices[off + i + 1].type & 1) == 0)
                            {
                                sx = (x + vertices[off + i + 1].x) >> 1;
                                sy = (y + vertices[off + i + 1].y) >> 1;
                            }
                            else
                            {
                                sx = vertices[off + i + 1].x;
                                sy = vertices[off + i + 1].y;
                                ++i;
                            }
                        }
                        else
                        {
                            sx = x;
                            sy = y;
                        }

                        SetVertex(&vertices[num_vertices++], STBTT_vmove, sx, sy, 0, 0);
                        was_off = 0;
                        next_move = 1 + ReadUInt16(endPtsOfContours.Slice(j * 2));
                        ++j;
                    }
                    else
                    {
                        if ((flags & 1) == 0)
                        {
                            if (was_off != 0)
                                SetVertex(
                                    &vertices[num_vertices++], STBTT_vcurve, (cx + x) >> 1, (cy + y) >> 1, cx, cy);

                            cx = x;
                            cy = y;
                            was_off = 1;
                        }
                        else
                        {
                            if (was_off != 0)
                                SetVertex(&vertices[num_vertices++], STBTT_vcurve, x, y, cx, cy);
                            else
                                SetVertex(&vertices[num_vertices++], STBTT_vline, x, y, 0, 0);
                            was_off = 0;
                        }
                    }
                }

                num_vertices = CloseShape(
                    vertices, num_vertices, was_off, start_off, sx, sy, scx, scy, cx, cy);
            }
            else if (numberOfContours == (-1))
            {
                int more = 1;
                var comp = data.Slice(g + 10);
                num_vertices = 0;
                vertices = null;
                while (more != 0)
                {
                    int comp_num_verts = 0;
                    int i = 0;
                    float* mtx = stackalloc float[6];
                    mtx[0] = 1;
                    mtx[1] = 0f;
                    mtx[2] = 0f;
                    mtx[3] = 1;
                    mtx[4] = 0f;
                    mtx[5] = 0f;
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
                            mtx[4] = ReadInt16(comp);
                            comp = comp.Slice(2);
                            mtx[5] = ReadInt16(comp);
                            comp = comp.Slice(2);
                        }
                        else
                        {
                            mtx[4] = (sbyte)comp[0];
                            comp = comp.Slice(1);
                            mtx[5] = (sbyte)comp[0];
                            comp = comp.Slice(1);
                        }
                    }

                    if ((flags & (1 << 3)) != 0)
                    {
                        mtx[0] = mtx[3] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                        mtx[1] = mtx[2] = 0f;
                    }
                    else if ((flags & (1 << 6)) != 0)
                    {
                        mtx[0] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                        mtx[1] = mtx[2] = 0f;
                        mtx[3] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                    }
                    else if ((flags & (1 << 7)) != 0)
                    {
                        mtx[0] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                        mtx[1] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                        mtx[2] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                        mtx[3] = ReadInt16(comp) / 16384f;
                        comp = comp.Slice(2);
                    }

                    m = (float)Math.Sqrt(mtx[0] * mtx[0] + mtx[1] * mtx[1]);
                    n = (float)Math.Sqrt(mtx[2] * mtx[2] + mtx[3] * mtx[3]);
                    comp_num_verts = GetGlyphShape(info, gidx, out TTVertex* comp_verts);
                    if (comp_num_verts > 0)
                    {
                        for (i = 0; i < comp_num_verts; ++i)
                        {
                            TTVertex* v = &comp_verts[i];
                            short x = 0;
                            short y = 0;
                            x = v->x;
                            y = v->y;
                            v->x = (short)(m * (mtx[0] * x + mtx[2] * y + mtx[4]));
                            v->y = (short)(n * (mtx[1] * x + mtx[3] * y + mtx[5]));
                            x = v->cx;
                            y = v->cy;
                            v->cx = (short)(m * (mtx[0] * x + mtx[2] * y + mtx[4]));
                            v->cy = (short)(n * (mtx[1] * x + mtx[3] * y + mtx[5]));
                        }

                        var tmp = (TTVertex*)CRuntime.malloc(
                            (num_vertices + comp_num_verts) * sizeof(TTVertex));

                        if (tmp == null)
                        {
                            if (vertices != null)
                                FreeShape(vertices);
                            if (comp_verts != null)
                                FreeShape(comp_verts);
                            pvertices = null;
                            return 0;
                        }

                        if (num_vertices > 0)
                            CRuntime.memcpy(tmp, vertices, num_vertices * sizeof(TTVertex));
                        CRuntime.memcpy(tmp + num_vertices, comp_verts, comp_num_verts * sizeof(TTVertex));
                        if (vertices != null)
                            FreeShape(vertices);
                        vertices = tmp;
                        FreeShape(comp_verts);
                        num_vertices += comp_num_verts;
                    }

                    more = flags & (1 << 5);
                }
            }
            //else if ((numberOfContours) < 0)
            //{
            //}
            //else
            //{
            //}

            pvertices = vertices;
            return num_vertices;
        }

    }
}
