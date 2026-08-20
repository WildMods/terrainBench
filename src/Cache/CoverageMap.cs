using System.Diagnostics;
namespace terrainBench.Cache;

/// <summary>
/// A compact data structure to quickly find out what detail levels are available
/// at any position in the level 8 tile grid
/// </summary>
public class CoverageMap {
    public readonly byte[] map = new byte[ZOrder.GRID_SIZE * ZOrder.GRID_SIZE];

    public CoverageMap(Cache cache) {
        var zone = Profiler.BeginZone("C_CoverageMap");
        for (sbyte lvl = 8; lvl > 0; lvl--) {
            byte lvlDiff = (byte)(ZOrder.MAX_LOD - lvl);
            UInt32 drawSize = (UInt32)((1 << lvlDiff) * (1 << lvlDiff));
            int sizeofLevel = (int)((1 << lvl) * (1 << lvl));

            // Index is an int because level 8 uses the whole 16-bit range, so
            // otherwise the loop will never end
            int tilesFound = 0;
            int tilesWritten = 0;
            for (int idx = 0; idx < sizeofLevel; idx++) {
                var res = cache.GetHeightmapTile(lvl, (UInt16)idx, false);
                if (res.IsErr()) {
                    continue;
                }
                tilesFound++;

                // Find the index of this tile's starting point on the level 8 grid
                UInt16 lvl8Idx = (UInt16)(idx << (2 * lvlDiff));
                byte val = (byte)(1 << (lvl - 1));
                for (int i = 0; i < drawSize; i++) {
                    int targetPos = lvl8Idx + i;
                    ZOrder.Deinterleave16To8((UInt16)targetPos, out var x, out var y);
                    int linearIdx = ZOrder.GRID_SIZE * y + x;

                    // We have 8 bits and 9 detail levels, so exclude level 0.
                    Debug.Assert(lvl > 0);
                    // MSB = level 8, LSB = level 1.
                    map[linearIdx] |= val;
                    tilesWritten++;
                }
            }
            Console.WriteLine("Found {0}/{1} level {2} tiles, covering {3} level 8 tiles", tilesFound, sizeofLevel, lvl, tilesWritten);
        }

        zone.Dispose();
    }

    /// <summary>
    /// Find out the highest detail level available at a specific coordinate
    /// </summary>
    public byte FindBestLOD(byte x, byte y) {
        var linearIdx = ZOrder.GRID_SIZE * y + x;
        var cov = map[linearIdx];
        byte best = ZOrder.MAX_LOD;
        while ((cov & 0x80) == 0) {
            cov <<= 1;
            best--;
        }

        return best;
    }

    public byte FindBestLOD(ushort idx) {
        ZOrder.Deinterleave16To8(idx, out var x, out var y);
        return FindBestLOD(x, y);
    }

    /// <summary>
    /// Find all the highest-detail available tiles in a square region.
    /// The minimum and maximum coordinates are both inclusive.
    /// </summary>
    /// <returns>A set of packed tile IDs, to be unpacked with ZOrder.UnpackIndex().</returns>
    public HashSet<Int32> FindAllTilesInSquare(byte minX, byte minY, byte maxX, byte maxY) {
        var indices = new HashSet<Int32>();
        for (short x = minX; x <= maxX; x++) {
            for (short y = minY; y <= maxY; y++) {
                byte best = FindBestLOD((byte)x, (byte)y);
                var idx = ZOrder.Interleave8To16((byte)x, (byte)y);
                
                var lvlDiff = ZOrder.MAX_LOD - best;
                UInt16 targetIdx = (UInt16)(idx >> (2 * lvlDiff));
                indices.Add(ZOrder.PackIndex(targetIdx, best));
            }
        }

        return indices;
    }

    public HashSet<Int32> FindAllTilesInRadius(byte xCenter, byte yCenter, ushort sizeTiles) {
        byte xMin = (byte)Math.Max(0, xCenter - sizeTiles);
        byte yMin = (byte)Math.Max(0, yCenter - sizeTiles);
        byte xMax = (byte)Math.Min(0xFF, xCenter + sizeTiles);
        byte yMax = (byte)Math.Min(0xFF, yCenter + sizeTiles);
        return FindAllTilesInSquare(xMin, yMin, xMax, yMax);
    }
    
    public HashSet<Int32> FindAllTilesInManhattanRadius(byte xCenter, byte yCenter, ushort sizeTiles) {
        byte xMin = (byte)Math.Max(0, xCenter - sizeTiles);
        byte yMin = (byte)Math.Max(0, yCenter - sizeTiles);
        byte xMax = (byte)Math.Min(0xFF, xCenter + sizeTiles);
        byte yMax = (byte)Math.Min(0xFF, yCenter + sizeTiles);
        
        var indices = new HashSet<Int32>();
        for (short x = xMin; x <= xMax; x++) {
            for (short y = yMin; y <= yMax; y++) {
                var d = Math.Abs(xCenter - x) +  Math.Abs(yCenter - y);
                if (d > sizeTiles) {
                    continue;
                }
                
                byte best = FindBestLOD((byte)x, (byte)y);
                var idx = ZOrder.Interleave8To16((byte)x, (byte)y);
                
                var lvlDiff = ZOrder.MAX_LOD - best;
                UInt16 targetIdx = (UInt16)(idx >> (2 * lvlDiff));
                indices.Add(ZOrder.PackIndex(targetIdx, best));
            }
        }

        return indices;
    }
}