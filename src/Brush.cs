using System.Diagnostics;
using OpenTK.Mathematics;

namespace terrainBench;

public struct Brush() {
    public enum Shape {
        CIRCLE, SQUARE,
    }
    
    public enum FalloffFunc {
        LINEAR,         // mx + b
        INVERSE,        // (1 / mx) + b
        SQUARE,         // mx^2 + b
        INVERSE_SQUARE, // (1 / (mx^2)) + b
        ENUM_MAX,
    }
    
    public enum DistanceType {
        EUCLIDEAN, // Physically correct 2D distance
        MANHATTAN, // Approximate, causes a square-shaped falloff
    }
    
    public enum EditFunc {
        ADD, SUBTRACT, MULTIPLY, DIVIDE, OVERWRITE,
    }

    public Vector3 center = new();
    public int radius = 50;
    public Shape shape = Shape.CIRCLE;
    public bool isDistance3D = false;
    public EditFunc editFunc = EditFunc.ADD;

    public FalloffFunc func = FalloffFunc.LINEAR;
    public DistanceType falloffShape = DistanceType.EUCLIDEAN;
    
    /// <summary>
    /// The amount the brush strength decreases as it moves away from the center
    /// Implemented as the "m" to plug into the falloff function
    /// </summary>
    public float falloffStrength = -0.5f;

    /// <summary>
    /// The power of the brush before any falloff is applied.
    /// Implemented as the "b" to plug into the falloff function
    /// </summary>
    public float baseStrength = 50f;

    /// <summary>
    /// Calculates the result of a falloff function
    /// </summary>
    public static float EvalFalloff(FalloffFunc func, float m, float x, float b)
    {
        Debug.Assert((int)func < (int)FalloffFunc.ENUM_MAX);
        var result = func switch {
            FalloffFunc.LINEAR  => m * x + b,
            FalloffFunc.INVERSE => (1f / (m * x)) + b,
            FalloffFunc.SQUARE  => m * (x * x) + b,
            FalloffFunc.INVERSE_SQUARE => 1f / (m * (x * x)) + b,
        };
        return Math.Max(0, result);
    }

    /// <summary>
    /// Calculate the distance between 2 points with specific settings
    /// </summary>
    /// <param name="type">The type of distance function to use</param>
    /// <param name="is3D">Whether to take the 2D or 3D distance</param>
    /// <returns>The distance between the 2 points</returns>
    public static float EvalDistance(Vector3 a, Vector3 b, DistanceType type, bool is3D) {
        if (!is3D) {
            // Ignore height in 2D mode
            a.Y = b.Y = 0;
        }

        float dist = type switch {
            DistanceType.EUCLIDEAN => Vector3.Distance(a, b),
            DistanceType.MANHATTAN => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z),
        };

        return dist;
    }
    
    /// <summary>
    /// Calculate the distance between 2 points with specific settings
    /// </summary>
    /// <param name="type">The type of distance function to use</param>
    /// <returns>The distance between the 2 points</returns>
    public static float EvalDistance2D(Vector2i a, Vector2i b, DistanceType type) {
        return type switch {
            DistanceType.EUCLIDEAN => Vector2i.Distance(a, b),
            DistanceType.MANHATTAN => Vector2i.ManhattanDistance(a, b),
        };
    }

    public static ushort ApplyEditFunc(ushort x, EditFunc func, float strength) {
        var result = func switch {
            EditFunc.ADD       => x + (ushort)strength,
            EditFunc.SUBTRACT  => x - (ushort)strength,
            EditFunc.MULTIPLY  => (int)(x * strength),
            EditFunc.DIVIDE    => (int)(x / strength),
            EditFunc.OVERWRITE => (int)strength,
        };
        return (ushort)result;
    }

    /// <summary>
    /// Calculates the brush strength at a distance x.
    /// </summary>
    /// <param name="x">The distance from the brush's center</param>
    /// <returns></returns>
    public float EvalFalloff(float x) {
        return Brush.EvalFalloff(func, falloffStrength, x, baseStrength);
    }
    
    public float EvalBrushStrength(Vector3 pos) {
        float dist = EvalDistance(center, pos, falloffShape, isDistance3D);
        return EvalFalloff(dist);
    }

    /// <summary>
    /// Iterate over all pixels that will be affected by the brush, based on its
    /// current settings
    /// </summary>
    /// <returns>The point and its distance from the brush center</returns>
    public IEnumerable<(Vector2i, float)> IterAffectedPixels() {
        var c = new Vector2i((int)center.X, (int)center.Z);
        var min = c - new Vector2i(radius, radius);
        var max = c + new Vector2i(radius, radius);
        min.X = Math.Max(0, min.X);
        min.Y = Math.Max(0, min.Y);

        for (var x = min.X; x < max.X; x++) {
            for (var y = min.Y; y < max.Y; y++) {
                Vector2i p = new(x, y);
                var dist = EvalDistance2D(p, c, falloffShape);
                if (shape == Shape.SQUARE) {
                    // The point is always within the radius
                    yield return (p, dist);
                }

                if (dist > radius) {
                    continue; // Pixel out of range
                }
                yield return (p, dist);
            }
        }
    }

    public HashSet<int> ApplyToTiles(Cache.Cache cache, float multiplier) {
        HashSet<int> updatedTiles = new();
        
        foreach (var (p, dist) in IterAffectedPixels()) {
            Debug.Assert(p.X >= 0 && p.Y >= 0);
            TerrainCoords.PixelGrid8Pos pp = new(new Vector3(p.X, 0, p.Y));
            TerrainCoords.TileGrid8Pos tp = pp;

            var idx = ZOrder.Interleave8To16((byte)tp.x, (byte)tp.z);
            var tile = Array.Empty<ushort>();
            sbyte lod = -1;
            for (int i = ZOrder.MAX_LOD; i >= 0; i--) {
                var tileRes = cache.GetHeightmapTile(i, idx, false);
                if (tileRes.IsOk()) {
                    cache.MakeTileDirty(idx, LodComponent.hght, (byte)i);
                    tileRes = cache.GetHeightmapTile(i, idx, false);
                    tile = tileRes.Unwrap();
                    lod = (sbyte)i;
                    break;
                }
                idx >>= 2;
            }

            if (lod < 0) {
                break; // Couldn't find tile
            }

            var posInTile = (Vector2i)pp.xz;
            posInTile.X %= ZOrder.GRID_SIZE;
            posInTile.Y %= ZOrder.GRID_SIZE;

            int linearIdx = posInTile.X * ZOrder.GRID_SIZE + posInTile.Y;

            var strength = EvalFalloff(dist) * multiplier;
            try {
                ref var value = ref tile[linearIdx];
                value = ApplyEditFunc(value, editFunc, strength);
            } catch (IndexOutOfRangeException e) {
                Console.WriteLine("Index {0} @ pos {1} was out-of-bounds, p = {2}", linearIdx, posInTile, p);
            }

            updatedTiles.Add(ZOrder.PackIndex(idx, (byte)lod));
        }

        return updatedTiles;
    }
}