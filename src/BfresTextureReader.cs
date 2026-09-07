using System.Diagnostics;
using BfresLibrary;
using Native.IO.Handles;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench;
using Core;

/// <summary>
/// Wrapper to handle reading textures from Wii U and Switch files, which have
/// different names and split mipmaps differently.
/// </summary>
public class BfresTextureReader {
    public byte[] deswizzledBase = Array.Empty<byte>();
    public byte[] deswizzledMip = Array.Empty<byte>();

    public List<byte[]> deswizzledMips = new List<byte[]>();

    public int width = 0;
    public int height = 0;
    public int arrayLength = 0;
    public int mipCount = 0;

    BfresTextureReader(Span<byte> baseData, string texName) {
        var tex = BFRESSwitch.GetBNTXTexture(baseData, texName);
        if (tex is null) {
            Console.WriteLine("Couldn't find textures!");
        }
        width = tex.Width;
        height = tex.Height;
        arrayLength = tex.ArrayLength;
        mipCount = tex.MipCount;

        for (int i = 0; i < mipCount; i++) {
            var deswizzledLevel = tex.GetDeswizzledDataForEntireLevel(i, out var levelSize);
            deswizzledMips.Add(deswizzledLevel);
        }
        
    }
    
    BfresTextureReader(Span<byte> baseData, Span<byte> mipData, string texName) {
        var ftexRes = BFRESWiiU.GetFTEXByName(baseData, texName);
        if (ftexRes.IsErr()) {
            Console.WriteLine("Couldn't read texture metadata.");
            Console.WriteLine(ftexRes.Err());
            return;
        }
        
        var ftex = ftexRes.Unwrap();
        width  = (int)ftex.width;
        height = (int)ftex.height;
        arrayLength = (int)ftex.arrayLength;
        mipCount    = (int)ftex.mipCount;
        
        var deswizzleTime = Stopwatch.StartNew();
        for (int i = 0; i < mipCount; i++) {
            var buf = (i == 0) ? baseData : mipData;
            var deswizzledLevel = GetDeswizzled(texName, buf, i);
            deswizzledMips.Add(deswizzledLevel);
        }
        
        deswizzleTime.Stop();
        Console.WriteLine("Loaded base textures in {0}ms", deswizzleTime.ElapsedMilliseconds);
    }
    
    /// <param name="basePath">e.g. "Model/Terrain" can read "Model/Terrain.Tex.sbfres",
    /// "Model/Terrain.Tex1.sbfres", or "model/Terrain.Tex2.sbfres"</param>
    public static Result<BfresTextureReader, ErrorStack> Create(Game game, string texPath, string tex1Path, string tex2Path, string texName) {
        // var texPath = $"{basePath}.Tex.sbfres";
        // var tex1Path = $"{basePath}.Tex1.sbfres";
        // var tex2Path = $"{basePath}.Tex2.sbfres";

        try {
            var texData = game.ReadFile(texPath, Game.Section.Base);
            if (texData.IsOk()) {
                BFRESSwitch.ParseBFRES(texData.Unwrap());
                return new BfresTextureReader(texData.Unwrap(), texName);
            } else {
                Console.WriteLine("Unable to find .Tex file:\n{0}", texData.Err()?.Message);
            }
        } catch (Exception e) {
            Console.WriteLine(e);
            return Err(new ErrorStack("Failed to load terrain textures from Switch files", e));
        }
        
        var tex1Data = game.ReadDecompressed(tex1Path, Game.Section.Base);
        var tex2Data = game.ReadFile(tex2Path, Game.Section.Base);
        if (tex1Data.IsErr() && tex2Data.IsErr()) {
            return Err(new ErrorStack($"Unable to find '{texPath}', '{tex1Path}', or '{tex2Path}'. Are your dump paths correct?"));
        } else if (tex1Data.IsErr()) {
            Debug.Assert(tex2Data.IsOk());
            return Err(new ErrorStack($"Found a Wii U Tex2 file, but unable to find '{tex1Path}'."));
        } else if (tex2Data.IsErr()) {
            Debug.Assert(tex1Data.IsOk());
            return Err(new ErrorStack($"Found a Wii U Tex1 file, but unable to find '{tex2Path}'"));
        }

        try {
            return new BfresTextureReader(tex1Data.Unwrap(), tex2Data.Unwrap(), texName);
        } catch (Exception ex) {
            Console.WriteLine(ex.Message);
            return Err(new ErrorStack("Failed to load terrain textures", ex));
        }
        
    }
    
    public static byte[] GetDeswizzled(string name, ReadOnlySpan<byte> data, int mipLevel) {
        return BFRESWiiU.GetDeswizzledByName(name, data, mipLevel);
    }
}
