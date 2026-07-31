using System.Diagnostics;
using BfresLibrary;
using Native.IO.Handles;
using OperationResult;
using static OperationResult.Helpers;
namespace terrainBench;

/// <summary>
/// Wrapper to handle reading textures from Wii U and Switch files, which have
/// different names and split mipmaps differently.
/// </summary>
public class BfresTextureReader {
    // A Wii U Tex1 file, or a Switch Tex file
    readonly ResFile baseRes;

    // If pulling from Wii U files, this will be the Tex2 BFRES (for mipmaps)
    readonly ResFile? mipRes = null;

    BfresTextureReader(Span<byte> baseData) {
        baseRes = new ResFile(new MemoryStream(baseData.ToArray()));
    }
    
    BfresTextureReader(Span<byte> baseData, Span<byte> mipData) {
        baseRes = new ResFile(new MemoryStream(baseData.ToArray()));
        mipRes = new ResFile(new MemoryStream(mipData.ToArray()));
    }
    
    /// <param name="basePath">e.g. "Model/Terrain" can read "Model/Terrain.Tex.sbfres",
    /// "Model/Terrain.Tex1.sbfres", or "model/Terrain.Tex2.sbfres"</param>
    public static Result<BfresTextureReader, ErrorStack> Create(Game game, string texPath, string tex1Path, string tex2Path) {
        // var texPath = $"{basePath}.Tex.sbfres";
        // var tex1Path = $"{basePath}.Tex1.sbfres";
        // var tex2Path = $"{basePath}.Tex2.sbfres";

        var texData = game.ReadFile(texPath, Game.Section.Base);
        if (texData.IsOk()) {
            return new BfresTextureReader(texData.Unwrap());
        } else {
            Console.WriteLine("Unable to find .Tex file:\n{0}", texData.Err()?.Message);
        }
        
        var tex1Data = game.ReadFile(tex1Path, Game.Section.Base);
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

        return new BfresTextureReader(tex1Data.Unwrap(), tex2Data.Unwrap());
    }

    public Result<TextureShared, ErrorStack> GetTexture(string name, int mipLevel) {
        Debug.Assert(baseRes != null);
        bool needMips = (mipLevel != 0);

        // Only get the mip file if we need mipmaps and it exists.
        // If there's no mip file, we assume the base file has mips and the base textures.
        ResFile bfres = needMips ? (mipRes ?? baseRes) : baseRes;
        Debug.Assert(bfres != null);

        var tex = bfres.Textures[name];
        if (tex == null) {
            return Err(new ErrorStack($"Texture {name} not found at mip level {mipLevel}"));
        }

        return tex;
    }
}
