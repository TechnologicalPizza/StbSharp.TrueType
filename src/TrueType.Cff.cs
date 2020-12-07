using System;

namespace StbSharp
{
    public partial class TrueType
    {
        public static Buffer CffGetIndex(ref Buffer b)
        {
            int start = b.Cursor;
            int count = (int)b.Get(2);
            if (count != 0)
            {
                int offsize = b.GetByte();
                b.Skip(offsize * count);
                b.Skip((int)(b.Get(offsize) - 1));
            }
            return b.Slice(start, b.Cursor - start);
        }

        [CLSCompliant(false)]
        public static uint CffInt(ref Buffer b)
        {
            int b0 = b.GetByte();
            if ((b0 >= 32) && (b0 <= 246))
                return (uint)(b0 - 139);
            else if ((b0 >= 247) && (b0 <= 250))
                return (uint)((b0 - 247) * 256 + b.GetByte() + 108);
            else if ((b0 >= 251) && (b0 <= 254))
                return (uint)(-(b0 - 251) * 256 - b.GetByte() - 108);
            else if (b0 == 28)
                return b.Get(2);
            else if (b0 == 29)
                return b.Get(4);
            return 0;
        }

        public static void CffSkipOperand(ref Buffer b)
        {
            int b0 = b.PeekByte();
            if (b0 == 30)
            {
                b.Skip(1);
                while (b.Cursor < b.Size)
                {
                    int v = b.GetByte();
                    if (((v & 0xF) == 0xF) || ((v >> 4) == 0xF))
                        break;
                }
            }
            else
            {
                CffInt(ref b);
            }
        }

        public static int CffIndexCount(ref Buffer b)
        {
            b.Seek(0);
            return (int)b.Get(2);
        }

        public static Buffer CffIndexGet(Buffer b, int i)
        {
            b.Seek(0);
            int count = (int)b.Get(2);
            int offsize = b.GetByte();
            b.Skip(i * offsize);
            int start = (int)b.Get(offsize);
            int end = (int)b.Get(offsize);
            return b.Slice(2 + (count + 1) * offsize + start, end - start);
        }

        public static Buffer DictGet(ref Buffer b, int key)
        {
            b.Seek(0);
            while (b.Cursor < b.Size)
            {
                int start = b.Cursor;
                while (b.PeekByte() >= 28)
                    CffSkipOperand(ref b);

                int end = b.Cursor;
                int op = b.GetByte();
                if (op == 12)
                    op = b.GetByte() | 0x100;
                if (op == key)
                    return b.Slice(start, end - start);
            }
            return b.Slice(0, 0);
        }

        [CLSCompliant(false)]
        public static void DictGetInts(
            ref Buffer b, int key, Span<uint> dst)
        {
            var operands = DictGet(ref b, key);
            for (int i = 0; (i < dst.Length) && (operands.Cursor < operands.Size); i++)
                dst[i] = CffInt(ref operands);
        }
    }
}
