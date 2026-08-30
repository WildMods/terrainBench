using System.Numerics;
using OpenTK.Mathematics;

namespace terrainBench.Cache;

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
    where T : IAdditionOperators<T, T, T>, IShiftOperators<T, int, T>
    {
        int lowSize = size / 2; // Size of our tile in the low-detail image
        int lowLinearPos = lowPos.X + (lowPos.Y * size); // Linear index of the low-detail starting pixel
        for (int i = 0; i < size; i += 2) {
            // Note the size per row when calculating the index is full
            // resolution, but the size of the target row data is halved.
            int lowLinearTarget = lowLinearPos + (i / 2) * size;
            var target  = new ArraySegment<T>(low, lowLinearTarget, lowSize);
            
            var source1 = new ArraySegment<T>(high, (i + 0) * size, size);
            var source2 = new ArraySegment<T>(high, (i + 1) * size, size);

            for (int x = 0; x < size; x += 2) {
                // Collect 4x4 area of pixels from our 2 rows
                T pixel = source1[x] + source1[x + 1] + source2[x] + source2[x + 1];
                // Divide by 4 via shifting to get the average.
                // I used an explicit shift here because I'm not sure if the
                // compiler does enough inlining of generic operators to turn
                // this into a bitshift otherwise. -- torf
                pixel >>= 2;
                
                target[x / 2] = pixel; // Write to low-detail tile
            }
        }
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
}