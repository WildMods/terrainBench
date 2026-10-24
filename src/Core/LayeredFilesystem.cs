using System.Collections.Concurrent;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench.Core;

/// <summary>
/// A view of several folders unified as if they were a single filesystem.
/// </summary>
public class LayeredFilesystem {
    List<string> layers = new();
    ConcurrentDictionary<string, int> knownPaths = new();

    private string BuildPathInLayer(string path, int layer) {
        return Path.Combine(layers[layer], path);
    }

    /// <summary>
    /// Initialize the filesystem view from a set of directories
    /// </summary>
    /// <param name="initialLayers">The directories to register as layers.
    /// Earlier layers take priority over later layers.</param>
    public LayeredFilesystem(IEnumerable<string> initialLayers) {
        layers = initialLayers.ToList();
    }

    /// <summary>
    /// Cache the final location of a file, or of all the files in a directory.
    /// This speeds up future lookups to this file or directory.
    /// </summary>
    /// <param name="path">The file or directory to index</param>
    public void PreIndexPath(string path) {
        for (int i = 0; i < layers.Count; i++) {
            var finalpath = BuildPathInLayer(path, i);
            if (File.Exists(finalpath)) {
                knownPaths[path] = i;
            }
            else if (Directory.Exists(finalpath)) {
                foreach (var f in Directory.EnumerateFiles(finalpath)) {
                    var filename = Path.Combine(path, Path.GetFileName(f));
                    knownPaths[filename] = i;
                }
            }
        }
    }

    /// <summary>
    /// Add a new layer to the bottom layer of the view (i.e. all previously
    /// added layers will be checked for files before this new layer is checked)
    /// </summary>
    public void AddLayer(string path) {
        knownPaths.Clear();
        layers.Add(path);
    }
    
    /// <summary>Find the real path of a file on disk.</summary>
    /// <param name="path">The filepath to search for within the registered folders</param>
    public Result<string, ErrorStack> ResolvePath(string path) {
        using (var z = Profiler.BeginZone("LayeredFilesystem.ResolvePath")) {
            z.EmitText(path);
            // Try to pull from cache
            bool exists = false;
            int layerID = -1;
            exists = knownPaths.TryGetValue(path, out layerID);
            if (exists) {
                return BuildPathInLayer(path, layerID);
            }

            for (int i = 0; i < layers.Count; i++) {
                var fullPath = BuildPathInLayer(path, i);
                if (File.Exists(fullPath)) {
                    // Found it, cache result
                    knownPaths[path] = i;

                    return fullPath;
                }
            }
            return Err(new ErrorStack($"Failed to find {path} in any filesystem layer."));
        }
    }

    /// <summary>Map a file from the layered view into memory</summary>
    public Result<MappedFile, ErrorStack> MapPath(string path) {
        var pathRes = ResolvePath(path);
        if (pathRes.IsErr()) {
            return Err(pathRes.Err().Context("Failed to map the file."));
        }

        var fullPath = pathRes.Unwrap();
        return MappedFile.Create(fullPath);
    }
}