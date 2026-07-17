// Created Jul. 15 2026
// @author Ginger
using CsOead;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench;

public class Dump
{
    private string _path;

    public Dump(string path) {
        _path = path;
    }

    public Result<byte[], ErrorStack> GetFile(string loose)
    {
        if (loose.Contains("//"))
        {
            return Err(new ErrorStack($"loose path must be loose file. Use GetNestedFile for nested files: {loose}"));
        }
        var path = Path.Combine(_path, loose);
        if (!File.Exists(path))
        {
            return Err(new ErrorStack($"Loose file not found: {loose}"));
        }
        
        Span<byte> span = File.ReadAllBytes(path);
        return Yaz0.TryDecompress(span, out var data) ? Ok(data!.ToArray()) : Ok(span.ToArray());
    }

    public Result<byte[], ErrorStack> GetNestedFile(string relative)
    {
        string[] parts = relative.Split("//");
        if (parts.Length == 1)
        {
            return Err(new ErrorStack($"relative path must be nested file. Use GetFile for loose files: {relative}"));
        }
        string loose = Path.Combine(_path, parts[0]);
        if (!File.Exists(loose))
        {
            return Err(new ErrorStack($"Loose file not found: {loose}"));
        }

        Sarc sarc;
        Span<byte> span = File.ReadAllBytes(loose);
        foreach (var nest in parts.Skip(1))
        {
            Yaz0.TryDecompress(span, out var data);
            try
            {
                sarc = Sarc.FromBinary(data);
            }
            catch (Exception e)
            {
                return Err(new ErrorStack(e.Message, e.StackTrace));
            }
            if (!sarc.TryGetFile(nest, out span))
            {
                return Err(new ErrorStack($"Nested file not found: {nest}"));
            }
        }

        return Ok(span.ToArray());
    }

    public IEnumerable<(string, byte[])> GlobFilesInFolder(string folder, string pattern)
    {
        var _files = Directory.EnumerateFiles(Path.Join(_path, folder), pattern, SearchOption.AllDirectories);
        foreach (var file in _files)
        {
            yield return (Path.GetFileName(file), File.ReadAllBytes(file));
        }
    }

    public IEnumerable<(string, byte[])> GetAllFilesInFolder(string folder)
    {
        return GlobFilesInFolder(folder, "*");
    }
}
