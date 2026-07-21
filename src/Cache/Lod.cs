using CommunityToolkit.HighPerformance;
using OperationResult;
using terrainBench.LodComponents;
using static OperationResult.Helpers;

namespace terrainBench.Cache;

public class Lod
{
    private static readonly short[] TILE_COUNTS = [1, 4, 16, 36, 144, 320, 1154, 3616, 3742];
    private readonly int _level;
    private readonly Dictionary<ushort, ushort[]> _hghts;
    private readonly Dictionary<ushort, ushort[]> _dirtyHghts;
    private readonly Dictionary<ushort, Material[]> _mates;
    private readonly Dictionary<ushort, Material[]> _dirtyMates;
    private readonly Dictionary<ushort, GrassExtm[]> _grass;
    private readonly Dictionary<ushort, GrassExtm[]> _dirtyGrass;
    private readonly Dictionary<ushort, WaterExtm[]> _water;
    private readonly Dictionary<ushort, WaterExtm[]> _dirtyWater;

    public Lod(int level, Game game)
    {
        _level = level;
        _hghts = [];
        _dirtyHghts = [];
        _mates = [];
        _dirtyMates = [];
        _grass = [];
        _dirtyGrass = [];
        _water = [];
        _dirtyWater = [];
        var tasks = new List<Task>(4)
        {
            LoadHeightmapTiles(game),
            LoadMaterialTiles(game),
            LoadGrassTiles(game),
            LoadWaterTiles(game)
        };
        Task.WaitAll(tasks);
    }

    private async Task LoadHeightmapTiles(Game game)
    {
        var iter = game.IterLodComponent(_level, LodComponent.hght);
        var tasks = new List<Task>(TILE_COUNTS[_level]);
        
        foreach (var result in iter)
        {
            tasks.Add(Task.Run(delegate
            {
                if (result.IsErr()) return;
                foreach (var (name, data) in result.Unwrap())
                {
                    var tileId = ZOrder.IndexFromFilename(name);
                    if (tileId.IsErr()) continue;
                    _hghts[tileId.Unwrap()] = data.AsSpan().Cast<byte, ushort>().ToArray();
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
            tasks.Add(Task.Run(delegate
            {
                if (result.IsErr()) return;
                foreach (var (name, data) in result.Unwrap())
                {
                    var tileId = ZOrder.IndexFromFilename(name);
                    if (tileId.IsErr()) continue;
                    _mates[tileId.Unwrap()] = data.AsSpan().Cast<byte, Material>().ToArray();
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
                if (result.IsErr()) return;
                foreach (var (name, data) in result.Unwrap())
                {
                    var tileId = ZOrder.IndexFromFilename(name);
                    if (tileId.IsErr()) continue;
                    _grass[tileId.Unwrap()] = data.AsSpan().Cast<byte, GrassExtm>().ToArray();
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
                if (result.IsErr()) return;
                foreach (var (name, data) in result.Unwrap())
                {
                    var tileId = ZOrder.IndexFromFilename(name);
                    if (tileId.IsErr()) continue;
                    _water[tileId.Unwrap()] = data.AsSpan().Cast<byte, WaterExtm>().ToArray();
                }
            }));
        }
        
        await Task.WhenAll(tasks);
    }

    public Result<ushort[], (ushort, ushort)> GetHeightmapTile(ushort tileId)
    {
        if (_dirtyHghts.TryGetValue(tileId, out var result) || _hghts.TryGetValue(tileId, out result))
        {
            return result;
        }

        var newId = (ushort)(tileId >> 2);
        var section = (ushort)(tileId & 0b11);
        return Err((newId, section));
    }

    public void InsertTile(ushort tileId, ushort[] data)
    {
        _dirtyHghts.Add(tileId, data);
    }

    public Result<Material[], (ushort, ushort)> GetMaterialTile(ushort tileId)
    {
        if (_dirtyMates.TryGetValue(tileId, out var result) || _mates.TryGetValue(tileId, out result))
        {
            return result;
        }

        var newId = (ushort)(tileId >> 2);
        var section = (ushort)(tileId & 0b11);
        return Err((newId, section));
    }

    public void InsertTile(ushort tileId, Material[] data)
    {
        _dirtyMates.Add(tileId, data);
    }

    public Result<GrassExtm[], (ushort, ushort)> GetGrassTile(ushort tileId)
    {
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
        _dirtyGrass.Add(tileId, data);
    }

    public Result<WaterExtm[], (ushort, ushort)> GetWaterTile(ushort tileId)
    {
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
        _dirtyWater.Add(tileId, data);
    }
}