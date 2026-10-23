using OpenTK.Mathematics;
namespace terrainBench.Core;

// High-level object representing the TSCB (Terrain Scene Binary) file
public class TerrainScene {
    public class TileInfo {
        public Vector2 center;
        public float tileSize;
        public float minLandHeight;
        public float maxLandHeight;
        public float minWaterHeight;
        public float maxWaterHeight;
        public uint unknown1;
        public string basename;
        public uint unknown2;
        public uint unknown3;
        public bool hasGrass;
        public bool hasWater;
        public ushort zIndex;
        public byte lod;

        /// <summary>
        /// Calculate the size of this tile in world space (which is 10k x 10k in vanilla)
        /// </summary>
        /// <param name="hdrWorldScale">The world scale value from the header</param>
        /// <param name="hdrTileSize">The tile size value from the header</param>
        public float CalcWorldSize(float hdrWorldScale, float hdrTileSize) {
            // Taken from Zephenryus' formula on the wiki, presumably found via RE
            // https://zeldamods.org/wiki/TSCB#Parameters
            return (tileSize / hdrTileSize * hdrWorldScale) * 20.0f;
        }
        public float CalcWorldSize(TerrainScene tscb) {
            return CalcWorldSize(tscb.worldScale, tscb.tileSize);
        }
    }
    
    public uint version; // Always 10
    /// <summary>Acts as a multiplier to all tile sizes. In vanilla this is 500
    /// and the biggest tile is 32 (giving 16000, the game world size)</summary>
    public float worldScale;
    public float maxTerrainHeight;
    
    public float tileSize;

    public List<TileInfo> tiles = new();
    public List<TerrainSceneBinary.MaterialInfo> materials = new();
    
    
}