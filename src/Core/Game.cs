// Created Jul. 15 2026
// @author Ginger
using System.Collections.Concurrent;
using Native.IO.Handles;
using OperationResult;
using static OperationResult.Helpers;
using CsOead;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using CommunityToolkit.HighPerformance;

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
    // We build a set of known game file paths and what folder they belong to.
    // Instead of checking each game folder for each file, we look up the
    // canonical path here and find out which folder (if any) to find it in.
    private LayeredFilesystem layerFS;
    public string _basePath;
    public string _updatePath;
    public string _dlcPath;
    public string modPath;
    
    public Game(string basePath, string updatePath, string dlcPath, string modPath)
    {
        _basePath = basePath;
        _updatePath = updatePath;
        _dlcPath = dlcPath;
        this.modPath = modPath;
        layerFS = new([modPath, dlcPath, updatePath, basePath]);
        IndexDefaultPaths();
    }

    public void IndexDefaultPaths() {
        string[] paths = { "Terrain/A/MainField" };
        using (Profiler.BeginZone("IndexDefaultPaths")) {
            foreach (var p in paths) {
                layerFS.PreIndexPath(p);
            }
        }
    }
    
    /// <summary>Read a (potentially compressed) file inside a SARC.</summary>
    public static RefResult<Span<byte>, ErrorStack> GetNestedFile(string relative, string nested) {
        if (!File.Exists(relative)) {
            return Err(new ErrorStack($"Loose file not found: {relative}"));
        }

        Sarc sarc;
        Span<byte> span = File.ReadAllBytes(relative);
        
        var nest = nested.Replace("//", "/");
        // Handle compressed and uncompressed SARCs
        var sarcData = span;
        if (Yaz0.TryDecompress(span, out var data)) {
            sarcData = data.AsSpan();
        } try {
            sarc = Sarc.FromBinary(sarcData);
        } catch (Exception e) {
            return Err(new ErrorStack(e.Message, e.StackTrace));
        }
        if (!sarc.TryGetFile(nest, out span)) {
            return Err(new ErrorStack($"Nested file not found: {nest}"));
        }
        
        // Handle compressed data inside a SARC
        var decompressedData = Yaz0.Decompress(span);
        if (decompressedData.Length > 0) {
            span = decompressedData;
        }
        sarc.Clear();
        sarc.Close();

        return Ok(span);
    }

    /// <summary>
    /// Decompress a game file into memory
    /// </summary>
    /// <param name="s">The game folder to look for the file in</param>
    public Result<DataMarshal, ErrorStack> ReadDecompressed(string relativePath) {
        using (var z = Profiler.BeginZone("ReadDecompressed")) {
            z.EmitText(relativePath);
            var finalpath = layerFS.ResolvePath(relativePath);
            if (finalpath.IsErr()) {
                return Err(finalpath.Err().Context("Failed to read compressed file"));
            }
            
            var data = Yaz0.DecompressFile(finalpath.Unwrap());
            if (data.AsSpan().Length == 0) {
                return Err(new ErrorStack($"Unable to map & decompress '{finalpath}'"));
            }
            return data;
        }
    }
    
    public RefResult<Span<byte>, ErrorStack> ReadFile(string relativePath) {
        using (var z = Profiler.BeginZone("ReadFile")) {
            z.EmitText(relativePath);
            bool isNested = relativePath.Contains("//");
            var loose = relativePath;
            string nestedPart = "";
            
            if (isNested) {
                var parts = relativePath.Split("//", 2);
                loose = parts[0];
                nestedPart = parts[1];
            }
            
            var resolveRes = layerFS.ResolvePath(loose);
            if (resolveRes.IsErr()) {
                return Err(resolveRes.Err().Context("Failed to read file"));
            }

            var finalpath = resolveRes.Unwrap();

            if (isNested) {
                return GetNestedFile(finalpath, nestedPart);
            }

            using (var z2 = Profiler.BeginZone("ReadAllBytes")) {
                var data = File.ReadAllBytes(finalpath);
                if (data.AsSpan().Length == 0) {
                    return Err(new ErrorStack($"Unable to load '{finalpath}'"));
                }

                return data;
            }
        }
    }

    /// <summary>
    /// Iterate over all SSTERAs of a specific LOD and type
    /// </summary>
    public IEnumerable<MappedFile> IterLodComponent(IEnumerable<UInt16> tiles, int level, LodComponent component)
    {
        using (Profiler.BeginZone("IterLodComponent")) {
            foreach (var idx in tiles) {
                var path = ZOrder.BuildFilename(idx, level, component.ToString());
                path = Path.Combine("Terrain/A/MainField", path + ".sstera");

                var mappedRes = layerFS.MapPath(path);
                if (mappedRes.IsErr()) {
                    continue;
                }

                yield return mappedRes.Unwrap();
            }
        }
    }
}
