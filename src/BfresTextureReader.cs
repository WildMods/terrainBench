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

    public int width = 0;
    public int height = 0;
    public int arrayLength = 0;

    BfresTextureReader(Span<byte> baseData) {
        var tex = BFRESSwitch.GetBNTXTexture(baseData, "MaterialAlb");
        if (tex is null) {
            Console.WriteLine("Couldn't find textures!");
        }
        width = tex.Width;
        height = tex.Height;
        arrayLength = tex.ArrayLength;

        deswizzledBase = tex.GetDeswizzledDataForEntireLevel(0, out var baseLevelSize);
        deswizzledMip = new byte[(int)(deswizzledBase.Length * 1.5)];

        long posInMipBuffer = 0;
        for (int i = 1; i < tex.MipCount; i++) {
            Console.WriteLine("");
            long levelSize = tex.CalcLayerLinearSize(i) * tex.ArrayLength;
            var mipLevelData = new Span<byte>(deswizzledMip, (int)posInMipBuffer, (int)levelSize);

            tex.GetDeswizzledDataForEntireLevel(i, mipLevelData, out levelSize);
            posInMipBuffer += levelSize;
        }
        
    }
    
    BfresTextureReader(Span<byte> baseData, Span<byte> mipData) {
        string texName = "MaterialAlb";
        
        var baseRes = new ResFile(new MemoryStream(baseData.ToArray()));
        var t = baseRes.Textures[texName];
        width = (int)t.Width;
        height = (int)t.Height;
        arrayLength = (int)t.ArrayLength;
        
        var deswizzleTime = Stopwatch.StartNew();
        deswizzledBase = GetDeswizzled(texName, baseData, false);
        deswizzledMip = GetDeswizzled(texName, mipData, true);
        deswizzleTime.Stop();
        Console.WriteLine("Loaded base textures in {0}ms", deswizzleTime.ElapsedMilliseconds);
    }
    
    /// <param name="basePath">e.g. "Model/Terrain" can read "Model/Terrain.Tex.sbfres",
    /// "Model/Terrain.Tex1.sbfres", or "model/Terrain.Tex2.sbfres"</param>
    public static Result<BfresTextureReader, ErrorStack> Create(Game game, string texPath, string tex1Path, string tex2Path) {
        // var texPath = $"{basePath}.Tex.sbfres";
        // var tex1Path = $"{basePath}.Tex1.sbfres";
        // var tex2Path = $"{basePath}.Tex2.sbfres";

        try {
            var texData = game.ReadFile(texPath, Game.Section.Base);
            if (texData.IsOk()) {
                BFRESSwitch.ParseBFRES(texData.Unwrap());
                return new BfresTextureReader(texData.Unwrap());
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
            return new BfresTextureReader(tex1Data.Unwrap(), tex2Data.Unwrap());
        } catch (Exception ex) {
            Console.WriteLine(ex.Message);
            return Err(new ErrorStack("Failed to load terrain textures", ex));
        }
        
    }
    
    public static byte[] GetDeswizzled(string name, ReadOnlySpan<byte> data, bool mip) {
        return BFRESWiiU.GetDeswizzledByName(name, data, mip);
    }
}
