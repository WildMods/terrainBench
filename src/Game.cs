using CsOead;

namespace terrainBench;

public class Game
{
    private readonly Dump _base;
    private readonly Dump _update;
    private readonly Dump _dlc;

    public Game(string basePath, string updatePath, string dlcPath)
    {
        _base = new(basePath);
        _update = new(updatePath);
        _dlc = new(dlcPath);
    }

    public IEnumerable<(string, Sarc)> GetLod(int level)
    {
        IEnumerable<(string, byte[])>[] iters = [
            _base.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.hght.sstera"),
            _base.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.mate.sstera"),
            _update.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.grass.extm.sstera"),
            _update.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.water.extm.sstera")
        ];
        foreach (var iter in iters)
        {
            foreach (var (name, data) in iter)
            {
                Yaz0.TryDecompress(data, out var bytes);
                yield return (name.Replace(".sstera", ""), Sarc.FromBinary(bytes));
            }
        }
    }
}