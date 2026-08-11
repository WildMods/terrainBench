// Created Jul. 15 2026
// @author Ginger
using CsOead;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench;

/// <summary>
/// Represents a game folder in the game dump (i.e. base, update, or DLC folder)
/// </summary>
public class Dump
{
    private string _path;

    public Dump(string path) {
        _path = path;
    }

    /// <summary>
    /// Open a game file for writing
    /// </summary>
    public Result<Stream, ErrorStack> OpenWrite(string loose) {
        string baseErr = $"Unable to open ${loose} for writing: ";
        try {
            var f = File.OpenWrite(loose);
            return f;
        } catch (DirectoryNotFoundException) {
            return Err(new ErrorStack($"${baseErr} The containing directory couldn't be found"));
        } catch (UnauthorizedAccessException) {
            return Err(new ErrorStack($"${baseErr} Unauthorized access"));
        } catch (PathTooLongException) {
            return Err(new ErrorStack($"${baseErr} path too long"));
        } catch (Exception e) {
            return Err(new ErrorStack($"${baseErr} '${e.Message}'"));
        }
    }

    /// <summary>
    /// Read and decompress a loose file
    /// </summary>
    public Result<Native.IO.Handles.DataMarshal, ErrorStack> GetDecompressed(string loose)
    {
        string path = Path.Combine(_path, loose);
        var data = Yaz0.DecompressFile(path);
        if (data.AsSpan().Length == 0) {
            return Err(new ErrorStack($"Unable to map & decompress '{path}'"));
        }
        return data;
    }

    /// <summary>
    /// Read a loose file
    /// </summary>
    public RefResult<Span<byte>, ErrorStack> GetFile(string loose)
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
        return Yaz0.TryDecompress(span, out var data) ? Ok(data!.AsSpan()) : Ok(span);
    }

    /// <summary>
    /// Read a file inside a SARC. The path to the SARC is specified normally,
    /// but folders inside the SARC use a double slash.
    /// e.g. "Pack/TitleBG.pack//Model//Link.sbfres"
    /// </summary>
    public RefResult<Span<byte>, ErrorStack> GetNestedFile(string relative)
    {
        string[] parts = relative.Split("//", 2);
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
        
        var nest = parts[1].Replace("//", "/");
        // Handle compressed and uncompressed SARCs
        var sarcData = span;
        if (Yaz0.TryDecompress(span, out var data)) {
            sarcData = data.AsSpan();
        }
        try
        {
            sarc = Sarc.FromBinary(sarcData);
        }
        catch (Exception e)
        {
            return Err(new ErrorStack(e.Message, e.StackTrace));
        }
        if (!sarc.TryGetFile(nest, out span))
        {
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

    public IEnumerable<string> GlobFilesInFolder(string folder, string pattern)
    {
        var _files = Directory.EnumerateFiles(Path.Join(_path, folder), pattern, SearchOption.AllDirectories);
        foreach (var file in _files)
        {
            yield return file;
        }
    }

    public IEnumerable<string> GetAllFilesInFolder(string folder)
    {
        return GlobFilesInFolder(folder, "*");
    }
}
