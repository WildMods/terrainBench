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
}
