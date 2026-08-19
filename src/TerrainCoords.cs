using OpenTK.Mathematics;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace terrainBench;

// Convert between various terrain coordinate systems
public static class TerrainCoords {
    // See https://zeldamods.org/wiki/TSCB#Parameters
    // The tile entry for the level 0 tile has area_size = 32.
    // tile_size = 32 and world_scale = 500.
    // Plugging into the equation on the wiki:
    //    (32 / 32 * 500) * 20 = 10000
    public const int WORLD_SIZE = 10000;
    public const float TILE_TO_WORLD_HEIGHT = WORLD_SIZE / (float)ZOrder.GRID_SIZE;
    // Taken from the vanilla TSCB header
    public const float WORLD_HEIGHT = 800;
    
    public class Vec3Base(Vector3 position) {
        protected Vector3 position = position;
        public float x { get => position.X; set => position.X = value; }
        public float y { get => position.Y; set => position.Y = value; }
        public float z { get => position.Z; set => position.Z = value; }
        
        public Vector2 xz =>  new(position.X, position.Z);
        
        public static Vec3Base operator+(Vec3Base p1, Vec3Base p2) {
            return new(p1.position + p2.position);
        }
    }
    
    public class WorldPos(Vector3 position) : Vec3Base(position) {
        public static implicit operator Vector3(WorldPos w) => w.position;

        public static Matrix4 FromTileGridXform() {
            var scale = Matrix4.CreateScale(TILE_TO_WORLD_HEIGHT, 1, TILE_TO_WORLD_HEIGHT);
            var offset = Matrix4.CreateTranslation(-WORLD_SIZE / 2, 0, -WORLD_SIZE / 2);
            return scale * offset;
        }

        public static implicit operator WorldPos(TileGrid8Pos p) {
            var offset = new Vector3(WORLD_SIZE / 2, 0, WORLD_SIZE / 2);
            var mult = ((float)ZOrder.GRID_SIZE / WORLD_SIZE);
            Vector3 wp = p;
            wp.X /= mult;
            wp.Z /= mult;
            wp -= offset;
            return new(wp);
        }
        
        public static implicit operator TileGrid8Pos(WorldPos wp) {
            var offset = new Vector3(WORLD_SIZE / 2, 0, WORLD_SIZE / 2);
            var mult = ((float)ZOrder.GRID_SIZE / WORLD_SIZE);
            Vector3 tp = wp + offset;
            tp.X *= mult;
            tp.Z *= mult;
            return new(tp);
        }
        
        public static WorldPos operator+(WorldPos p1, WorldPos p2) {
            return new(p1.position + p2.position);
        }
        public static WorldPos operator-(WorldPos p1, WorldPos p2) {
            return new(p1.position - p2.position);
        }

        /// <summary>
        /// Convert a world space direction to a tile space one.
        /// Only use this with direction vectors, not positions.
        /// </summary>
        public TileGrid8Pos ToTileDir() {
            const float mult = ((float)ZOrder.GRID_SIZE / WORLD_SIZE);
            return new(new Vector3(position.X * mult, position.Y, position.Z * mult));
        }
        
        public static implicit operator WorldPos(Vector2 v) {
            return new(new Vector3(v.X, 0, v.Y));
        }
    }

    public class TileGrid8Pos(Vector3 position) : Vec3Base(position) {
        public static implicit operator Vector3(TileGrid8Pos p) => p.position;
        public static implicit operator PixelGrid8Pos(TileGrid8Pos p) {
            return new(new Vector3(p.x * 256, p.y, p.z * 256));
        }
        public static implicit operator TileGrid8Pos(PixelGrid8Pos p) {
            return new(new Vector3(p.x / 256, p.y, p.z / 256));
        }
        
        public static implicit operator TileGrid8Pos(Vector2 v) {
            return new(new Vector3(v.X, 0, v.Y));
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
        
        public static implicit operator PixelGrid8Pos(Vector2 v) {
            return new(new Vector3(v.X, 0, v.Y));
        }
    }
}
