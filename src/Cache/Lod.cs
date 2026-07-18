using CommunityToolkit.HighPerformance;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench.Cache;

public class Lod
{
    private static readonly short[] TILE_COUNTS = [1, 4, 16, 36, 144, 320, 1154, 3616, 3742];
    private readonly int _level;
    private readonly Dictionary<ushort, ushort[]> _hghts;
    private readonly Dictionary<ushort, ushort[]> _dirtyHghts;
    private readonly Dictionary<ushort, ushort[]> _mates;
    private readonly Dictionary<ushort, ushort[]> _dirtyMates;
    private readonly Dictionary<ushort, ushort[]> _grass;
    private readonly Dictionary<ushort, ushort[]> _dirtyGrass;
    private readonly Dictionary<ushort, ushort[]> _water;
    private readonly Dictionary<ushort, ushort[]> _dirtyWater;

    public Lod(int level, Game game)
    {
        _level = level;
        _hghts = new Dictionary<ushort, ushort[]>(TILE_COUNTS[_level]);
        _dirtyHghts = [];
        foreach (var sarc in game.GetLodHght(_level))
        {
            if (sarc.IsErr()) continue;
            foreach (var (name, data) in sarc.Unwrap())
            {
                var tileId = ZOrder.IndexFromFilename(name);
                if (tileId.IsErr()) continue;
                _hghts[tileId.Unwrap()] = data.AsSpan().Cast<byte, ushort>().ToArray();
            }
        }
    }

    public Result<ushort[], (ushort, ushort)> GetHght(ushort tileId)
    {
        if (_dirtyHghts.TryGetValue(tileId, out var result) || _hghts.TryGetValue(tileId, out result))
        {
            return result;
        }

        var newId = (ushort)(tileId >> 2);
        var section = (ushort)(tileId & 0b11);
        return Err((newId, section));
    }

    public void InsertHghtData(ushort tileId, ushort[] hghtData)
    {
        _dirtyHghts.Add(tileId, hghtData);
    }
}