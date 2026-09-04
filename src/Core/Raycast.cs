using OpenTK.Mathematics;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench.Core;
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
    public static IEnumerable<(Vector2i cell, Vector2 uv, float t)> Iterate2DLine(Vector2 start, Vector2 dir, float maxDist)
    {
        // Algorithm adapted from John Amanatides & Andrew Woo, via Joel Schumacher:
        // https://joelschumacher.de/posts/ray-casting-in-2d-grids
        // http://www.cse.yorku.ca/~amana/research/grid.pdf
        
        var startCell = (Vector2i)start;
        Vector2 uvOffset = start - startCell;
        if (dir == Vector2.Zero) {
            yield return (startCell, uvOffset.Yx, 0f);
            yield break;
        }
        
        var dirSign = new Vector2i(dir.X > 0 ? 1 : -1, dir.Y > 0 ? 1 : -1);
        var tileOffset = new Vector2i(dir.X > 0 ? 1 : 0, dir.Y > 0 ? 1 : 0);
        var tile = start;

        float t = 0;
        var dt = (tile + tileOffset - startCell) / dir;
        while (t <= maxDist)
        {
            Vector2 uv = tile;
            var tileOut = (Vector2i)uv.Truncate();
            uv -= tileOut; // Get only the fractional part, i.e. the
                              // offset within the tile we hit.
            yield return (tileOut, uv, t);

            dt.X = Math.Abs(dt.X);
            dt.Y = Math.Abs(dt.Y);

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
    }

    public static Result<PixelGrid8Pos, ErrorStack> RaycastTerrain(Cache cache, TileGrid8Pos startPos, Vector3 dir, float rangeTiles)
    {
        var z = Profiler.BeginZone("RaycastTerrain");
        var source = new Vector2(startPos.x, startPos.z);
        WorldPos worldDir = new(dir);
        Vector2 tileDir = worldDir.ToTileDir().xz.Normalized();
        Vector2 pixelDir = new PixelGrid8Pos(new Vector3(tileDir.X, 0, tileDir.Y)).xz.Normalized();
        foreach (var (cell, uvInitial, tTile) in Iterate2DLine(source, tileDir, rangeTiles))
        {
            // Bounds check
            bool oobHigh = (cell.X >= ZOrder.GRID_SIZE || cell.Y >= ZOrder.GRID_SIZE);
            bool oobLow = (cell.X < 0 || cell.Y < 0);
            if  (oobHigh || oobLow) {
                break;
            }

            // TODO: Get coverage info from caller to find the best tile in 1 attempt.
            int lod = 0;
            var uv = uvInitial;
            var idx = ZOrder.Interleave8To16((byte)cell.X, (byte)cell.Y);
            Result<ushort[], ErrorStack> tileRes = Err(new ErrorStack(""));
            for (int i = ZOrder.MAX_LOD; i >= 0; i--) {
                tileRes = cache.GetHeightmapTile(i, idx, false);
                if (tileRes.IsOk()) {
                    lod = i;
                    break; // Found one!
                }

                // Convert coordinates to the lower LOD level
                uv /= 2;
                if ((idx & 0b01) != 0) {
                    uv.X += 0.5f;
                }
                if ((idx & 0b10) != 0) {
                    uv.Y += 0.5f;
                }
                
                idx >>= 2;
            }
            int lodDiff = ZOrder.MAX_LOD - lod;

            if (tileRes.IsErr()) {
                continue; // Didn't find anything...
            }
            
            var tile = tileRes.Unwrap();
            var tp = new TileGrid8Pos(new Vector3(cell.X, 0, cell.Y));
            var startPixel = (Vector2i)(uv * new Vector2(255));
            foreach (var (pixel, subpixel, tPixel) in Iterate2DLine(startPixel, pixelDir, Single.PositiveInfinity))
            {
                bool oobHighPixel = (pixel.X >= ZOrder.GRID_SIZE || pixel.Y >= ZOrder.GRID_SIZE);
                bool oobLowPixel = (pixel.X < 0 || pixel.Y < 0);
                if (oobHighPixel || oobLowPixel) {
                    break;
                }
                int linearIdx = pixel.X + pixel.Y * ZOrder.GRID_SIZE;
                var normalizedHeight = (tile[linearIdx] / (float)0xFFFF) * WORLD_HEIGHT;

                Vector2 tileOffset = (tTile + (tPixel / 255)) * tileDir;
                TileGrid8Pos hitTilePos = startPos.xz + tileOffset;
                WorldPos wp =  hitTilePos;
                
                var worldDist = ((Vector3)(wp - startPos)).Xz; // Make sure only 2D is considered
                float t = worldDist.Length;
                WorldPos hitPos = new((WorldPos)startPos + (Vector3)worldDir * t);

                if (hitPos.y < 0f || t < 0f) {
                    continue; // Something's gone terribly wrong, ignore this
                }
                
                if (normalizedHeight >= hitPos.y) {
                    // Ray has gone under the terrain, it's a hit
                    hitPos.y = normalizedHeight; // Snap to terrain
                    z.Dispose();
                    return (PixelGrid8Pos)(TileGrid8Pos)hitPos;
                }
            }
        }
        
        z.Dispose();
        return Err(new ErrorStack("No ray collision found."));
    }

    public struct Ray(Vector3 origin, Vector3 dir) {
        public Vector3 Origin = origin, Dir = dir;
    }
    
    /// <summary>
    /// Calculate the world space direction that the mouse position points in.
    /// i.e. if you drew a line from the pixel the mouse is on straight "into"
    /// the screen, this gives that vector in world space.
    /// </summary>
    /// <param name="mousePos">The mouse position in pixel coordinates</param>
    /// <param name="projT">Your projection matrix</param>
    /// <param name="viewT">Your view matrix</param>
    /// <param name="fbSize">The size (in pixels) of the framebuffer</param>
    /// <param name="fbStart">The position (in pixels) of the framebuffer's origin.
    /// If your framebuffer takes up the whole screen or your mouse coordinates are
    /// already relative to its origin, this is just (0, 0).</param>
    /// <returns>The normalized world space direction vector, and the ray's origin</returns>
    public static Ray ScreenToRay(Vector2 mousePos, Matrix4 projT, Matrix4 viewT, Vector2 fbSize, Vector2 fbStart, bool flipY) {
        var z = Profiler.BeginZone("ScreenToRay");
        // Map to NDC (-1 to 1)
        var mouseNDC = ((mousePos - fbStart) / fbSize) * 2 - new Vector2(1, 1);
        mouseNDC.Y *= (flipY ? -1 : 1);
        
        // The near/far planes become the -1 / 1 edges of NDC.
        Vector3 nearScreen = new(mouseNDC.X, mouseNDC.Y, -1f);
        Vector3 farScreen = new(mouseNDC.X, mouseNDC.Y, 1f);
        
        Matrix4 invProj = projT.Inverted();
        Matrix4 invView = viewT.Inverted();

        // Calculate the world coordinates of the mouse at the near & far planes
        var nearWorld = new Vector4(nearScreen, 1f) * invProj * invView;
        var farWorld = new Vector4(farScreen, 1f) * invProj * invView;
        nearWorld /= nearWorld.W;
        farWorld /= farWorld.W;

        // The direction is the vector from the near to the far plane
        var ray = new Ray(nearWorld.Xyz, (farWorld.Xyz - nearWorld.Xyz).Normalized());
        z.Dispose();
        return ray;
    }
}
