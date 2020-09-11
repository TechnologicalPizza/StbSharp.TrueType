using System;
using System.Diagnostics;
using System.Numerics;

namespace StbSharp
{
    public partial class TrueType
    {
        public static int GetGlyphShapeT2(
            FontInfo info, int glyphIndex, out Vertex[]? pvertices)
        {
            var output_ctx = new CharStringContext();
            var count_ctx = new CharStringContext();
            count_ctx.bounds = 1;

            if (RunCharString(info, glyphIndex, ref count_ctx) != 0)
            {
                pvertices = new Vertex[count_ctx.num_vertices];
                output_ctx.pvertices = pvertices;

                if (RunCharString(info, glyphIndex, ref output_ctx) != 0)
                    return output_ctx.num_vertices;
            }

            pvertices = null;
            return 0;
        }

        public static int RunCharString(
            FontInfo info, int glyphIndex, ref CharStringContext c)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            int in_header = 1;
            int maskbits = 0;
            int subr_stack_height = 0;
            int sp = 0;
            int v = 0;
            int i = 0;
            int b0 = 0;
            int has_subrs = 0;
            int clear_stack = 0;
            float f = 0;
            var subrs = info.subrs;

            Span<float> s = stackalloc float[48];
            var subr_stack = new Buffer[10];

            var b = CffIndexGet(info.charstrings, glyphIndex);
            while (b.Cursor < b.Size)
            {
                i = 0;
                clear_stack = 1;
                b0 = b.GetByte();
                switch (b0)
                {
                    case 0x13:
                    case 0x14:
                        if (in_header != 0)
                            maskbits += sp / 2;
                        in_header = 0;
                        b.Skip((maskbits + 7) / 8);
                        break;

                    case 0x01:
                    case 0x03:
                    case 0x12:
                    case 0x17:
                        maskbits += sp / 2;
                        break;

                    case 0x15:
                        in_header = 0;
                        if (sp < 2)
                            return 0;
                        c.RMoveTo(s[sp - 2], s[sp - 1]);
                        break;

                    case 0x04:
                        in_header = 0;
                        if (sp < 1)
                            return 0;
                        c.RMoveTo(0f, s[sp - 1]);
                        break;

                    case 0x16:
                        in_header = 0;
                        if (sp < 1)
                            return 0;
                        c.RMoveTo(s[sp - 1], 0f);
                        break;

                    case 0x05:
                        if (sp < 2)
                            return 0;
                        for (; (i + 1) < sp; i += 2)
                            c.RLineTo(s[i], s[i + 1]);
                        break;

                    case 0x07:
                    case 0x06:
                        if (sp < 1)
                            return 0;

                        int goto_vlineto = b0 == 0x07 ? 1 : 0;
                        for (; ; )
                        {
                            if (goto_vlineto == 0)
                            {
                                if (i >= sp)
                                    break;
                                c.RLineTo(s[i], 0);
                                i++;
                            }
                            goto_vlineto = 0;

                            if (i >= sp)
                                break;
                            c.RLineTo(0f, s[i]);
                            i++;
                        }
                        break;

                    case 0x1F:
                    case 0x1E:
                        if (sp < 4)
                            return 0;

                        int goto_hvcurveto = b0 == 0x1F ? 1 : 0;
                        for (; ; )
                        {
                            if (goto_hvcurveto == 0)
                            {
                                if ((i + 3) >= sp)
                                    break;

                                c.RCCurveTo(
                                    0f, s[i],
                                    s[i + 1], s[i + 2],
                                    s[i + 3], ((sp - i) == 5) ? s[i + 4] : 0f);
                                i += 4;
                            }

                            goto_hvcurveto = 0;
                            if ((i + 3) >= sp)
                                break;
                            c.RCCurveTo(
                                s[i], 0f,
                                s[i + 1], s[i + 2],
                                ((sp - i) == 5) ? s[i + 4] : 0f, s[i + 3]);
                            i += 4;
                        }
                        break;

                    case 0x08:
                        if (sp < 6)
                            return 0;

                        for (; (i + 5) < sp; i += 6)
                        {
                            c.RCCurveTo(
                                s[i], s[i + 1],
                                s[i + 2], s[i + 3],
                                s[i + 4], s[i + 5]);
                        }
                        break;

                    case 0x18:
                        if (sp < 8)
                            return 0;

                        for (; (i + 5) < (sp - 2); i += 6)
                        {
                            c.RCCurveTo(
                                s[i], s[i + 1],
                                s[i + 2], s[i + 3],
                                s[i + 4], s[i + 5]);
                        }
                        if ((i + 1) >= sp)
                            return 0;
                        c.RLineTo(s[i], s[i + 1]);
                        break;

                    case 0x19:
                        if (sp < 8)
                            return 0;

                        for (; (i + 1) < (sp - 6); i += 2)
                            c.RLineTo(s[i], s[i + 1]);

                        if ((i + 5) >= sp)
                            return 0;

                        c.RCCurveTo(
                            s[i], s[i + 1],
                            s[i + 2], s[i + 3],
                            s[i + 4], s[i + 5]);
                        break;

                    case 0x1A:
                    case 0x1B:
                        if (sp < 4)
                            return 0;

                        f = 0f;
                        if ((sp & 1) != 0)
                        {
                            f = s[i];
                            i++;
                        }

                        for (; (i + 3) < sp; i += 4)
                        {
                            if (b0 == 0x1B)
                            {
                                c.RCCurveTo(
                                     s[i], f,
                                     s[i + 1], s[i + 2],
                                     s[i + 3], 0);
                            }
                            else
                            {
                                c.RCCurveTo(
                                    f, s[i],
                                    s[i + 1], s[i + 2],
                                    0, s[i + 3]);
                            }
                            f = 0;
                        }
                        break;

                    case 0x0A:
                    case 0x1D:
                        if (b0 == 0x0A)
                        {
                            if (has_subrs == 0)
                            {
                                if (info.fdselect.Size != 0)
                                    subrs = CidGetGlyphSubRs(info, glyphIndex);
                                has_subrs = 1;
                            }
                        }

                        if (sp < 1)
                            return 0;
                        v = (int)s[--sp];

                        if (subr_stack_height >= 10)
                            return 0;

                        subr_stack[subr_stack_height++] = b;
                        b = GetSubr(b0 == 0x0A ? subrs : info.gsubrs, v);
                        if (b.Size == 0)
                            return 0;
                        b.Cursor = 0;
                        clear_stack = 0;
                        break;

                    case 0x0B:
                        if (subr_stack_height <= 0)
                            return 0;
                        b = subr_stack[--subr_stack_height];
                        clear_stack = 0;
                        break;

                    case 0x0E:
                        c.CloseShape();
                        return 1;

                    case 0x0C:
                    {
                        float dx1 = 0;
                        float dx2 = 0;
                        float dx3 = 0;
                        float dx4 = 0;
                        float dx5 = 0;
                        float dx6 = 0;
                        float dy1 = 0;
                        float dy2 = 0;
                        float dy3 = 0;
                        float dy4 = 0;
                        float dy5 = 0;
                        float dy6 = 0;
                        float dx = 0;
                        float dy = 0;

                        int b1 = b.GetByte();
                        switch (b1)
                        {
                            case 0x22:
                                if (sp < 7)
                                    return 0;
                                dx1 = s[0];
                                dx2 = s[1];
                                dy2 = s[2];
                                dx3 = s[3];
                                dx4 = s[4];
                                dx5 = s[5];
                                dx6 = s[6];
                                c.RCCurveTo(dx1, 0f, dx2, dy2, dx3, 0f);
                                c.RCCurveTo(dx4, 0f, dx5, -dy2, dx6, 0f);
                                break;

                            case 0x23:
                                if (sp < 13)
                                    return 0;
                                dx1 = s[0];
                                dy1 = s[1];
                                dx2 = s[2];
                                dy2 = s[3];
                                dx3 = s[4];
                                dy3 = s[5];
                                dx4 = s[6];
                                dy4 = s[7];
                                dx5 = s[8];
                                dy5 = s[9];
                                dx6 = s[10];
                                dy6 = s[11];
                                c.RCCurveTo(dx1, dy1, dx2, dy2, dx3, dy3);
                                c.RCCurveTo(dx4, dy4, dx5, dy5, dx6, dy6);
                                break;

                            case 0x24:
                                if (sp < 9)
                                    return 0;
                                dx1 = s[0];
                                dy1 = s[1];
                                dx2 = s[2];
                                dy2 = s[3];
                                dx3 = s[4];
                                dx4 = s[5];
                                dx5 = s[6];
                                dy5 = s[7];
                                dx6 = s[8];
                                c.RCCurveTo(dx1, dy1, dx2, dy2, dx3, 0f);
                                c.RCCurveTo(dx4, 0f, dx5, dy5, dx6, -(dy1 + dy2 + dy5));
                                break;

                            case 0x25:
                                if (sp < 11)
                                    return 0;
                                dx1 = s[0];
                                dy1 = s[1];
                                dx2 = s[2];
                                dy2 = s[3];
                                dx3 = s[4];
                                dy3 = s[5];
                                dx4 = s[6];
                                dy4 = s[7];
                                dx5 = s[8];
                                dy5 = s[9];
                                dx6 = dy6 = s[10];
                                dx = dx1 + dx2 + dx3 + dx4 + dx5;
                                dy = dy1 + dy2 + dy3 + dy4 + dy5;
                                if (Math.Abs(dx) > Math.Abs(dy))
                                    dy6 = -dy;
                                else
                                    dx6 = -dx;
                                c.RCCurveTo(dx1, dy1, dx2, dy2, dx3, dy3);
                                c.RCCurveTo(dx4, dy4, dx5, dy5, dx6, dy6);
                                break;

                            default:
                                return 0;
                        }
                    }
                    break;

                    default:
                        if ((b0 != 255) && (b0 != 28) && ((b0 < 32) || (b0 > 254)))
                            return 0;

                        if (b0 == 255)
                        {
                            f = (float)(int)b.Get(4) / 0x10000;
                        }
                        else
                        {
                            b.Skip(-1);
                            f = (short)CffInt(ref b);
                        }

                        if (sp >= 48)
                            return 0;

                        s[sp++] = f;
                        clear_stack = 0;
                        break;
                }

                if (clear_stack != 0)
                    sp = 0;
            }

            return 0;
        }

        public static Buffer GetSubr(Buffer idx, int n)
        {
            int count = CffIndexCount(ref idx);
            int bias = 107;

            if (count >= 33900)
                bias = 32768;
            else if (count >= 1240)
                bias = 1131;
            n += bias;

            if ((n < 0) || (n >= count))
                return Buffer.Empty;
            return CffIndexGet(idx, n);
        }
    }
}
