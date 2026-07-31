// Created Jul. 15 2026
// @author Ginger
using System.Collections.Concurrent;
using Native.IO.Handles;
using OperationResult;
using static OperationResult.Helpers;
using CsOead;
using System.Diagnostics;

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
    /// <summary>
    /// Specifies a game folder to use for file operations
    /// </summary>
    public enum Section
    {
        [System.ComponentModel.Description("base game")]
        Base,
        [System.ComponentModel.Description("update")]
        Update,
        DLC, // This can stay capitalized
    }
    
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

    // Get a specific game folder by enum
    private Dump GetDump(Section s)
    {
        bool valid = true;
        var res = s switch
        {
            Section.Base => _base,
            Section.Update => _update,
            Section.DLC => _dlc,
            _ => new Dump($"${valid = false}"), // Sorry this is kinda janky -- torf
        };
        Debug.Assert(valid);
        return res;
    }
    
    /// <summary>
    /// Decompress a game file into memory
    /// </summary>
    /// <param name="s">The game folder to look for the file in</param>
    public Result<DataMarshal, ErrorStack> ReadDecompressed(string relativePath, Section s) {
        using (var z = Profiler.BeginZone("ReadDecompressed")) {
            z.EmitText(relativePath);
            return GetDump(s).GetDecompressed(relativePath);
        }
    }
    
    public RefResult<Span<byte>, ErrorStack> ReadFile(string relativePath, Section s) {
        using (var z = Profiler.BeginZone("ReadFile")) {
            z.EmitText(relativePath);
            var d = GetDump(s);
            if (relativePath.Contains("//")) {
                return d.GetNestedFile(relativePath);
            } else {
                return d.GetFile(relativePath);
            }
        }
    }

    /// <summary>
    /// Open a game file for writing
    /// </summary>
    /// <param name="s">The game folder to look for the file in</param>
    public Result<Stream, ErrorStack> OpenWrite(string loose, Section s) {
        var res = GetDump(s).OpenWrite(loose);
        if (res.IsErr()) {
            return res.Context($"Unable to open '${loose}' from the ${s} folder");
        }
        return res;
    }

    /// <summary>
    /// Iterate over all SSTERAs in this order: [HGHT, MATE, GRASS, WATER]
    /// </summary>
    public IEnumerable<(string, Sarc)> GetLod(int level)
    {
        IEnumerable<string>[] iters = [
            _base.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.hght.sstera"),
            _base.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.mate.sstera"),
            _update.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.grass.extm.sstera"),
            _update.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.water.extm.sstera")
        ];

        var YazWatch = Stopwatch.StartNew();
        YazWatch.Stop();
        foreach (var iter in iters) {
            var mutex = new Mutex();
            var buffers = new List<SSTERARecord>();
            
            // It's a bit faster to just decompress everything at once
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
    
    /// <summary>
    /// Iterate over all decompressed SSTERAs of a specific LOD and type
    /// </summary>
    public IEnumerable<Result<DataMarshal, ErrorStack>> IterLodComponent(int level, LodComponent component)
    {
        using (Profiler.BeginZone("IterLodComponent")) {
            ConcurrentBag<DataMarshal> buffers = [];
            var iter = _base.GlobFilesInFolder("Terrain/A/MainField", $"5{level}0000????.{component.ToString()}.sstera");

            // It's a bit faster to just decompress everything at once
            var YazWatch = Stopwatch.StartNew();
            Parallel.ForEach(iter, path => {
                using (var z = Profiler.BeginZone("oead::DecompressFile")) {
                    z.EmitText(path);
                    DataMarshal data = Yaz0.DecompressFile(path);
                    if (data.AsSpan().Length > 0)
                    {
                        buffers.Add(data);
                    }
                }
            });
            YazWatch.Stop();

            foreach (var data in buffers) {
                yield return Ok(data);
            }
        }
    }
}
