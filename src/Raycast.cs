using OpenTK.Mathematics;

namespace terrainBench;

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
        var startCell = (Vector2i)start;
        var uvOffset = start - startCell;
        if (dir == Vector2.Zero) {
            yield return (startCell, uvOffset.Yx);
            yield break;
        }
        
        var dirSign = new Vector2i(dir.X > 0 ? 1 : -1, dir.Y > 0 ? 1 : -1);
        var tileOffset = new Vector2i(dir.X > 0 ? 1 : 0, dir.Y > 0 ? 1 : 0);
        var tileUVOffset = new Vector2i(dir.X < 0 ? 1 : 0, dir.Y < 0 ? 1 : 0);
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
    }
}