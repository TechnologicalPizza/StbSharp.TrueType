using System;
using System.Runtime.InteropServices;

namespace StbSharp
{
    public class StbRectPack
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RPContext
        {
            public int width;
            public int height;
            public int x;
            public int y;
            public int bottom_y;

            public void Init(int pw, int ph)
            {
                width = pw;
                height = ph;
                x = 0;
                y = 0;
                bottom_y = 0;
            }

            public void PackRects(Span<RPRect> rects)
            {
                int i;
                for (i = 0; i < rects.Length; ++i)
                {
                    if ((x + rects[i].w) > width)
                    {
                        x = 0;
                        y = bottom_y;
                    }

                    if ((y + rects[i].h) > height)
                        break;

                    rects[i].x = x;
                    rects[i].y = y;
                    rects[i].was_packed = true;

                    x += rects[i].w;
                    if ((y + rects[i].h) > bottom_y)
                        bottom_y = y + rects[i].h;
                }

                for (; i < rects.Length; ++i)
                    rects[i].was_packed = false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RPRect
        {
            public int x;
            public int y;
            public int w;
            public int h;

            public int id;
            public bool was_packed;
        }
    }
}
