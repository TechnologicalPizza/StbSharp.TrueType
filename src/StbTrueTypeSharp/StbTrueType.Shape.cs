
namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
	internal
#endif
    unsafe partial class StbTrueType
    {
        public static void SetVertex(TTVertex* v, byte type, int x, int y, int cx, int cy)
        {
            v->type = type;
            v->x = (short)x;
            v->y = (short)y;
            v->cx = (short)cx;
            v->cy = (short)cy;
        }

        public static int CloseShape(
            TTVertex* vertices, int num_vertices, int was_off, int start_off,
            int sx, int sy, int scx, int scy, int cx, int cy)
        {
            if (start_off != 0)
            {
                if (was_off != 0)
                    SetVertex(&vertices[num_vertices++], STBTT_vcurve, (cx + scx) >> 1, (cy + scy) >> 1, cx, cy);

                SetVertex(&vertices[num_vertices++], STBTT_vcurve, sx, sy, scx, scy);
            }
            else
            {
                if (was_off != 0)
                    SetVertex(&vertices[num_vertices++], STBTT_vcurve, sx, sy, cx, cy);
                else
                    SetVertex(&vertices[num_vertices++], STBTT_vline, sx, sy, 0, 0);
            }

            return num_vertices;
        }

        public static void TrackVertex(TTCharStringContext* c, int x, int y)
        {
            if ((x > c->max.x) || (c->started == 0))
                c->max.x = x;
            if ((y > c->max.y) || (c->started == 0))
                c->max.y = y;
            if ((x < c->min.x) || (c->started == 0))
                c->min.x = x;
            if ((y < c->min.y) || (c->started == 0))
                c->min.y = y;
            c->started = 1;
        }

        public static void CsContextV(
            TTCharStringContext* c, byte type, int x, int y, int cx, int cy, int cx1, int cy1)
        {
            if (c->bounds != 0)
            {
                TrackVertex(c, x, y);
                if (type == STBTT_vcubic)
                {
                    TrackVertex(c, cx, cy);
                    TrackVertex(c, cx1, cy1);
                }
            }
            else
            {
                SetVertex(&c->pvertices[c->num_vertices], type, x, y, cx, cy);
                c->pvertices[c->num_vertices].cx1 = (short)cx1;
                c->pvertices[c->num_vertices].cy1 = (short)cy1;
            }

            c->num_vertices++;
        }

        public static void CsContext_CloseShape(TTCharStringContext* ctx)
        {
            if ((ctx->firstPos.x != ctx->pos.x) || (ctx->firstPos.y != ctx->pos.y))
                CsContextV(ctx, STBTT_vline, (int)ctx->firstPos.x, (int)ctx->firstPos.y, 0, 0, 0, 0);
        }

        public static void CsContext_RMoveTo(TTCharStringContext* ctx, float dx, float dy)
        {
            CsContext_CloseShape(ctx);
            ctx->firstPos.x = ctx->pos.x = ctx->pos.x + dx;
            ctx->firstPos.y = ctx->pos.y = ctx->pos.y + dy;
            CsContextV(ctx, STBTT_vmove, (int)ctx->pos.x, (int)ctx->pos.y, 0, 0, 0, 0);
        }

        public static void CsContext_RLineTo(TTCharStringContext* ctx, float dx, float dy)
        {
            ctx->pos.x += dx;
            ctx->pos.y += dy;
            CsContextV(ctx, STBTT_vline, (int)ctx->pos.x, (int)ctx->pos.y, 0, 0, 0, 0);
        }

        public static void CsContext_RCCurveTo(
            TTCharStringContext* ctx, float dx1, float dy1, float dx2, float dy2, float dx3, float dy3)
        {
            float cx1 = ctx->pos.x + dx1;
            float cy1 = ctx->pos.y + dy1;
            float cx2 = cx1 + dx2;
            float cy2 = cy1 + dy2;
            ctx->pos.x = cx2 + dx3;
            ctx->pos.y = cy2 + dy3;
            
            CsContextV(
                ctx, STBTT_vcubic, (int)ctx->pos.x, (int)ctx->pos.y,
                (int)cx1, (int)cy1, (int)cx2, (int)cy2);
        }

        public static void FreeShape(TTVertex* v)
        {
            CRuntime.Free(v);
        }
    }
}
