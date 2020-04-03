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
        public static int CloseShape(
            Span<Vertex> vertices, int num_vertices, int was_off, int start_off,
            int sx, int sy, int scx, int scy, int cx, int cy)
        {
            if (start_off != 0)
            {
                if (was_off != 0)
                    vertices[num_vertices++].Set(
                        VertexType.Curve, (cx + scx) >> 1, (cy + scy) >> 1, cx, cy);

                vertices[num_vertices++].Set(VertexType.Curve, sx, sy, scx, scy);
            }
            else
            {
                if (was_off != 0)
                    vertices[num_vertices++].Set(VertexType.Curve, sx, sy, cx, cy);
                else
                    vertices[num_vertices++].Set(VertexType.Line, sx, sy, 0, 0);
            }

            return num_vertices;
        }

        public static void TrackVertex(ref CharStringContext c, int x, int y)
        {
            if (c.started == 0)
            {
                c.max.X = x;
                c.max.Y = y;
                c.min.X = x;
                c.min.Y = y;
            }
            else
            {
                if (x > c.max.X)
                    c.max.X = x;
                if (y > c.max.Y)
                    c.max.Y = y;

                if (x < c.min.X)
                    c.min.X = x;
                if (y < c.min.Y)
                    c.min.Y = y;
            }
            c.started = 1;
        }

        public static void CsContextV(
            ref CharStringContext c, VertexType type,
            int x, int y, int cx, int cy, int cx1, int cy1)
        {
            if (c.bounds != 0)
            {
                TrackVertex(ref c, x, y);
                if (type == VertexType.Cubic)
                {
                    TrackVertex(ref c, cx, cy);
                    TrackVertex(ref c, cx1, cy1);
                }
            }
            else
            {
                c.pvertices[c.num_vertices].Set(type, x, y, cx, cy);
                c.pvertices[c.num_vertices].cx1 = (short)cx1;
                c.pvertices[c.num_vertices].cy1 = (short)cy1;
            }

            c.num_vertices++;
        }

        public static void CsContext_CloseShape(ref CharStringContext ctx)
        {
            if ((ctx.firstPos.X != ctx.pos.X) || (ctx.firstPos.Y != ctx.pos.Y))
                CsContextV(
                    ref ctx, VertexType.Line,
                    (int)ctx.firstPos.X, (int)ctx.firstPos.Y, 0, 0, 0, 0);
        }

        public static void CsContext_RMoveTo(ref CharStringContext ctx, float dx, float dy)
        {
            CsContext_CloseShape(ref ctx);
            ctx.firstPos.X = ctx.pos.X += dx;
            ctx.firstPos.Y = ctx.pos.Y += dy;
            CsContextV(
                ref ctx, VertexType.Move, (int)ctx.pos.X, (int)ctx.pos.Y, 0, 0, 0, 0);
        }

        public static void CsContext_RLineTo(ref CharStringContext ctx, float dx, float dy)
        {
            ctx.pos.X += dx;
            ctx.pos.Y += dy;
            CsContextV(
                ref ctx, VertexType.Line, (int)ctx.pos.X, (int)ctx.pos.Y, 0, 0, 0, 0);
        }

        public static void CsContext_RCCurveTo(
            ref CharStringContext ctx,
            float dx1, float dy1, float dx2, float dy2, float dx3, float dy3)
        {
            float cx1 = ctx.pos.X + dx1;
            float cy1 = ctx.pos.Y + dy1;
            float cx2 = cx1 + dx2;
            float cy2 = cy1 + dy2;
            ctx.pos.X = cx2 + dx3;
            ctx.pos.Y = cy2 + dy3;

            CsContextV(
                ref ctx, VertexType.Cubic,
                (int)ctx.pos.X, (int)ctx.pos.Y, (int)cx1, (int)cy1, (int)cx2, (int)cy2);
        }

        public static void FreeShape(Vertex* v)
        {
            CRuntime.Free(v);
        }
    }
}
