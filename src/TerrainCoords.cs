using OpenTK.Mathematics;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace terrainBench;

// Convert between various terrain coordinate systems
public static class TerrainCoords {
    public class Vec3Base(Vector3 position) {
        protected Vector3 position = position;
        public float x { get => position.X; set => position.X = value; }
        public float y { get => position.Y; set => position.Y = value; }
        public float z { get => position.Z; set => position.Z = value; }
        
        public static Vec3Base operator+(Vec3Base p1, Vec3Base p2) {
            return new(p1.position + p2.position);
        }
    }
    
    public class WorldPos(Vector3 position) : Vec3Base(position) {
        public const int WORLD_SIZE = 16 * 1000;
        public static implicit operator Vector3(WorldPos w) => w.position;

        public static Matrix4 FromTileGridXform() {
            var scale = Matrix4.CreateScale(WORLD_SIZE / (float)ZOrder.GRID_SIZE);
            var offset = Matrix4.CreateTranslation(-WORLD_SIZE / 2, 0, -WORLD_SIZE / 2);
            return scale * offset;
        }

        public static implicit operator WorldPos(TileGrid8Pos p) {
            var wp = FromTileGridXform() * new Vector4(p, 1);
            return new(wp.Xyz);
        }
        
        public static implicit operator TileGrid8Pos(WorldPos wp) {
            var tp = wp + new Vector3(WORLD_SIZE / 2, 0, WORLD_SIZE / 2);
            tp *= ((float)ZOrder.GRID_SIZE / WORLD_SIZE);
            return new(tp);
        }
        
        public static WorldPos operator+(WorldPos p1, WorldPos p2) {
            return new(p1.position + p2.position);
        }
        public static WorldPos operator-(WorldPos p1, WorldPos p2) {
            return new(p1.position - p2.position);
        }
    }

    public class TileGrid8Pos(Vector3 position) : Vec3Base(position) {
        public static implicit operator Vector3(TileGrid8Pos p) => p.position;
        public static implicit operator PixelGrid8Pos(TileGrid8Pos p) {
            return new((Vector3)p * 256);
        }
        public static implicit operator TileGrid8Pos(PixelGrid8Pos p) {
            return new((Vector3)p / 256);
        }
    }
    
    public class PixelGrid8Pos(Vector3 position) : Vec3Base(position) {
        public static implicit operator Vector3(PixelGrid8Pos p) => p.position;
        public static implicit operator WorldPos(PixelGrid8Pos p) {
            return (WorldPos)(TileGrid8Pos)p;
        }
        
        public static PixelGrid8Pos operator+(PixelGrid8Pos p1, PixelGrid8Pos p2) {
            return new(p1.position + p2.position);
        }
        public static PixelGrid8Pos operator-(PixelGrid8Pos p1, PixelGrid8Pos p2) {
            return new(p1.position - p2.position);
        }
    }
}