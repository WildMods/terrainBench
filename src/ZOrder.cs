// Created Jul. 15 2026, partially copied from:
// https://github.com/Torphedo/BOTWTerrain/blob/trunk/util.h
// @author Torphedo
using OperationResult;
using static OperationResult.Helpers;
namespace terrainBench;

/// <summary>
/// Utilities for Z-order curve indices used in BOTW terrain tiles.
/// See https://handwiki.org/wiki/Z-order_curve
/// Internally we refer to a Z-value as a "Z-index", which is kind of non-standard.
/// </summary>
public static class ZOrder {
    public const int MAX_LOD = 8;
    
    /// <summary>
    /// Convert a 2D 8-bit coordinate to a 16-bit 1D Z-index
    /// (interleaves the bits of the 2 values)
    /// </summary>
    public static UInt16 Interleave8To16(byte x, byte y) {
        UInt16 result = 0;

        for (UInt16 i = 0; i < 16; i+= 2) {
            UInt16 low = (UInt16)(x & 1);
            result |= (UInt16)(low << i);
            x >>= 1; // Cut off bottom bit
        }
        for (UInt16 i = 1; i < 16; i+= 2) {
            UInt16 low = (UInt16)(y & 1);
            result |= (UInt16)(low << i);
            y >>= 1; // Cut off bottom bit
        }

        return result;
    }

    /// <summary>
    /// Convert a 1D 16-bit Z-index to a 2D 8-bit coordinate
    /// (deinterleaves the bits of the Z-index)
    /// </summary>
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

    /// <summary>
    /// Convert a Z-index to be relative to its SSTERA base index
    /// (equivalent to modulo 4)
    /// </summary>
    public static byte LocalIdx(UInt16 idx) {
        return (byte)(idx & 0b11);
    }

    /// <summary>
    /// Round an index down to the next multiple of 4, giving the ID of the SSTERA
    /// file a tile should be in.
    /// </summary>
    public static UInt16 RoundToSSTERAIdx(UInt16 idx) {
        return (UInt16)((idx >> 2) << 2);
    }

    /// <summary>
    /// Parse the Z-index from a tile's filename (expects a tile, not its containing SSTERA)
    /// </summary>
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

    /// <summary>
    /// Generate a tile's filename without an extension.
    /// e.g. if idx = 0xC0A0 and lod = 8, returns "580000C0A0".
    /// </summary>
    public static string BuildFilename(UInt16 idx, int lod) {
        if (lod > MAX_LOD) {
            return "";
        }

        string hexPart = idx.ToString("X8"); // Hexidecimal padded with 0s to 8 digits

        // e.g. "580000C0A0" (just needs an extension added)
        return $"5{lod}{hexPart}";
    }

    /// <summary>
    /// Equivalent to the other BuildFilename but adds ".extension" (just read the implementation)
    /// </summary>
    public static string BuildFilename(UInt16 idx, int lod, string extension) {
        // e.g. "580000C0A0.hght"
        return $"{BuildFilename(idx, lod)}.{extension}";
    }

    /// <summary>
    /// Iterate over the Z-indices within a square region around a point
    /// </summary>
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
    
    /// <summary>
    /// Same as IterInSquareRange(), but returns packed index values that include the LOD
    /// </summary>
    public static IEnumerable<Int32> IterInSquareRangeAtLod(UInt16 idx, byte radius, byte lod) {
        var iter = IterInSquareRange(idx, radius);
        foreach (var i in iter) {
            yield return PackIndex(i, lod);
        }
    }

    /// <summary>
    /// Pack a Z-index and LOD value into a single value
    /// </summary>
    public static Int32 PackIndex(UInt16 idx, byte lod) {
        Int32 val = ((Int32)lod) << 16;
        val |= idx;
        return val;
    }
    
    /// <summary>
    /// Unpack a Z-index and LOD value from a packed value
    /// </summary>
    public static void UnpackIndex(Int32 val, out UInt16 idx, out byte lod) {
        lod = (byte)((val >> 16) & 0xFF);
        idx = (UInt16)(val & 0xFFFF);
    }

    /// <summary>
    /// Take the 2D Manhattan distance between two Z-indices
    /// </summary>
    public static UInt16 ManhattanDist(UInt16 a, UInt16 b) {
        Deinterleave16To8(a, out var aX, out var aY);
        Deinterleave16To8(b, out var bX, out var bY);
        return (UInt16)(Math.Abs(bX - aX) + Math.Abs(bY - aY));
    }
}
