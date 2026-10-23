using System.Diagnostics;

namespace terrainBench;
public static class UnsafeUtil {
    public static unsafe ref T ReadUnsafe<T>(ReadOnlySpan<byte> span, long offset)
        where T : unmanaged
    {
        Debug.Assert(offset >= 0 && (offset + sizeof(T)) < span.Length);
        fixed (byte* ptr = span) {
            return ref *(T*)(ptr + offset);
        }
    }

    public static unsafe ReadOnlySpan<T> ROSpanSegment<T>(ReadOnlySpan<byte> span, long offset, int count)
        where T : unmanaged
    {
        Debug.Assert(offset >= 0 && count >= 0);
        var size = sizeof(T) * count;
        var endPos = offset + size;
        if (endPos > span.Length) {
            Console.WriteLine("Span of size {0} @ offset {1} would be out of bounds (size = {2})", size, offset, span.Length);
            Debug.Assert(false);
        }
        fixed (byte* ptr = span) {
            var pos = (T*)(ptr + offset);
            return new ReadOnlySpan<T>(pos, count);
        }
    }
    
    public static unsafe Span<T> SpanSegment<T>(Span<byte> span, long offset, int count)
        where T : unmanaged
    {
        Debug.Assert(offset >= 0 && count >= 0);
        var size = sizeof(T) * count;
        var endPos = offset + size;
        if (endPos > span.Length) {
            Console.WriteLine("Span of size {0} @ offset {1} would be out of bounds (size = {2})", size, offset, span.Length);
            Debug.Assert(false);
        }
        fixed (byte* ptr = span) {
            var pos = (T*)(ptr + offset);
            return new Span<T>(pos, count);
        }
    }

}
