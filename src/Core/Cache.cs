using System.Diagnostics;
using OpenTK.Mathematics;
using OperationResult;
using SkiaSharp;
using static OperationResult.Helpers;

namespace terrainBench.Core;
using LodComponents;

public class Cache
{
    private readonly Lod[] _lods = new Lod[9];

    public Cache() {
        Clear();
    }

    public void Clear() {
        for (int i = 0; i <= ZOrder.MAX_LOD; ++i) {
            _lods[i] = new(i);
        }
    }

    public Result<bool, ErrorStack> LoadFromImage(string path, int lvl, ProgressReport tilesLoadedOut) {
        var timer = Stopwatch.StartNew();
        
        for (int i = 0; i <= ZOrder.MAX_LOD; i++) {
            _lods[i].loadFinished = true;
        }

        SKBitmap? bmp = null;
        try {
            bmp = SKBitmap.Decode(path);
            Console.WriteLine("Decoded '{0}': {1}", path, bmp);
            if (bmp == null) {
                return Err(new ErrorStack($"Failed to load '{path}'"));
            }
        } catch (Exception e) {
            Console.WriteLine($"Failed to load image '{path}' {e}");
            return Err(new ErrorStack($"Failed to load image '{path}'", e));
        }

        if (lvl < 0) {
            // Auto-select import LOD based on heightmap resolution
            for (int i = 0; i <= ZOrder.MAX_LOD; i++) {
                if ((1 << i) * ZOrder.GRID_SIZE >= bmp.Height && (1 << i) * ZOrder.GRID_SIZE >= bmp.Width) {
                    lvl = i;
                    break;
                }
            }
        }
        
        if (lvl >= _lods.Length || lvl < 0) {
            return Err(new ErrorStack($"LOD {lvl} doesn't exist!"));
        }
        
        int lvlDiff = ZOrder.MAX_LOD - lvl;
        var res = _lods[lvl].LoadHeightmapImage(bmp, 2 + lvlDiff, tilesLoadedOut);

        DownscaleAllLevels(LodComponent.hght);
        DownscaleAllLevels(LodComponent.mate);
        timer.Stop();
        Console.WriteLine("Loaded '{0}' in {1}ms", path, timer.ElapsedMilliseconds);
        return res;
    }

    public void Load(Game game, ProgressReport tilesLoadedOut) {
        var timer = Stopwatch.StartNew();
        using (Profiler.BeginZone("CacheLoad"))
        {
            for (int i = 0; i < 9; ++i)
            {
                _lods[i].Load(game, tilesLoadedOut);
            }
        }
        timer.Stop();
        Console.WriteLine("Loaded all terrain data in {0}ms", timer.ElapsedMilliseconds);
    }

    public IEnumerable<Result<ushort[], ErrorStack>> GetHeightmapForEntireLevel(int level)
    {
        for (ushort i = 0; i < Math.Pow(4, level); ++i)
        {
            yield return GetHeightmapTile(level, i);
        }
    }

    public Result<bool, ErrorStack> DownscaleLevel(uint level, LodComponent component)
    {
        if (level >= _lods.Length) {
            return Err(new ErrorStack($"LOD {level} doesn't exist!"));
        }
        if (level == 0) {
            return false; // Nothing to do, but not an error
        }

        bool res = true;
        Parallel.ForEach(_lods[level].IterDirtyTiles(component), idx => {
            var downscaleRes = DownscaleTileByOne(idx, level, component);
            if (downscaleRes.IsErr()) {
                res = false;
            }
        });

        return res;
    }
    
    public Result<bool, ErrorStack> DownscaleAllLevels(LodComponent component) {
        for (int i = ZOrder.MAX_LOD; i >= 0; i--) {
            var res = DownscaleLevel((uint)i, component);
            if (res.IsErr()) {
                return Err(res.ExpectErr("").Context($"Failed to downscale level {i} -> {i - 1}"));
            };
        }
        return true;
    }

    Result<bool, ErrorStack> DownscaleTileByOne(ushort idx, uint level, LodComponent component)
    {
        if (level >= _lods.Length) {
            return Err(new ErrorStack($"LOD {level} doesn't exist!"));
        }
        if (level == 0) {
            return false; // Nothing to do, but not an error
        }

        Vector2i tilePos;
        {
            ZOrder.Deinterleave16To8(idx, out var x, out var y);
            tilePos = new(x, y);
        }
        
        int size = component switch {
            LodComponent.hght => ZOrder.GRID_SIZE,
            LodComponent.mate => ZOrder.GRID_SIZE,
            LodComponent.water => 64,
            LodComponent.grass => 64,
            _ => 0,
        };
        if (size == 0) {
            return Err(new ErrorStack($"Invalid terrain component value ({component})!"));
        }

        Vector2i pixelPosLow = TileScaling.GetLowDetailPos(tilePos, size);
        ushort lowIdx = (ushort)(idx >> 2);
        MakeTileDirty(lowIdx, component, (byte)(level - 1));
        switch (component) {
        case LodComponent.hght: {
            var highTile = _lods[level].GetHeightmapTile(idx).Unwrap();
            var lowTileRes = _lods[level - 1].GetHeightmapTile(lowIdx);
            if (lowTileRes.IsErr()) {
                _lods[level - 1].InsertTile(lowIdx, new ushort[ZOrder.GRID_SIZE * ZOrder.GRID_SIZE]);
                lowTileRes = _lods[level - 1].GetHeightmapTile(lowIdx);
            }
            
            TileScaling.DownscaleTile(highTile, lowTileRes.Unwrap(), pixelPosLow, size);
            break;
        }
        case LodComponent.mate: {
            var highTile = _lods[level].GetMaterialTile(idx).Unwrap();
            var lowTileRes = _lods[level - 1].GetMaterialTile(lowIdx);
            if (lowTileRes.IsErr()) {
                _lods[level - 1].InsertTile(lowIdx, new Material[ZOrder.GRID_SIZE * ZOrder.GRID_SIZE]);
                lowTileRes = _lods[level - 1].GetMaterialTile(lowIdx);
            }
            
            TileScaling.DownscaleTile(highTile, lowTileRes.Unwrap(), pixelPosLow, size);
            break;
        }
        default:
            // TODO: Implement math operators for other types so we can downscale them
            return Err(new ErrorStack($"Unsupported component: {component}!"));
        }

        return true;
    }

    public Result<bool, ErrorStack> DownscaleTileCascade(ushort idx, uint level, LodComponent component)
    {
        for (uint i = level; i > 0; i--) {
            var res = DownscaleTileByOne(idx, i, component);
            if (res.IsErr()) {
                return Err(res.ExpectErr("").Context($"Failed to downscale idx {idx} @ level {i}"));
            }
            idx >>= 2;
        }

        return true;
    }

    public Result<ushort[], ErrorStack> GetHeightmapTile(int level, ushort tileId, bool upscale = true)
    {
        var result = _lods[level].GetHeightmapTile(tileId);
        ushort[]? subdivided = null;
        if (result.TryGetValue(ref subdivided, out var lower))
        {
            return subdivided;
        } else if (!upscale) {
            return Err(new ErrorStack("Unable to find heightmap tile and upscaling was disabled."));
        }
        return GenerateHeightmapSection(level - 1, lower.Item1, lower.Item2)
            .Inspect(tile => _lods[level].InsertTile(tileId, tile))
            .Context("Could not load or generate heightmap tile.");
    }

    public Result<ushort[], ErrorStack> GenerateHeightmapSection(
        int level,
        ushort tileId,
        ushort section
    ) {
        var res = _lods[level].GetHeightmapTile(tileId);
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

    public Result<Material[], ErrorStack> GetMaterialTile(int level, ushort tileId, bool upscale = true)
    {
        var result = _lods[level].GetMaterialTile(tileId);
        Material[]? subdivided = null;
        if (result.TryGetValue(ref subdivided, out var lower))
        {
            return subdivided;
        } else if (!upscale) {
            return Err(new ErrorStack("Unable to find material tile and upscaling was disabled."));
        }
        return GenerateMaterialSection(level - 1, lower.Item1, lower.Item2)
            .Inspect(tile => _lods[level].InsertTile(tileId, tile))
            .Context("Could not load or generate material tile.");
    }

    public Result<Material[], ErrorStack> GenerateMaterialSection(
        int level,
        ushort tileId,
        ushort section
    )
    {
        return Err(new ErrorStack("Not implemented", new NotImplementedException()));
    }

    public Result<GrassExtm[], ErrorStack> GetGrassTile(int level, ushort tileId)
    {
        var result = _lods[level].GetGrassTile(tileId);
        GrassExtm[]? subdivided = null;
        if (result.TryGetValue(ref subdivided, out var lower))
        {
            return subdivided;
        }
        return GenerateGrassSection(level - 1, lower.Item1, lower.Item2)
            .Inspect(tile => _lods[level].InsertTile(tileId, tile))
            .Context("Could not load or generate grass tile.");
    }

    public Result<GrassExtm[], ErrorStack> GenerateGrassSection(
        int level,
        ushort tileId,
        ushort section
    )
    {
        return Err(new ErrorStack("Not implemented", new NotImplementedException()));
    }

    public Result<WaterExtm[], ErrorStack> GetWaterTile(int level, ushort tileId, bool upscale)
    {
        var result = _lods[level].GetWaterTile(tileId);
        WaterExtm[]? subdivided = null;
        if (result.TryGetValue(ref subdivided, out var lower))
        {
            return subdivided;
        }
        return GenerateWaterSection(level - 1, lower.Item1, lower.Item2)
            .Inspect(tile => _lods[level].InsertTile(tileId, tile))
            .Context("Could not load or generate water tile.");
    }

    public Result<WaterExtm[], ErrorStack> GenerateWaterSection(
        int level,
        ushort tileId,
        ushort section
    )
    {
        return Err(new ErrorStack("Not implemented", new NotImplementedException()));
    }

    public void WriteAllTiles(string basePath, bool dirty, CsOead.Endianness endian, string fieldName = "MainField")
    {
        var timer = Stopwatch.StartNew();
        DownscaleAllLevels(LodComponent.hght);
        DownscaleAllLevels(LodComponent.mate);
        for (uint i = 0; i <= ZOrder.MAX_LOD; i++) {
            var lvl = _lods[i];
            lvl.WriteAllTiles(basePath, dirty, endian, fieldName);
            Console.WriteLine("Saved level {0}", i);
        }

        timer.Stop();
        Console.WriteLine("Finished saving all tiles in {0}ms.", timer.ElapsedMilliseconds);
    }

    public bool MakeTileDirty(ushort tileId, LodComponent type, byte lod) {
        if (lod > ZOrder.MAX_LOD) {
            return false;
        }

        _lods[lod].MakeTileDirty(tileId, type);
        return true;
    }
}
