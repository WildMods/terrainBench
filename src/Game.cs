// Created Jul. 15 2026
// @author Ginger
using System.Collections.Concurrent;
using Native.IO.Handles;
using OperationResult;
using static OperationResult.Helpers;
using CsOead;

namespace terrainBench;

public enum LodComponent
{
    // ReSharper disable InconsistentNaming
    hght,
    mate,
    grass,
    water
    // ReSharper restore InconsistentNaming
}

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

    public Result<DataMarshal, ErrorStack> BaseGameDecompressed(string relativePath) {
        return _base.GetDecompressed(relativePath);
    }


    public IEnumerable<(string, Sarc)> GetLod(int level)
    {
        IEnumerable<string>[] iters = [
            _base.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.hght.sstera"),
            _base.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.mate.sstera"),
            _update.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.grass.extm.sstera"),
            _update.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.water.extm.sstera")
        ];

        var YazWatch = System.Diagnostics.Stopwatch.StartNew();
        YazWatch.Stop();
        foreach (var iter in iters) {
            var mutex = new Mutex();
            var buffers = new List<SSTERARecord>();
            YazWatch.Start();
            Parallel.ForEach(iter, path => {
                    DataMarshal data = Yaz0.DecompressFile(path);
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

        }

        Console.WriteLine("Spent {0}ms decompressing Yaz0 from disk", YazWatch.ElapsedMilliseconds);
    }
    
    public IEnumerable<Result<Sarc, ErrorStack>> IterLodComponent(int level, LodComponent component)
    {
        ConcurrentBag<Result<Sarc, ErrorStack>> bag = [];
        Parallel.ForEach(
            _base.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.{component.ToString()}.sstera"),
            path => {
                DataMarshal data = Yaz0.DecompressFile(path);
                if (data.AsSpan().Length > 0) {
                    bag.Add(Ok(Sarc.FromBinary(data)));
                }
                else
                {
                    bag.Add(Err(new ErrorStack($"Failed to decompress {Path.GetFileName(path)}")));
                }
            }
        );

        foreach (var result in bag)
        {
            yield return result;
        }
    }
}
