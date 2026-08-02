using CommunityToolkit.HighPerformance;
using OperationResult;
using CsOead;
using terrainBench.LodComponents;
using static OperationResult.Helpers;
using System.Collections.Concurrent;

namespace terrainBench.Cache;

// Shorthands to keep later code a bit shorter
using HGHTMap = ConcurrentDictionary<ushort, ushort[]>;
using MATEMap = ConcurrentDictionary<ushort, LodComponents.Material[]>;
using GrassMap = ConcurrentDictionary<ushort, LodComponents.GrassExtm[]>;
using WaterMap = ConcurrentDictionary<ushort, LodComponents.WaterExtm[]>;

public class Lod
{
    private static readonly short[] TILE_COUNTS = [1, 4, 16, 36, 144, 320, 1154, 3616, 3742];
    private readonly int _level;
    private readonly HGHTMap _hghts;
    private readonly HGHTMap _dirtyHghts;
    private readonly MATEMap _mates;
    private readonly MATEMap _dirtyMates;
    private readonly GrassMap _grass = new();
    private readonly GrassMap _dirtyGrass = new();
    private readonly WaterMap _water = new();
    private readonly WaterMap _dirtyWater = new();

    private bool loadFinished = false;

    public Lod(int level)
    {
        _level = level;

        _hghts = new();
        _dirtyHghts = new();
        _mates = new();
        _dirtyMates = new();

        _grass = new();
        _dirtyGrass = new();
        _water = new();
        _dirtyWater = new();
    }

    public void Load(Game game)
    {
        var tasks = new List<Task>()
        {
            LoadHeightmapTiles(game),
            LoadMaterialTiles(game),
            LoadGrassTiles(game),
            LoadWaterTiles(game)
        };
        Task.WaitAll(tasks);
        loadFinished = true;
        Console.WriteLine("Loaded level {0}", _level);
    }

    private void WaitForLoad() {
        while (!loadFinished) {
            Thread.Sleep(5);
        }
    }

    private async Task LoadHeightmapTiles(Game game)
    {
        var iter = game.IterLodComponent(_level, LodComponent.hght);
        var tasks = new List<Task>(TILE_COUNTS[_level]);
        
        foreach (var result in iter)
        {
            if (result.IsErr()) return;
            tasks.Add(Task.Run(delegate
            {
                using (Profiler.BeginZone("LoadSSTERA_HGHT")) {
                    if (result.IsErr()) return;
                    using (var s = Sarc.FromBinary(result.Unwrap()))
                    {
                        foreach (var (name, data) in s)
                        {
                            var tileId = ZOrder.IndexFromFilename(name);
                            if (tileId.IsErr()) continue;
                            using (Profiler.BeginZone("HGHT_ToArray")) {
                                _hghts[tileId.Unwrap()] = data.AsSpan().Cast<byte, ushort>().ToArray();
                            }
                        }
                    }
                    result.Unwrap().Dispose();
                }
            }));
        }
        
        await Task.WhenAll(tasks);
    }

    private async Task LoadMaterialTiles(Game game)
    {
        var iter = game.IterLodComponent(_level, LodComponent.mate);
        var tasks = new List<Task>(TILE_COUNTS[_level]);
        
        foreach (var result in iter)
        {
            if (result.IsErr()) return;
            tasks.Add(Task.Run(delegate
            {
                using (Profiler.BeginZone("LoadSSTERA_MATE")) {
                    if (result.IsErr()) return;
                    using (var s = Sarc.FromBinary(result.Unwrap()))
                    {
                        foreach (var (name, data) in s)
                        {
                            var tileId = ZOrder.IndexFromFilename(name);
                            if (tileId.IsErr()) continue;
                            using (Profiler.BeginZone("MATE_ToArray"))
                            {
                                _mates[tileId.Unwrap()] = data.AsSpan().Cast<byte, Material>().ToArray();
                            }
                        }
                    }
                    result.Unwrap().Dispose();
                }
            }));
        }
        
        await Task.WhenAll(tasks);
    }

    private async Task LoadGrassTiles(Game game)
    {
        var iter = game.IterLodComponent(_level, LodComponent.grass);
        var tasks = new List<Task>(TILE_COUNTS[_level]);
        
        foreach (var result in iter)
        {
            tasks.Add(Task.Run(delegate
            {
                using (Profiler.BeginZone("LoadSSTERA_GRASS")) {
                    if (result.IsErr()) return;
                    using (var s = Sarc.FromBinary(result.Unwrap()))
                    {
                        foreach (var (name, data) in s)
                        {
                            var tileId = ZOrder.IndexFromFilename(name);
                            if (tileId.IsErr()) continue;
                            _grass[tileId.Unwrap()] = data.AsSpan().Cast<byte, GrassExtm>().ToArray();
                        }
                    }
                    result.Unwrap().Dispose();
                }
            }));
        }
        
        await Task.WhenAll(tasks);
    }

    private async Task LoadWaterTiles(Game game)
    {
        var iter = game.IterLodComponent(_level, LodComponent.water);
        var tasks = new List<Task>(TILE_COUNTS[_level]);
        
        foreach (var result in iter)
        {
            tasks.Add(Task.Run(delegate
            {
                using (Profiler.BeginZone("LoadSSTERA_WATER")) {
                    if (result.IsErr()) return;
                    using (var s = Sarc.FromBinary(result.Unwrap()))
                    {
                        foreach (var (name, data) in s)
                        {
                            var tileId = ZOrder.IndexFromFilename(name);
                            if (tileId.IsErr()) continue;
                            _water[tileId.Unwrap()] = data.AsSpan().Cast<byte, WaterExtm>().ToArray();
                        }
                    }
                    result.Unwrap().Dispose();
                }
            }));
        }
        
        await Task.WhenAll(tasks);
    }

    public Result<ushort[], (ushort, ushort)> GetHeightmapTile(ushort tileId)
    {
        WaitForLoad();
        using (Profiler.BeginZone("LodGetHGHT")) {
            if (_dirtyHghts.TryGetValue(tileId, out var result) || _hghts.TryGetValue(tileId, out result))
            {
                return result;
            }

            var newId = (ushort)(tileId >> 2);
            var section = (ushort)(tileId & 0b11);
            return Err((newId, section));
        }
    }

    public void InsertTile(ushort tileId, ushort[] data)
    {
        WaitForLoad();
        _dirtyHghts[tileId] = data;
    }

    public Result<Material[], (ushort, ushort)> GetMaterialTile(ushort tileId)
    {
        WaitForLoad();
        using (Profiler.BeginZone("LodGetMATE")) {
            if (_dirtyMates.TryGetValue(tileId, out var result) || _mates.TryGetValue(tileId, out result))
            {
                return result;
            }

            var newId = (ushort)(tileId >> 2);
            var section = (ushort)(tileId & 0b11);
            return Err((newId, section));
        }
    }

    public void InsertTile(ushort tileId, Material[] data)
    {
        WaitForLoad();
        _dirtyMates[tileId] = data;
    }

    public Result<GrassExtm[], (ushort, ushort)> GetGrassTile(ushort tileId)
    {
        WaitForLoad();
        if (_dirtyGrass.TryGetValue(tileId, out var result) || _grass.TryGetValue(tileId, out result))
        {
            return result;
        }

        var newId = (ushort)(tileId >> 2);
        var section = (ushort)(tileId & 0b11);
        return Err((newId, section));
    }

    public void InsertTile(ushort tileId, GrassExtm[] data)
    {
        WaitForLoad();
        _dirtyGrass[tileId] = data;
    }

    public Result<WaterExtm[], (ushort, ushort)> GetWaterTile(ushort tileId)
    {
        WaitForLoad();
        if (_dirtyWater.TryGetValue(tileId, out var result) || _water.TryGetValue(tileId, out result))
        {
            return result;
        }

        var newId = (ushort)(tileId >> 2);
        var section = (ushort)(tileId & 0b11);
        return Err((newId, section));
    }

    public void InsertTile(ushort tileId, WaterExtm[] data)
    {
        WaitForLoad();
        _dirtyWater[tileId] = data;
    }

    /// <summary>
    /// Build a SARC containing all the tiles adjacent to the given index.
    /// The "STERA" name is not a typo, since the first S in "SSTERA" indicates
    /// Yaz0, and this just gives the uncompressed SARC.
    /// </summary>
    /// <param name="sarcName">The filename that the compressed SARC should have (file extension ending in ".sstera")</param>
    public Result<Sarc, ErrorStack> BuildSTERA(UInt16 idx, LodComponent type, out string sarcName) {
        string ext = type switch {
            LodComponent.hght => "hght",
            LodComponent.mate => "mate",
            LodComponent.grass => "grass.extm",
            LodComponent.water => "water.extm",
            _ => "",
        };
        string invalidEnumMsg = $"Invalid enum value ${(int)type} given for terrain data type";
        if (ext == "") {
            sarcName = "";
            return Err(new ErrorStack(invalidEnumMsg));
        }

        // SSTERAs must start at a multiple of 4, so round down
        idx = ZOrder.RoundToSSTERAIdx(idx);
        sarcName = ZOrder.BuildFilename(idx, _level, $"${ext}.sstera");

        // Try to add the 4 tiles within this SSTERA (some may be missing)
        var sarc = new CsOead.Sarc();
        for (UInt16 i = 0; i < 4; i++) {
            UInt16 tileIdx = (UInt16)(idx + i);
            string fname = ZOrder.BuildFilename(tileIdx, _level, ext);

            // Get the actual tile data.
            // There's definitely a more concise way to do this, but I'm not
            // very familiar with OperationResult. Sorry.
            // - torf
            ReadOnlySpan<byte> tile = new();
            bool found = true;
            switch (type) {
            case LodComponent.hght: {
                var r = GetHeightmapTile(tileIdx).InspectErr(err => found = false);
                if (r.IsOk()) {
                    tile = r.Unwrap().AsBytes();
                }
                break;
            }
            case LodComponent.mate: {
                var r = GetMaterialTile(tileIdx).InspectErr(err => found = false);
                if (r.IsOk()) {
                    tile = r.Unwrap().AsBytes();
                }
                break;
            }
            case LodComponent.grass: {
                var r = GetGrassTile(tileIdx).InspectErr(err => found = false);
                if (r.IsOk()) {
                    tile = r.Unwrap().AsBytes();
                }
                break;
            }
            case LodComponent.water: {
                var r = GetWaterTile(tileIdx).InspectErr(err => found = false);
                if (r.IsOk()) {
                    tile = r.Unwrap().AsBytes();
                }
                break;
            }
            default:
                sarcName = "";
                return Err(new ErrorStack(invalidEnumMsg));
            }

            if (found) {
                sarc.Add(fname, tile);
            }
        }

        return sarc;
    }
}
