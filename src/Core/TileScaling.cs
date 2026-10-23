using System.Numerics;
using OpenTK.Mathematics;
using terrainBench.Core.LodComponents;

namespace terrainBench.Core;

public static class TileScaling {
    /// <summary>
    /// Downscale a tile and write it to a portion of a higher-detail tile.
    /// Both tiles are assumed to be the same power-of-2 resolution, and the
    /// high-detail pixels are downscaled by 4x (i.e. every 4 high-detail pixels
    /// map to 1 low-detail pixel, and the resolution is halved on both axes).
    /// </summary>
    /// <param name="high">The high-detail tile to downscale</param>
    /// <param name="low">The low-detail tile to write to</param>
    /// <param name="lowPos">The pixel in the low-detail tile where the
    ///                      downscaled tile should begin</param>
    /// <param name="size">The square size in pixels of both tiles</param>
    /// <typeparam name="T">The pixel type</typeparam>
    public static void DownscaleTile<T>(T[] high, T[] low, Vector2i lowPos, int size)
    where T : IAdditionOperators<T, T, T>, IShiftOperators<T, int, T>, IBitwiseOperators<T, ushort, T>
    {
        var z = Profiler.BeginZone("DownscaleTile");
        int lowSize = size / 2; // Size of our tile in the low-detail image
        int lowLinearPos = lowPos.X + (lowPos.Y * size); // Linear index of the low-detail starting pixel
        for (int i = 0; i < size; i += 2) {
            // Note the size per row when calculating the index is full
            // resolution, but the size of the target row data is halved.
            int lowLinearTarget = lowLinearPos + (i / 2) * size;
            var target  = new ArraySegment<T>(low, lowLinearTarget, lowSize);
            
            var source1 = new ArraySegment<T>(high, (i + 0) * size, size);
            var source2 = new ArraySegment<T>(high, (i + 1) * size, size);

            // Collect 4x4 area of pixels from our 2 rows
            for (int x = 0; x < size; x += 2) {
                // We divide by 4 via shifting to get the average. The shift is
                // done before the addition to avoid integer overflows.
                
                // The reason I explicitly shift here is that I'm not sure if
                // the compiler does enough inlining of generic operators to
                // turn a divide into a shift otherwise. -- torf

                // If we just sum & divide by 4, overflows are very likely to
                // cause incorrect answers. To avoid this, we can pre-divide all
                // our values.
                
                T roughAvg = (source1[x] >> 2) + (source1[x + 1] >> 2) +
                          (source2[x] >> 2) + (source2[x + 1] >> 2);
                // Pre-dividing like this loses the precision of the low 2 bits
                // in the average. To get it back, we average them separately
                // in the normal way.
                T preciseAvg = ((source1[x] & 0b11) + (source1[x + 1] & 0b11) +
                               (source2[x] & 0b11) + (source2[x + 1] & 0b11)) >> 2;
                T pixel = roughAvg + preciseAvg;
                
                target[x / 2] = pixel; // Write to low-detail tile
            }
        }
        z.Dispose();
    }
    
    /// <summary>
    /// Same as DownscaleTile(), but specifically for Material data.
    /// </summary>
    /// <param name="high">The high-detail tile to downscale</param>
    /// <param name="low">The low-detail tile to write to</param>
    /// <param name="lowPos">The pixel in the low-detail tile where the
    ///                      downscaled tile should begin</param>
    /// <param name="size">The square size in pixels of both tiles</param>
    public static void DownscaleMaterialTile(Material[] high, Material[] low, Vector2i lowPos, int size)
    {
        var z = Profiler.BeginZone("DownscaleTile");
        int lowSize = size / 2; // Size of our tile in the low-detail image
        int lowLinearPos = lowPos.X + (lowPos.Y * size); // Linear index of the low-detail starting pixel
        for (int y = 0; y < size; y += 2) {
            // Note the size per row when calculating the index is full
            // resolution, but the size of the target row data is halved.
            int lowLinearTarget = lowLinearPos + (y / 2) * size;
            var target  = new ArraySegment<Material>(low, lowLinearTarget, lowSize);
            
            var source1 = new ArraySegment<Material>(high, (y + 0) * size, size);
            var source2 = new ArraySegment<Material>(high, (y + 1) * size, size);

            // Collect 4x4 area of pixels from our 2 rows
            for (int x = 0; x < size; x += 2) {
                // The downscaling strategy is to find which 2 materials have
                // the highest weight across the pixels, then adjust those
                // cumulative weights to be an 8-bit ratio.
                
                
                // The 4 pixels
                Material p1 = source1[x], p2 = source1[x + 1];
                Material p3 = source2[x], p4 = source2[x + 1];
                
                // First we build a table of all material IDs and their weights
                byte[] foundMats = {
                    p1.Material0, p1.Material1,
                    p2.Material0, p2.Material1,
                    p3.Material0, p3.Material1,
                    p4.Material0, p4.Material1,
                };
                ushort[] weights = {
                    (byte)(0xFF - p1.BlendWeight), p1.BlendWeight,
                    (byte)(0xFF - p2.BlendWeight), p2.BlendWeight,
                    (byte)(0xFF - p3.BlendWeight), p3.BlendWeight,
                    (byte)(0xFF - p4.BlendWeight), p4.BlendWeight,
                };
                
                // Insertion sort by material ID, so that weights to be added
                // together are next to each other
                for (int i = 1; i < foundMats.Length; i++) {
                    int j = i - 1;
                    while (j >= 0 && foundMats[j] > foundMats[j + 1]) {
                        // Swap in both arrays, to keep them in sync
                        (weights[j], weights[j + 1]) = (weights[j + 1], weights[j]);
                        (foundMats[j], foundMats[j + 1]) = (foundMats[j + 1], foundMats[j]);
                        j--;
                    }
                }

                // Sum up weights that are attached to the same material ID,
                // storing the sum in the first slot.
                byte curID = foundMats[0];
                for (int i = 1, targetIdx = 0; i < weights.Length; i++) {
                    if (foundMats[i] == curID) {
                        weights[targetIdx] += weights[i];
                        weights[i] = 0;
                    } else {
                        targetIdx = i;
                        curID = foundMats[targetIdx];
                    }
                }
                
                // Insertion sort again, this time by weight.
                // This is maybe a bit overkill, we just need the top 2.
                for (int i = 1; i < weights.Length; i++) {
                    int j = i - 1;
                    // Note the comparison operator is the opposite of last time,
                    // so this orders it from largest to smallest weight.
                    while (j >= 0 && weights[j] < weights[j + 1]) {
                        // Swap in both arrays, to keep them in sync
                        (weights[j], weights[j + 1]) = (weights[j + 1], weights[j]);
                        (foundMats[j], foundMats[j + 1]) = (foundMats[j + 1], foundMats[j]);
                        j--;
                    }
                }
                
                // Pick the top 2 materials, and make their weights add up to
                // 0xFF while keeping the same ratio.
                var sum = weights[0] + weights[1];
                float ratio = 0xFF / (float)sum;
                var weight = Math.Round(weights[0] * ratio);
                
                // Note we're just inheriting Unk3 from a random pixel. This is
                // not really correct, but hard to know how to blend w/o knowing
                // what it does.
                
                // I remember seeing Unk3 set to non-zero on steep cliffs, so
                // maybe it can be used as a hint to the downscaler to increase
                // the weight of the dominant material (i.e, prevent the material
                // at the top of the cliff from bleeding down)
                // -- torf
                Material pixel = new(foundMats[1], foundMats[0], (byte)weight, p1.Unk3);
                target[x / 2] = pixel; // Write to low-detail tile
            }
        }
        z.Dispose();
    }

    /// <summary>
    /// Find out the position in pixels of a high-detail tile if it were
    /// super-imposed onto the lower-detail tile 1 level below.
    /// </summary>
    /// <param name="highTilePos">The position of the high resolution tile
    ///                           (in tiles of its grid level, not pixels)</param>
    /// <param name="size">The square power-of-2 resolution of both tiles</param>
    public static Vector2i GetLowDetailPos(Vector2i highTilePos, int size) {
        // Even coords start at offset 0, odd ones start halfway. Just isolate the low bit
        var p = new Vector2i(highTilePos.X & 1, highTilePos.Y & 1);
        return p * (size / 2);
    }
    public static Vector2i GetLowDetailPos(ushort idx, int size) {
        ZOrder.Deinterleave16To8(idx, out var x, out var y);
        Vector2i tilePos = new(x, y);
        // Even coords start at offset 0, odd ones start halfway. Just isolate the low bit
        var p = new Vector2i(tilePos.X & 1, tilePos.Y & 1);
        return p * (size / 2);
    }
}
