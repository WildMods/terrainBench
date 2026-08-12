using System.Diagnostics;
using OpenTK.Mathematics;

namespace terrainBench;

public struct Brush() {
    public enum Shape {
        CIRCLE,
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

    public Vector3 center = new();
    public float radius = 10f;
    public Shape shape = Shape.CIRCLE;
    public bool isDistance3D = false;

    public FalloffFunc func = FalloffFunc.LINEAR;
    public DistanceType falloffShape = DistanceType.EUCLIDEAN;
    
    /// <summary>
    /// The amount the brush strength decreases as it moves away from the center
    /// Implemented as the "m" to plug into the falloff function
    /// </summary>
    public float falloffStrength = 1f;
    
    /// <summary>
    /// The power of the brush before any falloff is applied.
    /// Implemented as the "b" to plug into the falloff function
    /// </summary>
    public float baseStrength = 1f;

    /// <summary>
    /// Calculates the result of a falloff function
    /// </summary>
    public static float EvalFalloff(FalloffFunc func, float m, float x, float b)
    {
        Debug.Assert((int)func < (int)FalloffFunc.ENUM_MAX);
        return func switch {
            FalloffFunc.LINEAR  => m * x + b,
            FalloffFunc.INVERSE => (1f / (m * x)) + b,
            FalloffFunc.SQUARE  => m * (x * x) + b,
            FalloffFunc.INVERSE_SQUARE => 1f / (m * (x * x)) + b,
        };
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
}