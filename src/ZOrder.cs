// Created Jul. 15 2026, partially copied from:
// https://github.com/Torphedo/BOTWTerrain/blob/trunk/util.h
// @author Torphedo
using OperationResult;
using static OperationResult.Helpers;
namespace terrainBench;

public static class ZOrder {
    public static UInt16 Interleave8To16(byte x, byte y) {
        UInt16 result = 0;

        for (UInt16 i = 0; i < 8; i+= 2) {
            UInt16 low = (UInt16)(x & 1);
            result |= (UInt16)(low << i);
            x >>= 1; // Cut off bottom bit
        }
        for (UInt16 i = 1; i < 8; i+= 2) {
            UInt16 low = (UInt16)(y & 1);
            result |= (UInt16)(low << i);
            y >>= 1; // Cut off bottom bit
        }

        return result;
    }

    public static void Deinterleave16To8(UInt16 idx, out byte x, out byte y) {
        x = 0;
        y = 0;
        for (UInt16 i = 0; i < 16; i+= 2) {
            y <<= 1;
            y |= (byte)(idx >> 15);
            idx <<= 1;

            x <<= 1;
            x |= (byte)(idx >> 15);
            idx <<= 1;
        }
    }

    public static byte LocalIdx(UInt16 idx) {
        return (byte)(idx & 0b11);
    }

    public static Result<UInt16, ErrorStack> IndexFromFilename(string filename) {
        bool validExt = filename.EndsWith(".hght") || filename.EndsWith(".mate")
            || filename.EndsWith(".water.extm") || filename.EndsWith(".grass.extm");
        if (!validExt) {
            return Err(new ErrorStack($"Invalid extension: {filename}"));
        }
        if (!filename.StartsWith("5")) {
            return Err(new ErrorStack($"Invalid filename (does not start with '5'): {filename}"));
        }
        if (filename.EndsWith(".extm")) {
            filename = filename.Replace(".extm", "");
        }

        filename = Path.GetFileNameWithoutExtension(filename);
        filename = filename.Remove(0, 2);
        UInt32 res = Convert.ToUInt32(filename, 16);
        if (res > 0xFFFF) {
            return Err(new ErrorStack($"Invalid filename (gave out of bounds Z-order index {res}): {filename}"));
        }

        return (UInt16)res;
    }

    public static IEnumerable<UInt16> IterInSquareRange(UInt16 idx, byte radius) {
        // These have alternating 0 and 1 bits to isolate just the X or Y bits.
        // The actual values will be inflated when spaced out every other bit
        // like this, but relative comparisons (< / >) are still valid.
        const UInt16 xMask = 0x5555, yMask = 0xAAAA;

        // Calculate the indices of the corners of the square area
        Deinterleave16To8(idx, out var xCenter, out var yCenter);
        byte xMin = (byte)Math.Max(0x00, (Int16)xCenter - radius);
        byte yMin = (byte)Math.Max(0x00, (Int16)yCenter - radius);
        byte xMax = (byte)Math.Min(0xFF, (Int16)xCenter + radius);
        byte yMax = (byte)Math.Min(0xFF, (Int16)yCenter + radius);

        UInt16 idxMin = Interleave8To16(xMin, yMin);
        UInt16 idxMax = Interleave8To16(xMax, yMax);
        for (UInt16 i = idxMin; i <= idxMax; i++) {
            if ((i & xMask) < (idxMin & xMask) || (i & xMask) > (idxMax & xMask)) {
                continue; // Outside of X range
            }
            if ((i & yMask) < (idxMin & yMask) || (i & yMask) > (idxMax & yMask)) {
                continue; // Outside of Y range
            }
            yield return i;
        }
    }
}
