using OpenTK.Mathematics;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench;
using static TerrainCoords;

public static class Raycast {
    /// <summary>
    /// Iterate over all points along a line in a 2D grid.
    /// Or in other terminology, find all cells hit by a 2D raycast.
    /// </summary>
    /// <param name="start">The position to start the ray</param>
    /// <param name="dir">The ray's direction vector</param>
    /// <param name="maxDist">The distance at which to stop iterating</param>
    /// <returns>The grid position, and the exact collision point along the edge of the cell</returns>
    public static IEnumerable<(Vector2i cell, Vector2 uv)> Iterate2DLine(Vector2 start, Vector2 dir, float maxDist)
    {
        var z = Profiler.BeginZone("Iterate2DLine");
        // Algorithm adapted from John Amanatides & Andrew Woo, via Joel Schumacher:
        // https://joelschumacher.de/posts/ray-casting-in-2d-grids
        // http://www.cse.yorku.ca/~amana/research/grid.pdf
        
        var startCell = (Vector2i)start;
        var uvOffset = start - startCell;
        if (dir == Vector2.Zero) {
            yield return (startCell, uvOffset.Yx);
            z.Dispose();
            yield break;
        }
        
        var dirSign = new Vector2i(dir.X > 0 ? 1 : -1, dir.Y > 0 ? 1 : -1);
        var tileOffset = new Vector2i(dir.X > 0 ? 1 : 0, dir.Y > 0 ? 1 : 0);
        var tile = startCell;

        float t = 0;
        var dt = (tile + tileOffset - startCell) / dir;
        while (t <= maxDist)
        {
            Vector2 uv = (t * dir * dirSign) + uvOffset;
            uv -= uv.Truncate(); // Get only the fractional part, i.e. the
                                 // offset within the tile we hit.
            yield return (tile, uv);

            if (dt.X < dt.Y) {
                tile.X += dirSign.X;
                t += dt.X;
                float dtX = dt.X;
                dt.X = dirSign.X / dir.X;
                dt.Y -= dtX;
            } else {
                tile.Y += dirSign.Y;
                t += dt.Y;
                dt.X -= dt.Y;
                dt.Y = dirSign.Y / dir.Y;
            }
        }
        z.Dispose();
    }

    public static Result<PixelGrid8Pos, ErrorStack> RaycastTerrain(Cache.Cache cache, TileGrid8Pos startPos, Vector3 dir, float rangeTiles)
    {
        var source = new Vector2(startPos.x, startPos.z);
        foreach (var (cell, uv) in Iterate2DLine(source, dir.Xz, rangeTiles))
        {
            // Bounds check
            bool oobHigh = (cell.X >= ZOrder.GRID_SIZE || cell.Y >= ZOrder.GRID_SIZE);
            bool oobLow = (cell.X < 0 || cell.Y < 0);
            if  (oobHigh || oobLow) {
                break;
            }
                
            var idx = ZOrder.Interleave8To16((byte)cell.X, (byte)cell.Y);
            var tileRes = cache.GetHeightmapTile(8, idx, false);
            if (tileRes.IsErr()) {
                continue; // There's no tile to hit here
            }
            var tile = tileRes.Unwrap();
            var startPixel = (Vector2i)(uv * new Vector2(255));
            foreach (var (pixel, subpixel) in Raycast.Iterate2DLine(startPixel, dir.Xz, Single.PositiveInfinity))
            {
                bool oobHighPixel = (pixel.X >= ZOrder.GRID_SIZE || pixel.Y >= ZOrder.GRID_SIZE);
                bool oobLowPixel = (pixel.X < 0 || pixel.Y < 0);
                if (oobHighPixel || oobLowPixel) {
                    break;
                }

                var tp = new TileGrid8Pos(new Vector3(cell.X, 0, cell.Y));
                PixelGrid8Pos pp = new(new Vector3(pixel.X, 0, pixel.Y));
                pp += tp;
                
                var pixelDist = pp - startPos;
                pixelDist.y = 0;
                var worldDist = (WorldPos)pixelDist;
                float t = ((Vector3)worldDist).Length / Vector3.Dot(Vector3.Normalize(worldDist), dir);
                float rayHeight = startPos.y + dir.Y * t;
                
                int linearIdx = pixel.X + pixel.Y * ZOrder.GRID_SIZE;
                var normalizedHeight = (tile[linearIdx] / (float)0xFFFF) * 16;
                pp.y = normalizedHeight;
                if (normalizedHeight >= rayHeight) {
                    return pp; // Ray has gone under the terrain, it's a hit
                }
            }
        }
        
        return Err(new ErrorStack("No ray collision found."));
    }
}