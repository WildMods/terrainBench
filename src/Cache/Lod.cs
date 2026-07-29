using CommunityToolkit.HighPerformance;
using OperationResult;
using CsOead;
using terrainBench.LodComponents;
using static OperationResult.Helpers;
using System.Runtime.InteropServices;

namespace terrainBench.Cache;

public class Lod
{
    private static readonly short[] TILE_COUNTS = [1, 4, 16, 36, 144, 320, 1154, 3616, 3742];
    private readonly int _level;
    private readonly ushort[][] _hghts;
    private readonly ushort[][] _dirtyHghts;
    private readonly Material[][] _mates;
    private readonly Material[][] _dirtyMates;
    private readonly List<GrassExtm[]> _grass = new();
    private readonly List<GrassExtm[]> _dirtyGrass = new();
    private readonly List<WaterExtm[]> _water = new();
    private readonly List<WaterExtm[]> _dirtyWater = new();

    private bool loadFinished = false;

    public Lod(int level)
    {
        _level = level;
        int dim = 1 << level;

        using (Profiler.BeginZone("AllocLODLevel")) {
            _hghts = new ushort[dim * dim][];
            _dirtyHghts = new ushort[dim * dim][];
            
            _mates = new Material[dim * dim][];
            _dirtyMates = new Material[dim * dim][];
            
            CollectionsMarshal.SetCount(_grass, dim * dim);
            CollectionsMarshal.SetCount(_dirtyGrass, dim * dim);
            
            CollectionsMarshal.SetCount(_water, dim * dim);
            CollectionsMarshal.SetCount(_dirtyWater, dim * dim);
        }
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
                    using (var s = Sarc.FromBinary(result.Ok()))
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
                    result.Ok().Dispose();
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
                    Sarc s = Sarc.FromBinary(result.Ok());
                    foreach (var (name, data) in s)
                    {
                        var tileId = ZOrder.IndexFromFilename(name);
                        if (tileId.IsErr()) continue;
                        using (Profiler.BeginZone("MATE_ToArray")) {
                            _mates[tileId.Unwrap()] = data.AsSpan().Cast<byte, Material>().ToArray();
                        }
                    }
                    s.Dispose();
                    result.Ok().Dispose();
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
                    Sarc s = Sarc.FromBinary(result.Ok());
                    foreach (var (name, data) in s)
                    {
                        var tileId = ZOrder.IndexFromFilename(name);
                        if (tileId.IsErr()) continue;
                        _grass[tileId.Unwrap()] = data.AsSpan().Cast<byte, GrassExtm>().ToArray();
                    }
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
                    Sarc s = Sarc.FromBinary(result.Ok());
                    foreach (var (name, data) in s)
                    {
                        var tileId = ZOrder.IndexFromFilename(name);
                        if (tileId.IsErr()) continue;
                        _water[tileId.Unwrap()] = data.AsSpan().Cast<byte, WaterExtm>().ToArray();
                    }
                }
            }));
        }
        
        await Task.WhenAll(tasks);
    }

    public Result<ushort[], (ushort, ushort)> GetHeightmapTile(ushort tileId)
    {
        WaitForLoad();
        using (Profiler.BeginZone("LodGetHGHT")) {
            if (_dirtyHghts.Length > tileId && _dirtyHghts[tileId] != null) {
                return _dirtyHghts[tileId];
            }
            if (_hghts.Length > tileId && _hghts[tileId] != null) {
                return _hghts[tileId];
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
            if (_dirtyMates[tileId] != null) {
                return _dirtyMates[tileId];
            }
            if (_mates[tileId] != null) {
                return _mates[tileId];
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
        if (_dirtyGrass[tileId] != null) {
            return _dirtyGrass[tileId];
        }
        if (_grass[tileId] != null) {
            return _grass[tileId];
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
        if (_dirtyWater[tileId] != null) {
            return _dirtyWater[tileId];
        }
        if (_water[tileId] != null) {
            return _water[tileId];
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
}
