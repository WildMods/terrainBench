using OpenTK.Mathematics;

namespace terrainBench;

public static class VectorExtensions
{
    extension(Vector2i v) {
        public static float Distance(Vector2i a, Vector2i b) {
            return (a - b).EuclideanLength;
        }
        
        public static float ManhattanDistance(Vector2i a, Vector2i b) {
            return (a - b).ManhattanLength;
        }
    }
}