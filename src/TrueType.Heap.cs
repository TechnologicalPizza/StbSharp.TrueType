using System;
using System.Runtime.InteropServices;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class TrueType
    {
        // TODO: replace this with some managed impl

        [StructLayout(LayoutKind.Sequential)]
        public struct HeapChunk
        {
            public HeapChunk* next;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Heap
        {
            public HeapChunk* head;
            public void* first_free;
            public int num_remaining_in_head_chunk;
        }

        public static void* HeapAlloc(ref Heap hh, int size)
        {
            const int maxAlloc = 64000;

            if (size > maxAlloc)
                throw new ArgumentOutOfRangeException(nameof(size));
                
            if (hh.first_free != null)
            {
                void* p = hh.first_free;
                hh.first_free = *(void**)p;
                return p;
            }
            else
            {
                if (hh.num_remaining_in_head_chunk == 0)
                {
                    int count = maxAlloc / size;
                    var c = (HeapChunk*)CRuntime.MAlloc(sizeof(HeapChunk) + size * count);
                    if (c == null)
                        return null;

                    c->next = hh.head;
                    hh.head = c;
                    hh.num_remaining_in_head_chunk = count;
                }

                --hh.num_remaining_in_head_chunk;
                return (byte*)(hh.head + 1) + size * hh.num_remaining_in_head_chunk;
            }
        }

        public static void HeapFree(Heap* hh, void* p)
        {
            *(void**)p = hh->first_free;
            hh->first_free = p;
        }

        public static void HeapCleanup(Heap* hh)
        {
            HeapChunk* c = hh->head;
            while (c != null)
            {
                HeapChunk* n = c->next;
                CRuntime.Free(c);
                c = n;
            }
        }
    }
}
