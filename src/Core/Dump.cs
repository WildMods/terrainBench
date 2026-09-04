// Created Jul. 15 2026
// @author Ginger
using CsOead;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench.Core;

/// <summary>
/// Represents a game folder in the game dump (i.e. base, update, or DLC folder)
/// </summary>
public class Dump
{
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
    /// Read a file inside a SARC. The path to the SARC is specified normally,
    /// but folders inside the SARC use a double slash.
    /// e.g. "Pack/TitleBG.pack//Model//Link.sbfres"
    /// </summary>
    public static RefResult<Span<byte>, ErrorStack> GetNestedFile(string sectionPath, string relative)
    {
        string[] parts = relative.Split("//", 2);
        if (parts.Length == 1)
        {
            return Err(new ErrorStack($"relative path must be nested file. Use GetFile for loose files: {relative}"));
        }
        string loose = Path.Combine(sectionPath, parts[0]);
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
}
