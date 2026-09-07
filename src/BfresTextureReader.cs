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
    // A Wii U Tex1 file, or a Switch Tex file
    readonly ResFile baseRes;

    // If pulling from Wii U files, this will be the Tex2 BFRES (for mipmaps)
    readonly ResFile? mipRes = null;

    public byte[] deswizzledBase = Array.Empty<byte>();
    public byte[] deswizzledMip = Array.Empty<byte>();

    BfresTextureReader(Span<byte> baseData) {
        baseRes = new ResFile(new MemoryStream(baseData.ToArray()));
    }
    
    BfresTextureReader(Span<byte> baseData, Span<byte> mipData) {
        baseRes = new ResFile(new MemoryStream(baseData.ToArray()));
        var deswizzleTime = Stopwatch.StartNew();
        deswizzledBase = GetDeswizzled("MaterialAlb", baseData, false);
        deswizzledMip = GetDeswizzled("MaterialAlb", mipData, true);
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

    public static byte[] GetDeswizzled(string name, ReadOnlySpan<byte> data, bool mip) {
        var ftexHandleRes = ResourceFile.GetSubfile(data, ResourceFile.SubfileType.FTEX);
        if (ftexHandleRes.IsErr()) {
            Console.WriteLine("Failed to find FTEX subfile!");
        }

        var ftexHandle = ftexHandleRes.Unwrap();
        Console.WriteLine("FTEX offset 0x{0:x}, {1} entries", ftexHandle.indexGroupOffset, ftexHandle.fileCount);
        uint offset = ResourceFile.FindSubfileEntry(data, ftexHandle, "MaterialAlb");

        var ftex = ResourceFile.GetFTEX(data, offset);
        var result = ResourceFile.GetDeswizzledTextureData(data, offset, ftex, mip);
        return result;
    }
}
