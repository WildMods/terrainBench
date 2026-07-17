// Created Jul. 15 2026
// @author Ginger
using Native.IO.Handles;
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

    private struct SSTERARecord {
        public string path;
        public DataMarshal buf;
        public SSTERARecord(string path, DataMarshal buf) {
            this.path = path;
            this.buf = buf;
        }
    };


    public IEnumerable<(string, Sarc)> GetLod(int level)
    {
        IEnumerable<(string, string)>[] iters = [
            _base.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.hght.sstera"),
            _base.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.mate.sstera"),
            _update.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.grass.extm.sstera"),
            _update.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.water.extm.sstera")
        ];

        var paths = new List<string>();
        foreach (var iter in iters) {
            foreach (var (name, path) in iter) {
                paths.Add(path);
            }
        }

        var mutex = new Mutex();
        var buffers = new List<SSTERARecord>();
        var YazWatch = System.Diagnostics.Stopwatch.StartNew();
        Parallel.ForEach(paths, path => {
                DataMarshal data = Yaz0.DecompressFile(path);
                Console.WriteLine("Decompressed {0}", path);
                if (data.AsSpan().Length > 0) {
                    mutex.WaitOne();
                    buffers.Add(new SSTERARecord(path, data));
                    mutex.ReleaseMutex();
                }
        });
        YazWatch.Stop();

        foreach (var record in buffers) {
            string basename = Path.GetFileName(record.path);
            yield return (basename.Replace(".sstera", ""), Sarc.FromBinary(record.buf));
        }

        Console.WriteLine("Spent {0}ms decompressing Yaz0 from disk", YazWatch.ElapsedMilliseconds);
    }
}
