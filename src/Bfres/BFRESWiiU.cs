using System.Runtime.InteropServices;
using System.Diagnostics;
using Entish;
using static Entish.EndianUtils;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench;
using static UnsafeUtil;
using Bfres;

// The Wii U and Switch versions of BFRES differ a little, this file has the Wii U structures.

public enum SubfileTypeWiiU {
    FMDL = 0, /* Models */
    FTEX = 1, /* Textures */
    FSKA = 2, /* Skeletal animations */
    FSHU_PARAMS = 3, /* Shader params */
    FSHU_COLOR_ANIM = 4, /* Color animations */
    FSHU_TEX_SRT = 5, /* Texture SRT animations */
    FTXP = 6, /* Texture pattern animations */
    FVIS_BONE = 7, /* Bone visibility animations */
    FVIS_MATE = 8, /* Material visibility animations */
    FSHA = 9, /* Shape animation (shape keys?) */
    FSCN = 10, /* Scene animation */
    EMBED = 11, /* Other embedded data */
};

[StructLayout(LayoutKind.Sequential, Size = 0x10)]
readonly struct IndexGroupEntry {
    public readonly uint searchValue;
    public readonly ushort leftIndex;
    public readonly ushort rightIndex;
    public readonly int nameOffset;
    public readonly int dataOffset;
    
    public static unsafe void Swap(ushort bom, IndexGroupEntry* e) {
        if (bom == 0xFEFF) {
            return; // The data is in native endian
        }
        EndianUtils.Swap(&e->searchValue);
        EndianUtils.Swap(&e->leftIndex);
        EndianUtils.Swap(&e->rightIndex);
        EndianUtils.Swap(&e->nameOffset);
        EndianUtils.Swap(&e->dataOffset);
    } 
};

[StructLayout(LayoutKind.Sequential, Size = 0x8)]
public readonly struct IndexGroup {
    public readonly uint groupSize;
    public readonly int entryCount;
    
    public static unsafe void Swap(ushort bom, IndexGroup* header) {
        if (bom == 0xFEFF) {
            return; // The data is in native endian
        }
        EndianUtils.Swap(&header->groupSize);
        EndianUtils.Swap(&header->entryCount);
    } 
};

public readonly struct SubfileHandle(SubfileTypeWiiU t, uint offset, uint count) {
    public readonly SubfileTypeWiiU type = t;
    public readonly uint indexGroupOffset = offset;
    public readonly uint fileCount = count;
}

// We have to do explicit layout to stop the compiler from adding padding.
[StructLayout(LayoutKind.Explicit, Size = 0x6C, Pack = 0)]
public unsafe struct ResFileHeaderWiiU {
    [FieldOffset(0x00)] public fixed byte magic[4];
    [FieldOffset(0x04)] public readonly byte versionMajor;
    [FieldOffset(0x05)] public readonly byte versionMinor;
    [FieldOffset(0x06)] public readonly byte versionPatch;
    [FieldOffset(0x07)] public readonly byte versionHotfix;
    [FieldOffset(0x08)] public readonly ushort bom;
    
    [FieldOffset(0x0A)] public readonly ushort headerSize;
    [FieldOffset(0x0C)] public readonly uint fileSize;
    [FieldOffset(0x10)] public readonly uint fileAlignment;
    [FieldOffset(0x14)] public readonly int filenameOffset;
    [FieldOffset(0x18)] public readonly int stringTableLength;
    [FieldOffset(0x1C)] public readonly int stringTableOffset;
    [FieldOffset(0x20)] public fixed int subFileOffsets[12] ;
    [FieldOffset(0x50)] public fixed ushort subFileCounts[12];
    [FieldOffset(0x68)] public readonly uint userPointer;

    public static unsafe void Swap(ResFileHeaderWiiU* header) {
        if (header->bom == 0xFEFF) {
            return; // The data is in native endian
        }

        EndianUtils.Swap(&header->headerSize);
        EndianUtils.Swap(&header->fileSize);
        EndianUtils.Swap(&header->fileAlignment);
        EndianUtils.Swap(&header->filenameOffset);
        EndianUtils.Swap(&header->stringTableLength);
        EndianUtils.Swap(&header->stringTableOffset);
        for (uint i = 0; i < 12; i++)
        {
            EndianUtils.Swap(&header->subFileOffsets[i]);
            EndianUtils.Swap(&header->subFileCounts[i]);
        }
        EndianUtils.Swap(&header->userPointer);
    }

    public uint SubfileOffset(SubfileTypeWiiU t)
    {
        uint i = (uint)t;
        uint SUBFILE_OFFSET_TABLE_POS = 0x20;
        uint offsetOfOffset = SUBFILE_OFFSET_TABLE_POS + (sizeof(uint) * i);
        int offsetValue = subFileOffsets[i];
        return (uint)(offsetOfOffset + offsetValue);
    }
    

    public readonly bool CheckVersion(byte major, byte minor, byte patch, byte hotfix) {
        return versionMajor == major && versionMinor == minor &&
               versionPatch == patch && versionHotfix == hotfix;
    }
    public readonly bool IsBFRES() {
        return magic[0] == 'F' && magic[1] == 'R' && magic[2] == 'E' && magic[3] == 'S';
    }
    
}

public static class BFRESWiiU {
    public static uint FindSubfileEntry(ReadOnlySpan<byte> data, SubfileHandle h, string name) {
        var header = ReadUnsafe<ResFileHeaderWiiU>(data, 0);

        unsafe {
            var group = ReadUnsafe<IndexGroup>(data, h.indexGroupOffset);
            IndexGroup.Swap(header.bom, &group);
            uint entriesPos = (uint)(h.indexGroupOffset + sizeof(IndexGroup));
            var entries = ROSpanSegment<IndexGroupEntry>(data, entriesPos, group.entryCount + 1);


            // Walk the radix tree of entries. See:
            // https://mk8.tockdom.com/wiki/BFRES_(File_Format)#Index_Group
            // Also see BfresLibrary.ResDict.Traverse() in the BfresLibrary project:
            // https://github.com/KillzXGaming/BfresLibrary/blob/master/BfresLibrary/Shared/Common/ResDict.cs#L515-L539

            IndexGroupEntry parent = entries[0];
            IndexGroupEntry.Swap(header.bom, &parent);

            uint childIdx = parent.leftIndex;
            IndexGroupEntry child = entries[(int)childIdx];
            IndexGroupEntry.Swap(header.bom, &child);

            // Each entry points to a specific bit in a specific character of
            // the filename. The value of that bit determines whether we walk
            // left or right down the tree.
            while (parent.searchValue > child.searchValue) {
                parent = child;

                // Get the bit pointed to by this entry
                uint charpos = child.searchValue >> 3;
                byte bitpos = (byte)(child.searchValue & 0b111);
                int direction = 0;
                if (charpos < name.Length) {
                    int c = name[(int)charpos] >> bitpos;
                    direction = c & 1;
                }

                // Walk down the tree based on the target bit
                childIdx = direction == 1 ? child.rightIndex : child.leftIndex;
                child = entries[(int)childIdx];
                IndexGroupEntry.Swap(header.bom, &child);

                if (childIdx == 0 || childIdx > group.entryCount) {
                    Console.WriteLine("Can't find '{0}'", name);
                    return 0; // Unable to find entry
                }
            }

            var targetEntryPos = entriesPos + sizeof(IndexGroupEntry) * childIdx;
            // The pointers are relative to the pointer values themselves, and
            // C# doesn't seem to have anything like offsetof().
            const int NAME_PTR_OFFSET_IN_ENTRY = 8;
            const int DATA_PTR_OFFSET_IN_ENTRY = 12;

            var namePtr = targetEntryPos + NAME_PTR_OFFSET_IN_ENTRY + child.nameOffset;
            var dataPtr = targetEntryPos + DATA_PTR_OFFSET_IN_ENTRY + child.dataOffset;
            return (uint)dataPtr;
        }
    }

    public static FTexHeader GetFTEX(ReadOnlySpan<byte> data, uint offset) {
        var header = ReadUnsafe<ResFileHeaderWiiU>(data, 0);
        var ftex = ReadUnsafe<FTexHeader>(data, offset);
        unsafe {
            FTexHeader.Swap(header.bom, &ftex);
        }

        return ftex;
    }

    public static Result<FTexHeader, ErrorStack> GetFTEXByName(ReadOnlySpan<byte> data, string name) {
        var ftexHandleRes = GetSubfile(data, SubfileTypeWiiU.FTEX);
        if (ftexHandleRes.IsErr()) {
            return Err(ftexHandleRes.ExpectErr("").Context("Failed to find FTEX subfile"));
        }

        var ftexHandle = ftexHandleRes.Unwrap();
        uint offset = FindSubfileEntry(data, ftexHandle, name);
        return GetFTEX(data, offset);
    }

    public static ReadOnlySpan<byte> GetRawTextureData(ReadOnlySpan<byte> data, uint ftexOffset, FTexHeader ftex, bool mip) {
        short BASE_OFFSET_POS = 0xB0;
        short MIP_OFFSET_POS = 0xB4;
        var baseDataOffset = ftexOffset + BASE_OFFSET_POS + ftex.dataOffset;
        var mipDataOffset = ftexOffset + MIP_OFFSET_POS + (ftex.mipOffset - ftex.dataSize);

        if (mip) {
            var mipSpan = ROSpanSegment<byte>(data, mipDataOffset, (int)ftex.mipmapsSize);
            return mipSpan;
        } else {
            var baseSpan = ROSpanSegment<byte>(data, baseDataOffset, (int)ftex.dataSize);
            return baseSpan;
        }
    }

    public static byte[] GetDeswizzledTextureData(ReadOnlySpan<byte> data, uint ftexOffset, FTexHeader ftex, int mipLevel) {
        var isMip = mipLevel != 0;
        var swizzledData = GetRawTextureData(data, ftexOffset, ftex, isMip);

        // Alignment = 512 * bytes per pixel
        var blockSize = GX2.BlockSize((GX2SurfaceFormat)ftex.format);
        var bitsPerBlock = (ftex.alignment * 8) / 512;
        var bitsPerPixel = bitsPerBlock / (blockSize * blockSize);

        var width = Math.Max(1, ftex.width >> mipLevel);
        var height = Math.Max(1, ftex.height >> mipLevel);

        // Size of a single array layer at this mip level
        var layerSize = Math.Max(8, width * height * bitsPerPixel / 8);
        // Size of this entire mip level
        uint levelSize = (uint)(layerSize * ftex.arrayLength);
        var outBuf = new byte[levelSize];

        uint startOffset = 0;
        if (isMip) {
            unsafe {
                startOffset = ftex.mipmapOffsets[mipLevel - 1];
            }
            if (mipLevel == 1) {
                startOffset -= ftex.dataSize;
            }
        }
        
        // Tiling mode may change as we get smaller mips, we need to query
        // the library for the correct info.
        var surf = BfresLibrary.Swizzling.GX2.getSurfaceInfo(
            (BfresLibrary.Swizzling.GX2.GX2SurfaceFormat)ftex.format, ftex.width, ftex.height, ftex.arrayLength, ftex.dimension, ftex.tileMode, ftex.aaMode, mipLevel);
        uint curPitch = surf.pitch;
        var layerSizeIn = surf.sliceSize;
        
        // Views of the entire mip level
        var levelDataIn = ROSpanSegment<byte>(swizzledData, startOffset, (int)(layerSizeIn * ftex.arrayLength));
        var levelDataOut = SpanSegment<byte>(outBuf, 0, (int)(layerSize * ftex.arrayLength));
        
        // Deswizzle each array layer
        for (uint i = 0; i < ftex.arrayLength; i++) {
            // Views of a single array layer
            var layerIn = ROSpanSegment<byte>(levelDataIn, i * layerSizeIn, (int)layerSizeIn);
            var layerOut = SpanSegment<byte>(levelDataOut, i * layerSize, (int)layerSize);

            BfresLibrary.Swizzling.GX2.swizzleSurf(width, height,
                i, ftex.format, ftex.aaMode, ftex.usage, surf.tileMode,
                ftex.swizzleValue, curPitch, bitsPerBlock, ftex.firstSlice, 0, layerIn, layerOut, 0);
        }
        File.WriteAllBytes($"outmip{mipLevel}.bin", outBuf);
        
        return outBuf;
    }
    
    
    public static byte[] GetDeswizzledByName(string name, ReadOnlySpan<byte> data, int mipLevel) {
        var ftexHandleRes = GetSubfile(data, SubfileTypeWiiU.FTEX);
        if (ftexHandleRes.IsErr()) {
            Console.WriteLine("Failed to find FTEX subfile!");
        }

        var ftexHandle = ftexHandleRes.Unwrap();
        uint offset = FindSubfileEntry(data, ftexHandle, name);

        var ftex = GetFTEX(data, offset);
        var result = GetDeswizzledTextureData(data, offset, ftex, mipLevel);
        return result;
    }
    
    public static Result<SubfileHandle, ErrorStack> GetSubfile(ReadOnlySpan<byte> data, SubfileTypeWiiU t) {
        unsafe {
            var header = ReadUnsafe<ResFileHeaderWiiU>(data, 0);
            ResFileHeaderWiiU.Swap(&header);

            var count = header.subFileCounts[(int)t];
            var offset = header.SubfileOffset(t);

            if (offset == 0) {
                return Err(new ErrorStack($"The BFRES didn't contain a subfile of type {t}"));
            }
            return new SubfileHandle(t, offset, count);
        }
    }
}
