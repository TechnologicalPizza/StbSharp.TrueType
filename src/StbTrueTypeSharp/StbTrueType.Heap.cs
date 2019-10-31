using System.Runtime.InteropServices;

namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class StbTrueType
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TTHeapChunk
        {
            public TTHeapChunk* next;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TTHeap
        {
            public TTHeapChunk* head;
            public void* first_free;
            public int num_remaining_in_head_chunk;
        }

        public static void* HeapAlloc(TTHeap* hh, int size)
        {
            if (hh->first_free != null)
            {
                void* p = hh->first_free;
                hh->first_free = *(void**)p;
                return p;
            }
            else
            {
                if (hh->num_remaining_in_head_chunk == 0)
                {
                    int count = size < 32 ? 2000 : size < 128 ? 800 : 100;
                    var c = (TTHeapChunk*)CRuntime.malloc(sizeof(TTHeapChunk) + size * count);
                    if (c == null)
                        return null;
                    c->next = hh->head;
                    hh->head = c;
                    hh->num_remaining_in_head_chunk = count;
                }

                --hh->num_remaining_in_head_chunk;
                return (sbyte*)hh->head + sizeof(TTHeapChunk) + size * hh->num_remaining_in_head_chunk;
            }
        }

        public static void HeapFree(TTHeap* hh, void* p)
        {
            *(void**)p = hh->first_free;
            hh->first_free = p;
        }

        public static void HeapCleanup(TTHeap* hh)
        {
            TTHeapChunk* c = hh->head;
            while (c != null)
            {
                TTHeapChunk* n = c->next;
                CRuntime.free(c);
                c = n;
            }
        }
    }
}
