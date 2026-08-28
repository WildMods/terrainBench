using System.Diagnostics;
using OpenTK.Mathematics;
using terrainBench.Cache;
using terrainBench.LodComponents;

namespace terrainBench;

public struct Brush() {
    public enum Shape {
        CIRCLE, SQUARE,
    }
    
    public enum FalloffFunc {
        LINEAR,         // mx + b
        INVERSE,        // (m / x) + b
        SQUARE,         // mx^2 + b
        INVERSE_SQUARE, // (m / (x^2)) + b
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

    public LodComponent target = LodComponent.mate;
    public int[] textureIndices = { 0, 1 };
    
    /// <summary>
    /// The amount the brush strength decreases as it moves away from the center
    /// Implemented as the "m" to plug into the falloff function
    /// </summary>
    public float falloffStrength = -10f;

    /// <summary>
    /// The power of the brush before any falloff is applied.
    /// Implemented as the "b" to plug into the falloff function
    /// </summary>
    public float baseStrength = 20f;

    /// <summary>
    /// Calculates the result of a falloff function
    /// </summary>
    public static float ApplyFalloffFunc(FalloffFunc func, float m, float x, float b)
    {
        var result = func switch {
            FalloffFunc.LINEAR  => m * x + b,
            FalloffFunc.INVERSE => (m / x) + b,
            FalloffFunc.SQUARE  => m * (x * x) + b,
            FalloffFunc.INVERSE_SQUARE => m / (x * x) + b,
            _ => 0,
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
            DistanceType.MANHATTAN => Vector3.ManhattanDistance(a, b),
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

    public static int ApplyEditFunc(int x, EditFunc func, float strength) {
        return func switch {
            EditFunc.ADD       => x + (int)strength,
            EditFunc.SUBTRACT  => x - (int)strength,
            EditFunc.MULTIPLY  => (int)(x * strength),
            EditFunc.DIVIDE    => (int)(x / strength),
            EditFunc.OVERWRITE => (int)strength,
        };
    }
    public static ushort ApplyEditFunc16(ushort x, EditFunc func, float strength) {
        var result = ApplyEditFunc(x, func, strength);
        return (ushort)Math.Clamp(result, ushort.MinValue, ushort.MaxValue - 1);
    }
    
    public static byte ApplyEditFunc8(byte x, EditFunc func, float strength) {
        var result = ApplyEditFunc(x, func, strength);
        return (byte)Math.Clamp(result, byte.MinValue, byte.MaxValue - 1);
    }

    /// <summary>
    /// Calculates the brush strength at a distance x.
    /// </summary>
    /// <param name="x">The distance from the brush's center</param>
    /// <returns></returns>
    public float EvalFalloff(float x) {
        return Brush.ApplyFalloffFunc(func, falloffStrength, x, baseStrength);
    }
    
    public float EvalBrushStrength(Vector3 pos) {
        float dist = EvalDistance(center, pos, falloffShape, isDistance3D);
        return EvalFalloff(dist);
    }

    /// <summary>
    /// Iterate over all tiles that will be affected by the brush, based on its
    /// current settings
    /// </summary>
    /// <returns>The packed tile index/LOD</returns>
    public IEnumerable<Int32> IterAffectedTiles(CoverageMap coverage) {
        var c = new Vector2i((int)center.X, (int)center.Z);
        var min = c - new Vector2i(radius, radius);
        var max = c + new Vector2i(radius, radius);
        min.X = Math.Clamp(min.X, 0, UInt16.MaxValue);
        min.Y = Math.Clamp(min.Y, 0, UInt16.MaxValue);
        max.X = Math.Clamp(max.X, 0, UInt16.MaxValue);
        max.Y = Math.Clamp(max.Y, 0, UInt16.MaxValue);

        var minTile = min / ZOrder.GRID_SIZE;
        var maxTile = max / ZOrder.GRID_SIZE;
        Debug.Assert(minTile.X < ZOrder.GRID_SIZE && minTile.Y < ZOrder.GRID_SIZE);
        Debug.Assert(maxTile.X < ZOrder.GRID_SIZE && maxTile.Y < ZOrder.GRID_SIZE);

        foreach (var packed in coverage.FindAllTilesInSquare((byte)minTile.X, (byte)minTile.Y, (byte)maxTile.X, (byte)maxTile.Y))
        {
            yield return packed;
        }
    }

    public List<int> ApplyToTiles(Cache.Cache cache, CoverageMap coverage, float multiplier, int component = 0) {
        var z = Profiler.BeginZone("Brush.ApplyToTiles");
        if (target == LodComponent.hght) {
            component = 0; // This is the only usable component for HGHT
        }
        List<int> updatedTiles = new();
        var c = new Vector2i((int)center.X, (int)center.Z);
        
        foreach (var packed in IterAffectedTiles(coverage)) {
            var tileOps = Profiler.BeginZone("BrushPerTileCalc");
            ZOrder.UnpackIndex(packed, out var idx, out byte lod);
            
            updatedTiles.Add(packed);
            cache.MakeTileDirty(idx, LodComponent.hght, lod);
            
            var lvlDiff = ZOrder.MAX_LOD - lod;
            var lvl8Idx = idx << lvlDiff * 2; // Convert to level 8 tile index
            ZOrder.Deinterleave16To8((ushort)lvl8Idx, out var tileX, out var tileY);
            TerrainCoords.TileGrid8Pos tp = new(new Vector3(tileX, 0, tileY));
            var pp = (TerrainCoords.PixelGrid8Pos)tp;

            var pixelSize = 1 << lvlDiff;
            tileOps.Dispose();

            var hghtTile = cache.GetHeightmapTile(lod, idx, false).Unwrap();
            var mateTile = cache.GetMaterialTile(lod, idx, false).Unwrap();
            
            var applyPixels = Profiler.BeginZone("BrushApplyPixels");

            for (int x = 0; x < ZOrder.GRID_SIZE; x++) {
                for (int y = 0; y < ZOrder.GRID_SIZE; y++) {
                    Vector2i posInTile = new(x, y);

                    Vector2i p = (Vector2i)pp.xz + posInTile * pixelSize;
                    var dist = EvalDistance2D(p, c, falloffShape);
                    if (shape == Shape.SQUARE) {
                        // The point is always within the radius
                    } else if (dist > radius) {
                        continue; // Pixel out of range
                    }

                    int linearIdx = posInTile.Y * ZOrder.GRID_SIZE + posInTile.X;
                    var strength = EvalFalloff(dist / radius) * multiplier;
                    
                    try {
                        if (target == LodComponent.hght) {
                            ref var value = ref hghtTile[linearIdx];
                            value = ApplyEditFunc16(value, editFunc, strength);
                        } else if (target == LodComponent.mate) {
                            if (component < textureIndices.Length) {
                                // It doesn't make sense to do falloff on a
                                // texture index. Force overwrite with the active value
                                strength = textureIndices[component];
                            }
                            ref var pixel = ref mateTile[linearIdx];
                            var val = pixel.GetComponent(component);
                            val = ApplyEditFunc8(val, editFunc, strength);
                            pixel.SetComponent(val, component);
                        }
                    } catch (IndexOutOfRangeException e) {
                        Console.WriteLine("Index {0} @ pos {1} was out-of-bounds", linearIdx, posInTile);
                    }
                }
            }
            
            applyPixels.Dispose();
        }

        z.Dispose();
        return updatedTiles;
    }
}