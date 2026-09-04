// Created Jul. 15 2026
// @author Ginger
using System.Collections.Concurrent;
using Native.IO.Handles;
using OperationResult;
using static OperationResult.Helpers;
using CsOead;
using System.Diagnostics;

namespace terrainBench.Core;

public enum LodComponent
{
    // ReSharper disable InconsistentNaming
    hght,
    mate,
    [System.ComponentModel.Description("grass.extm")]
    grass,
    [System.ComponentModel.Description("water.extm")]
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
        Mod,
    }

    // We build a set of known game file paths and what folder they belong to.
    // Instead of checking each game folder for each file, we look up the
    // canonical path here and find out which folder (if any) to find it in.
    private ConcurrentDictionary<string, Section> fileIndex = new();
    private readonly string _basePath;
    private readonly string _updatePath;
    private readonly string _dlcPath;
    public string modPath;
    
    public Game(string basePath, string updatePath, string dlcPath, string modPath)
    {
        _basePath = basePath;
        _updatePath = updatePath;
        _dlcPath = dlcPath;
        this.modPath = modPath;
        IndexDefaultPaths();
    }

    string GetSectionPath(Section s) {
        string folder = s switch {
            Section.Base => _basePath,
            Section.Update => _updatePath,
            Section.DLC => _dlcPath,
            Section.Mod => modPath,
        };
        return folder;
    }

    string ResolveSectionPath(string path, Section s) {
        return Path.Combine(GetSectionPath(s), path);
    }

    private void IndexPath(string path, Section s) {
        string folder = s switch {
            Section.Base => _basePath,
            Section.Update => _updatePath,
            Section.DLC => _dlcPath,
            Section.Mod => modPath,
        };
        if (folder == null) {
            Console.WriteLine("{0} folder was null!", s.ToString());
            return;
        }
        
        var finalpath = Path.Combine(folder, path);
        if (File.Exists(finalpath)) {
            fileIndex[path] = s;
        } else if (Directory.Exists(finalpath)) {
            foreach (var f in Directory.EnumerateFiles(finalpath)) {
                var filename = Path.Combine(path, Path.GetFileName(f));
                fileIndex[filename] = s;
            }
        }
    }

    public void IndexDefaultPaths() {
        string[] paths = { "Terrain/A/MainField", "Model", "Pack/TitleBG.pack" };

        foreach (var p in paths) {
            IndexPath(p, Section.Base);
            IndexPath(p, Section.Update);
            IndexPath(p, Section.DLC);
            IndexPath(p, Section.Mod);
        }
    }

    /// <summary>
    /// Decompress a game file into memory
    /// </summary>
    /// <param name="s">The game folder to look for the file in</param>
    public Result<DataMarshal, ErrorStack> ReadDecompressed(string relativePath, Section s) {
        using (var z = Profiler.BeginZone("ReadDecompressed")) {
            z.EmitText(relativePath);
            var finalpath = ResolveSectionPath(relativePath, s);
            var data = Yaz0.DecompressFile(finalpath);
            if (data.AsSpan().Length == 0) {
                return Err(new ErrorStack($"Unable to map & decompress '{finalpath}'"));
            }
            return data;
        }
    }
    
    public RefResult<Span<byte>, ErrorStack> ReadFile(string relativePath, Section s) {
        using (var z = Profiler.BeginZone("ReadFile")) {
            z.EmitText(relativePath);
            var path = ResolveSectionPath(relativePath, s);
            if (relativePath.Contains("//")) {
                return Dump.GetNestedFile(GetSectionPath(s), relativePath);
            }
            
            var data = File.ReadAllBytes(path);
            if (data.AsSpan().Length == 0) {
                return Err(new ErrorStack($"Unable to load '{path}'"));
            }

            return data;
        }
    }

    /// <summary>
    /// Iterate over all decompressed SSTERAs of a specific LOD and type
    /// </summary>
    public IEnumerable<Result<DataMarshal, ErrorStack>> IterLodComponent(int level, LodComponent component)
    {
        using (Profiler.BeginZone("IterLodComponent")) {
            ConcurrentBag<DataMarshal> buffers = [];
            int minIdx = 0;
            int maxIdx = (1 << level) * (1 << level);
            
            // It's a bit faster to just decompress everything at once
            var YazWatch = Stopwatch.StartNew();
            Parallel.For(minIdx, maxIdx, i => {
                var path = ZOrder.BuildFilename((ushort)i, level, component.ToString());
                path = Path.Combine("Terrain/A/MainField", path + ".sstera");
                if (!fileIndex.TryGetValue(path, out var s)) {
                    return;
                }
                path = ResolveSectionPath(path, s);
                
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
