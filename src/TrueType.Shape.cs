using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace StbSharp
{
    public partial class TrueType
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct CharStringContext
        {
            public int bounds;
            public bool started;
            public Vector2 firstPos;
            public Vector2 pos;
            public IntPoint min;
            public IntPoint max;
            public Vertex[] pvertices;
            public int num_vertices;

            public void TrackVertex(int x, int y)
            {
                if (!started)
                {
                    max.X = x;
                    max.Y = y;
                    min.X = x;
                    min.Y = y;
                }
                else
                {
                    if (x > max.X)
                        max.X = x;
                    if (y > max.Y)
                        max.Y = y;

                    if (x < min.X)
                        min.X = x;
                    if (y < min.Y)
                        min.Y = y;
                }
                started = true;
            }

            public void Vertex(
                VertexType type, int x, int y, int cx, int cy, int cx1, int cy1)
            {
                if (bounds != 0)
                {
                    TrackVertex(x, y);
                    if (type == VertexType.Cubic)
                    {
                        TrackVertex(cx, cy);
                        TrackVertex(cx1, cy1);
                    }
                }
                else
                {
                    ref Vertex vertex = ref pvertices[num_vertices];
                    vertex.Set(type, x, y, cx, cy);
                    vertex.C1.X = cx1;
                    vertex.C1.Y = cy1;
                }

                num_vertices++;
            }

            public void CloseShape()
            {
                if ((firstPos.X != pos.X) || (firstPos.Y != pos.Y))
                    Vertex(VertexType.Line, (int)firstPos.X, (int)firstPos.Y, 0, 0, 0, 0);
            }

            public void RMoveTo(float dx, float dy)
            {
                CloseShape();
                firstPos.X = pos.X += dx;
                firstPos.Y = pos.Y += dy;
                Vertex(VertexType.Move, (int)pos.X, (int)pos.Y, 0, 0, 0, 0);
            }

            public void RLineTo(float dx, float dy)
            {
                pos.X += dx;
                pos.Y += dy;
                Vertex(VertexType.Line, (int)pos.X, (int)pos.Y, 0, 0, 0, 0);
            }

            public void RCCurveTo(
                float dx1, float dy1, float dx2, float dy2, float dx3, float dy3)
            {
                float cx1 = pos.X + dx1;
                float cy1 = pos.Y + dy1;
                float cx2 = cx1 + dx2;
                float cy2 = cy1 + dy2;
                pos.X = cx2 + dx3;
                pos.Y = cy2 + dy3;

                Vertex(
                    VertexType.Cubic, (int)pos.X, (int)pos.Y, (int)cx1, (int)cy1, (int)cx2, (int)cy2);
            }
        }

        public static int CloseShape(
            Span<Vertex> vertices, ref int numVertices, int wasOff, int startOff,
            float sx, float sy, float scx, float scy, float cx, float cy)
        {
            if (startOff != 0)
            {
                if (wasOff != 0)
                {
                    vertices[numVertices++].Set(
                        VertexType.Curve, (cx + scx) / 2f, (cy + scy) / 2f, cx, cy);
                }
                vertices[numVertices++].Set(VertexType.Curve, sx, sy, scx, scy);
            }
            else
            {
                if (wasOff != 0)
                    vertices[numVertices++].Set(VertexType.Curve, sx, sy, cx, cy);
                else
                    vertices[numVertices++].Set(VertexType.Line, sx, sy, 0, 0);
            }

            return numVertices;
        }
    }
}
