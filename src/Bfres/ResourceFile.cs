using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Entish;
using Entish.Attributes;
using static Entish.EndianUtils;
using OperationResult;
using static OperationResult.Helpers;

using BfresLibrary;
namespace terrainBench;
using Bfres;

public class ResourceFile {
    public enum SubfileType {
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



    // We have to do explicit layout to stop the compiler from adding padding.
    [StructLayout(LayoutKind.Explicit, Size = 0x6C, Pack = 0)]
    public unsafe struct ResFileHeader {
        [FieldOffset(0x00)] public readonly uint magic;
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

        public static unsafe void Swap(ResFileHeader* header) {
            if (header->bom == 0xFEFF) {
                return; // The data is in native endian
            }

            EndianUtils.Swap(&header->magic);
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

        public uint SubfileOffset(SubfileType t) {
            uint i = (uint)t;
            uint SUBFILE_OFFSET_TABLE_POS = 0x20;
            uint offsetOfOffset = SUBFILE_OFFSET_TABLE_POS + (sizeof(uint) * i);
            int offsetValue = subFileOffsets[i];
            return (uint)(offsetOfOffset + offsetValue);
        }
    }
    
    
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    readonly unsafe struct IndexGroupEntry {
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
    public readonly unsafe struct IndexGroup {
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
    
    private static unsafe ref T ReadUnsafe<T>(ReadOnlySpan<byte> span, long offset)
        where T : unmanaged
    {
        Debug.Assert(offset >= 0 && (offset + sizeof(T)) < span.Length);
        fixed (byte* ptr = span) {
            return ref *(T*)(ptr + offset);
        }
    }

    private static unsafe ReadOnlySpan<T> ROSpanSegment<T>(ReadOnlySpan<byte> span, long offset, int count)
        where T : unmanaged
    {
        Debug.Assert(offset >= 0 && count >= 0);
        var size = sizeof(T) * count;
        var endPos = offset + size;
        if (endPos > span.Length) {
            Console.WriteLine("Span of size {0} @ offset {1} would be out of bounds (size = {2})", size, offset, span.Length);
            Debug.Assert(false);
        }
        fixed (byte* ptr = span) {
            var pos = (T*)(ptr + offset);
            return new ReadOnlySpan<T>(pos, count);
        }
    }
    
    private static unsafe Span<T> SpanSegment<T>(Span<byte> span, long offset, int count)
        where T : unmanaged
    {
        Debug.Assert(offset >= 0 && count >= 0);
        var size = sizeof(T) * count;
        var endPos = offset + size;
        if (endPos > span.Length) {
            Console.WriteLine("Span of size {0} @ offset {1} would be out of bounds (size = {2})", size, offset, span.Length);
            Debug.Assert(false);
        }
        fixed (byte* ptr = span) {
            var pos = (T*)(ptr + offset);
            return new Span<T>(pos, count);
        }
    }

    public readonly struct SubfileHandle(SubfileType t, uint offset, uint count) {
        public readonly SubfileType type = t;
        public readonly uint indexGroupOffset = offset;
        public readonly uint fileCount = count;
    }
    
    public static Result<SubfileHandle, ErrorStack> GetSubfile(ReadOnlySpan<byte> data, SubfileType t) {
        unsafe {
            var header = ReadUnsafe<ResFileHeader>(data, 0);
            ResFileHeader.Swap(&header);

            var count = header.subFileCounts[(int)t];
            var offset = header.SubfileOffset(t);

            if (offset == 0) {
                return Err(new ErrorStack($"The BFRES didn't contain a subfile of type {t}"));
            }
            return new SubfileHandle(t, offset, count);
        }
    }

    public static uint FindSubfileEntry(ReadOnlySpan<byte> data, SubfileHandle h, string name) {
        var header = ReadUnsafe<ResFileHeader>(data, 0);

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
            Console.WriteLine("Found '{0}' in entry {1}, name @ 0x{2:X}, data @ 0x{3:X}", name, childIdx, namePtr, dataPtr);
            return (uint)dataPtr;
        }
    }

    public static FTexHeader GetFTEX(ReadOnlySpan<byte> data, uint offset) {
        var header = ReadUnsafe<ResFileHeader>(data, 0);
        var ftex = ReadUnsafe<FTexHeader>(data, offset);
        unsafe {
            FTexHeader.Swap(header.bom, &ftex);
        }

        return ftex;
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
            Console.WriteLine("Got swizzled span of {0} bytes of texture data", baseSpan.Length);
            return baseSpan;
        }
    }

    public static byte[] GetDeswizzledTextureData(ReadOnlySpan<byte> data, uint ftexOffset, FTexHeader ftex, bool mip) {
        var swizzledData = GetRawTextureData(data, ftexOffset, ftex, mip);

        // Alignment = 512 * bytes per pixel
        var bitsPerPixel = (ftex.pitch * 8) / 512;
        var bitsPerBlock = (ftex.alignment * 8) / 512;


        var mipMin = mip ? 1 : 0;
        var mipMax = mip ? Math.Min(FTexHeader.MAX_MIPS, ftex.mipCount) : 1;
        var numMips = mipMax - mipMin;
        var blockSize = GX2.BlockSize((GX2SurfaceFormat)ftex.format);

        uint outPos = 0;
        var outBuf = new byte[swizzledData.Length];
        for (int lvl = mipMin; lvl < mipMax; lvl++) {
            var width = Math.Max(1, ftex.width >> lvl);
            var height = Math.Max(1, ftex.height >> lvl);
            uint curPitch = ftex.pitch >> lvl;

            var layerSize = Math.Max(8, width * height * bitsPerPixel / (1 * 8));
            var levelSize = layerSize * ftex.arrayLength;
            
            var layerSizeIn = Math.Max(layerSize, ftex.alignment / 8);
            var rowSizeIn = layerSizeIn / blockSize;
            Console.WriteLine("Input layer size: {0}, row size {1} @ mip {2}", layerSizeIn, rowSizeIn, lvl);
            
            Console.WriteLine("Layer size: {0}, alternate calc gives {1}", layerSize, GX2.CalcSliceSize(ftex.height, ftex.pitch, ftex.aaMode));
            Console.WriteLine("Level size: {0}, alternate calc gives {1}", levelSize, GX2.CalcSurfaceSize(ftex.height, ftex.pitch, ftex.depth, ftex.aaMode));
            uint startOffset = 0;
            if (mip) {
                unsafe {
                    startOffset = ftex.mipmapOffsets[lvl - 1];
                }
                if (lvl == 1) {
                    startOffset -= ftex.dataSize;
                }
            }
            var levelDataIn = ROSpanSegment<byte>(swizzledData, startOffset, (int)(layerSizeIn * ftex.arrayLength));
            var levelDataOut = SpanSegment<byte>(outBuf, outPos, (int)(layerSize * ftex.arrayLength));
            outPos += levelSize;

            File.WriteAllBytes($"mip{lvl}.bin", levelDataIn.ToArray());
            
            Console.WriteLine("Pitch = {0} @ lvl {1}, levelSize {2}, layerSize {3}", curPitch, lvl, levelSize, layerSize);
            Console.WriteLine($"Mip {lvl} = {width}x{height}");
            for (uint i = 0; i < ftex.arrayLength; i++) {
                var layerIn = ROSpanSegment<byte>(levelDataIn, i * layerSizeIn, (int)layerSize);
                var layerOut = SpanSegment<byte>(levelDataOut, i * layerSize, (int)layerSize);

                // Note the input and output are the same right now since we copy the unswizzled data in
                BfresLibrary.Swizzling.GX2.swizzleSurf(width, height,
                    i, ftex.format, ftex.aaMode, ftex.usage, ftex.tileMode,
                    ftex.swizzleValue, curPitch, bitsPerBlock, ftex.firstSlice, 0, layerIn, layerOut, 0);
            }
            File.WriteAllBytes($"outmip{lvl}.bin", levelDataOut.ToArray());
        }
        
        return outBuf;
    }
    
    public static void ParseBFRES(ReadOnlySpan<byte> data) {
        var ftexHandleRes = GetSubfile(data, SubfileType.FTEX);
        if (ftexHandleRes.IsErr()) {
            Console.WriteLine("Failed to find FTEX subfile!");
        }

        var ftexHandle = ftexHandleRes.Unwrap();
        Console.WriteLine("FTEX offset 0x{0:x}, {1} entries", ftexHandle.indexGroupOffset, ftexHandle.fileCount);
        uint offset = FindSubfileEntry(data, ftexHandle, "MaterialAlb");

        unsafe {
            var header = ReadUnsafe<ResFileHeader>(data, 0);
            var ftex = ReadUnsafe<Bfres.FTexHeader>(data, offset);
            Bfres.FTexHeader.Swap(header.bom, &ftex);
            Console.WriteLine("{0}", ftex);
        }
    }
}
