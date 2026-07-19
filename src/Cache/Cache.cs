using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench.Cache;

public class Cache
{
    private readonly Lod[] _lods = new Lod[9];
    
    public Cache(Game game)
    {
        for (int i = 0; i < 10; ++i)
        {
            _lods[i] = new(i, game);
        }
    }

    public IEnumerable<Result<ushort[], ErrorStack>> GetHeightmapForEntireLevel(int level)
    {
        for (ushort i = 0; i < Math.Pow(4, level); ++i)
        {
            yield return GetHeightmapTile(level, i);
        }
    }

    public Result<ushort[], ErrorStack> GetHeightmapTile(int level, ushort tileId)
    {
        var result = _lods[level].GetComponentTile(LodComponent.hght, tileId);
        ushort[]? subdivided = null;
        if (result.TryGetValue(ref subdivided, out var lower))
        {
            return subdivided;
        }
        return GenerateHeightmapSection(level - 1, lower.Item1, lower.Item2)
            .Inspect(tile => _lods[level].InsertComponentData(LodComponent.hght, tileId, tile))
            .Context("Could not load or generate tile. Warn me if this happens!");
    }

    public Result<ushort[], ErrorStack> GenerateHeightmapSection(
        int level,
        ushort tileId,
        ushort section
    ) {
        var res = _lods[level].GetComponentTile(LodComponent.hght, tileId);
        if (res.IsErr()) return Err(new ErrorStack("Lower LOD is also missing tile!"));
        var lowDetailTile = res.Unwrap();
        
        var minX = (section & 0b01) << 7;
        var maxX = minX + 0b10000000;
        var minY = (section & 0b10) << 6;
        var maxY = minY + 0b10000000;
        var subdivided = new ushort[65536];
        var destX = -2;
        var destY = -2;
        
        // aba
        // ccc
        // aba
        // A pass - pull pixels from lower-detail tile
        // B pass - lerp "horizontally"
        // C pass - lerp "vertically"
        
        // Map lower-detail pixels into their locations on the higher-detail tile
        for (var y = minY; y < maxY; ++y)
        {
            destY += 2;
            for (var x = minX; x < maxX; ++x)
            {
                destX += 2;
                subdivided[(destY << 8) + destX] = lowDetailTile[(y << 8) + x];
            }
            destX = -2;
        }
        
        // Lerp between pixels "horizontally"
        // skip last X because there's nothing to the right to lerp with it
        for (var y = 0; y < 256; y += 2)
        {
            for (var x = 1; x < 255; x += 2)
            {
                subdivided[y << 8 + x] = Convert.ToUInt16(Math.Floor(
                    float.Lerp(
                        Convert.ToSingle(subdivided[(y << 8) + x - 1]),
                        Convert.ToSingle(subdivided[(y << 8) + x + 1]),
                        0.5f
                    )
                ));
            }
        }
        
        // Lerp between pixels "vertically"
        // skip last y because there's nothing below to lerp with it
        // skip last x because there's nothing above *or* below to lerp with it
        for (var y = 1; y < 255; y += 2)
        {
            for (var x = 0; x < 255; ++x)
            {
                subdivided[y << 8 + x] = Convert.ToUInt16(Math.Floor(
                    float.Lerp(
                        Convert.ToSingle(subdivided[((y - 1) << 8) + x]),
                        Convert.ToSingle(subdivided[((y + 1) << 8) + x]),
                        0.5f
                    )
                ));
            }
        }
        
        // fill in "right" and "bottom" borders
        for (var y = 0; y < 255; ++y)
        {
            var source = (y << 8) + 254;
            subdivided[source + 1] = subdivided[source];
        }
        for (var x = 0; x < 256; ++x)
        {
            subdivided[(255 << 8) + x] = subdivided[(254 << 8) + x];
        }
        
        return subdivided;
    }
}