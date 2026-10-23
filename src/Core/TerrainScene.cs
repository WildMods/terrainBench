
namespace terrainBench.Core;
using TSCB = TerrainSceneBinary;

public class TerrainScene
{
    public class TileInfo() {
        public float x;
        public float z;
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
    }
    
    public uint version;
    public float worldScale;
    public float maxTerrainHeight;
    public float tileSize;

    public List<TileInfo> tiles = new();
    public List<TerrainSceneBinary.MaterialInfo> materials = new();
    
    
}