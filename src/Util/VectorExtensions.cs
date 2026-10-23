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
    
    extension(Vector3 v) {
        public static float ManhattanDistance(Vector3 a, Vector3 b) {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
        }
    }
}