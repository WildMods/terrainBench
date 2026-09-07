using System.Runtime.InteropServices;
using Entish;
using static Entish.EndianUtils;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench;

// The Wii U and Switch versions of BFRES differ a little, this file has the Switch structures.
// We have to do explicit layout to stop the compiler from adding padding.


// See https://epd.zeldamods.org/wiki/Common_nn::util#nn::util::BinaryFileHeader
[StructLayout(LayoutKind.Explicit, Size = 0x20, Pack = 0)]
public readonly struct BinaryFileHeader {
    [FieldOffset(0x00)] public readonly ulong magic;
    [FieldOffset(0x08)] public readonly byte versionMajor;
    [FieldOffset(0x09)] public readonly byte versionMinor;
    [FieldOffset(0x0A)] public readonly ushort versionPatch;
    [FieldOffset(0x0C)] public readonly ushort bom;

    [FieldOffset(0x0E)] public readonly byte packedAlignment;
    [FieldOffset(0x0F)] public readonly byte unknown; // Always 64
    [FieldOffset(0x10)] public readonly uint filenameOffset;
    [FieldOffset(0x14)] public readonly ushort isRelocated; // Set at runtime by loader
    [FieldOffset(0x16)] public readonly ushort firstBlockHeaderOffset;
    [FieldOffset(0x18)] public readonly uint relocationTableOffset;
    [FieldOffset(0x1C)] public readonly uint fileSize;

    public static unsafe void Swap(BinaryFileHeader* header) {
        if (header->bom == 0xFEFF) {
            return; // The data is in native endian
        }

        EndianUtils.Swap(&header->magic);
        EndianUtils.Swap(&header->versionPatch);
        EndianUtils.Swap(&header->bom);
        EndianUtils.Swap(&header->filenameOffset);
        EndianUtils.Swap(&header->isRelocated);
        EndianUtils.Swap(&header->firstBlockHeaderOffset);
        EndianUtils.Swap(&header->relocationTableOffset);
        EndianUtils.Swap(&header->fileSize);
    }
}

public enum SubfileTypeSwitch {
    FMDL = 0, /* Models */
    FSKA = 1, /* Skeletal animations */
    FMAA = 2, /* Material animations */
    FBVS = 3, /* Bone visibility animations */
    FSHA = 4, /* Shape animation (shape keys?) */
    FSCN = 5, /* Scene animation */
    MAX = FSCN,
};

[StructLayout(LayoutKind.Explicit, Size = 0x10, Pack = 0)]
public readonly struct Subfile {
    [FieldOffset(0x00)] public readonly long arrayOffset;
    [FieldOffset(0x08)] public readonly long dictOffset;
}

// See https://wiki.wexosmk.xyz/index.php/BFRES_(File_Format)/Switch#Header
[StructLayout(LayoutKind.Explicit, Size = 0xD0, Pack = 0)]
public unsafe struct ResFileHeaderSwitch {
    [FieldOffset(0x00)] public readonly BinaryFileHeader header;
    [FieldOffset(0x20)] public readonly ulong nameOffset;

    // C# doesn't allow me to make fixed arrays of non-primitive types, sorry.
    [FieldOffset(0x28)] public readonly Subfile fmdlInfo;
    [FieldOffset(0x38)] public readonly Subfile fskaInfo;
    [FieldOffset(0x48)] public readonly Subfile fmaaInfo;
    [FieldOffset(0x58)] public readonly Subfile fbvsInfo;
    [FieldOffset(0x68)] public readonly Subfile fshaInfo;
    [FieldOffset(0x78)] public readonly Subfile fscnInfo;
    [FieldOffset(0x88)] public readonly Subfile memPoolInfo;
    [FieldOffset(0x98)] public readonly Subfile embeddedInfo;
    [FieldOffset(0xA8)] public readonly ulong userPointer;
    [FieldOffset(0xB0)] public readonly ulong stringTableOffset;
    [FieldOffset(0xB8)] public readonly uint stringTableSize;
    [FieldOffset(0xBC)] public fixed ushort subfileCounts[(int)SubfileTypeSwitch.MAX];
    [FieldOffset(0xC8)] public readonly ushort embeddedFileCount;
    [FieldOffset(0xCA)] public fixed byte padding[6];
}

[StructLayout(LayoutKind.Explicit, Size = 0x10, Pack = 0)]
public readonly struct EmbeddedFileSwitch {
    [FieldOffset(0x00)] public readonly long dataOffset;
    [FieldOffset(0x08)] public readonly uint dataSize;
    [FieldOffset(0x0C)] public readonly uint padding;
}

public static class BFRESSwitch {
    public static RefResult<ReadOnlySpan<byte>, ErrorStack> GetEmbeddedFile(ReadOnlySpan<byte> data, int index) {
        var platformRes = ResourceFile.GetBFRESPlatform(data);
        if (platformRes.IsErr()) {
            return Err(platformRes.Err().Context($"Unable to get embedded file {index} because the data isn't a BFRES"));
        }
        if (platformRes.Unwrap() != ResourceFile.Platform.Switch) {
            return Err(platformRes.Err().Context($"Unable to get embedded file {index} because the data isn't a Switch BFRES"));
        }
        var header = UnsafeUtil.ReadUnsafe<ResFileHeaderSwitch>(data, 0);
        var embeddedFiles = UnsafeUtil.ROSpanSegment<EmbeddedFileSwitch>(data, header.embeddedInfo.arrayOffset, header.embeddedFileCount);
        if (index >= header.embeddedFileCount) {
            return Err(new ErrorStack($"Embedded file {index} doesn't exist [there are {header.embeddedFileCount} files]"));
        }

        var entry = embeddedFiles[index];
        return UnsafeUtil.ROSpanSegment<byte>(data, entry.dataOffset, (int)entry.dataSize);
    }

    public static RefResult<ReadOnlySpan<byte>, ErrorStack> GetBNTX(ReadOnlySpan<byte> data) {
        var res = GetEmbeddedFile(data, 0);
        if (res.IsErr())
        {
            return Err(res.Err().Context("Unable to get BNTX data"));
        }
        return res.Unwrap();
    }

    public static BntxSharp.BntxTexture? GetBNTXTexture(ReadOnlySpan<byte> data, string name) {
        var bntxSpanRes = GetBNTX(data);
        if (bntxSpanRes.IsErr()) {
            Console.WriteLine("Failed to load BNTX section!");
            Console.WriteLine(bntxSpanRes.Err());
        }
        
        var bntx = BntxSharp.BntxFile.Load(bntxSpanRes.Unwrap());
        return bntx.Find(name);
    }
    
    public static void ParseBFRES(ReadOnlySpan<byte> data) {
        var header = UnsafeUtil.ReadUnsafe<ResFileHeaderSwitch>(data, 0);
        Console.WriteLine($"Found {header.embeddedFileCount} embedded files");

        var embeddedFiles = UnsafeUtil.ROSpanSegment<EmbeddedFileSwitch>(data, header.embeddedInfo.arrayOffset, header.embeddedFileCount);
        for (int i = 0; i < header.embeddedFileCount; i++) {
            var entry = embeddedFiles[i];
            Console.WriteLine($"Embedded file {i} is {entry.dataSize} bytes @ 0x{entry.dataOffset:X}");
        }

        var bntxSpanRes = GetBNTX(data);
        if (bntxSpanRes.IsErr()) {
            Console.WriteLine("Failed to load BNTX section!");
            Console.WriteLine(bntxSpanRes.Err());
        }
        
        var bntx = BntxSharp.BntxFile.Load(bntxSpanRes.Unwrap());
        foreach (var t in bntx.Textures) {
            Console.WriteLine($"'{t.Name}': {t.Width}x{t.Height}x{t.ArrayLength}");
        }
    }
}
