// Created Jul. 15 2026
// @author Ginger
using CsOead;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench.Core;

public static class Dump
{
    /// <summary>Read a file inside a SARC.</summary>
    public static RefResult<Span<byte>, ErrorStack> GetNestedFile(string relative, string nested)
    {
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
