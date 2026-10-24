using System.Collections.Concurrent;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench.Core;

public class LayeredFilesystem {
    List<string> layers = new();
    ConcurrentDictionary<string, int> knownPaths = new();

    private string BuildPathInLayer(string path, int layer) {
        return Path.Combine(layers[layer], path);
    }

    public LayeredFilesystem(IEnumerable<string> initialLayers) {
        layers = initialLayers.ToList();
    }

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

    public void AddLayer(string path) {
        knownPaths.Clear();
        layers.Add(path);
    }
    
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

    public Result<MappedFile, ErrorStack> MapPath(string path) {
        var pathRes = ResolvePath(path);
        if (pathRes.IsErr()) {
            return Err(pathRes.Err().Context("Failed to map the file."));
        }

        var fullPath = pathRes.Unwrap();
        return MappedFile.Create(fullPath);
    }
}