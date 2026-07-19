using CommunityToolkit.HighPerformance;
using CsOead;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench.Cache;

public class Lod
{
    private static readonly short[] TILE_COUNTS = [1, 4, 16, 36, 144, 320, 1154, 3616, 3742];
    private readonly int _level;
    private readonly Dictionary<ushort, ushort[]>[] _components;
    private readonly Dictionary<ushort, ushort[]>[] _dirtyComponents;

    public Lod(int level, Game game)
    {
        _level = level;
        var count = TILE_COUNTS[_level];
        _components = [new(count), new(count), new(count), new(count)];
        _dirtyComponents = [new(), new(), new(), new()];
        (IEnumerable<Result<Sarc, ErrorStack>>, Dictionary<ushort, ushort[]>)[] groups = [
            (game.IterLodComponent(_level, LodComponent.hght), _components[0]),
            (game.IterLodComponent(_level, LodComponent.mate), _components[1]),
            (game.IterLodComponent(_level, LodComponent.grass), _components[2]),
            (game.IterLodComponent(_level, LodComponent.water), _components[3])
        ];
        Parallel.ForEach(
            groups,
            group =>
            {
                var (iter, collection) = group;
                foreach (var result in iter)
                {
                    if (result.IsErr()) continue;
                    foreach (var (name, data) in result.Unwrap())
                    {
                        var tileId = ZOrder.IndexFromFilename(name);
                        if (tileId.IsErr()) continue;
                        collection[tileId.Unwrap()] = data.AsSpan().Cast<byte, ushort>().ToArray();
                    }
                }
            }
        );
    }

    public Result<ushort[], (ushort, ushort)> GetComponentTile(LodComponent component, ushort tileId)
    {
        if (_dirtyComponents[(int)component].TryGetValue(tileId, out var result) ||
            _components[(int)component].TryGetValue(tileId, out result))
        {
            return result;
        }

        var newId = (ushort)(tileId >> 2);
        var section = (ushort)(tileId & 0b11);
        return Err((newId, section));
    }

    public void InsertComponentData(LodComponent component, ushort tileId, ushort[] data)
    {
        _dirtyComponents[(int)component].Add(tileId, data);
    }
}