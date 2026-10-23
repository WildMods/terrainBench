using System.Runtime.InteropServices;
using Entish;
namespace terrainBench.Bfres;

// See https://mk8.tockdom.com/wiki/FTEX_(File_Format)
[StructLayout(LayoutKind.Sequential, Size = 0xC0)]
public unsafe struct FTexHeader {
    public const int MAX_MIPS = 13;
    
    public readonly uint magic;
    public readonly uint dimension; // GX2SurfaceDim
    public readonly uint width;
    public readonly uint height;
    public readonly uint depth;
    public readonly uint numMipmaps;
    public readonly uint format; // GX2SurfaceFormat
    public readonly uint aaMode; // GX2AAMode
    public readonly uint usage; // GX2SurfaceUse
    public readonly uint dataSize; // in bytes
    public readonly uint dataPtr; // set at runtime
    public readonly uint mipmapsSize;
    public readonly uint mipmapsPtr; // set at runtime
    public readonly uint tileMode; // GX2TileMode
    public readonly uint swizzleValue;
    public readonly uint alignment; // [bytes per pixel] * 512
    public readonly uint pitch;
    public fixed uint mipmapOffsets[MAX_MIPS];
    public readonly uint firstMip;
    public readonly uint mipCount;
    public readonly uint firstSlice; // Always 0
    public readonly uint numSlices; // Always 1
    public fixed byte channelMapping[4]; // Allows channels to be remapped (like OpenGL texture channel swizzling)
    public fixed uint textureRegisters[5]; // Unknown, some hardware-specific purpose
    public readonly uint textureHandle; // set at runtime
    public readonly uint arrayLength;
    public readonly int filenameOffset;
    public readonly int filePathOffset; // Often stripped, but would give the full path of the authoring source file
    
    public readonly int dataOffset;
    public readonly int mipOffset;
    public readonly int userDataGroupOffset; // Offset to index group (radix tree) for the user data section
    public readonly ushort userDataEntryCount;
    public readonly ushort padding;

    public readonly override string ToString() {
        var msg = @$"{width}x{height}x{depth}, {numMipmaps} mips, array length {arrayLength}.
        {dataSize} bytes, mipmaps are {mipmapsSize} bytes.
        {mipCount} mips @ offset 0x{mipOffset:X} starting @ mip {firstMip}, data offset = 0x{dataOffset:X}
        Data pointer = {dataPtr}, mip pointer = {mipmapsPtr}
        Format = {(GX2SurfaceFormat)format}, Dimension = {(GX2SurfaceDimension)dimension}, MSAA Mode = {(GX2AAMode)aaMode}
        Surface Use = {(GX2SurfaceUse)usage}, Tile Mode = {(GX2TileMode)tileMode}, Swizzle value = 0x{swizzleValue:X}
        Channel mapping [RGBA order]: {(GX2Component)channelMapping[0]} {(GX2Component)channelMapping[1]} {(GX2Component)channelMapping[2]} {(GX2Component)channelMapping[3]}
        Alignment {alignment}, pitch {pitch}
        Mip offsets: ";
        for (int i = 0; i < MAX_MIPS; i++) {
            msg += String.Format("0x{0:X} ", mipmapOffsets[i]);
        }
        msg += "\n";
        return msg;
    }
    
    public static unsafe void Swap(ushort bom, FTexHeader* h) {
        if (bom == 0xFEFF) {
            return; // The data is in native endian
        }
        EndianUtils.Swap(&h->magic);
        EndianUtils.Swap(&h->dimension);
        EndianUtils.Swap(&h->width);
        EndianUtils.Swap(&h->height);
        EndianUtils.Swap(&h->depth);
        EndianUtils.Swap(&h->numMipmaps);
        EndianUtils.Swap(&h->format);
        EndianUtils.Swap(&h->aaMode);
        EndianUtils.Swap(&h->usage);
        EndianUtils.Swap(&h->dataSize);
        EndianUtils.Swap(&h->dataPtr);
        EndianUtils.Swap(&h->mipmapsSize);
        EndianUtils.Swap(&h->mipmapsPtr);
        EndianUtils.Swap(&h->tileMode);
        EndianUtils.Swap(&h->swizzleValue);
        EndianUtils.Swap(&h->alignment);
        EndianUtils.Swap(&h->pitch);

        for (int i = 0; i < MAX_MIPS; i++) {
            EndianUtils.Swap(&h->mipmapOffsets[i]);
        }
        EndianUtils.Swap(&h->firstMip);
        EndianUtils.Swap(&h->mipCount);
        EndianUtils.Swap(&h->firstSlice);
        EndianUtils.Swap(&h->numSlices);
        
        for (int i = 0; i < 5; i++) {
            EndianUtils.Swap(&h->textureRegisters[i]);
        }
        EndianUtils.Swap(&h->textureHandle);
        // Note: we *don't* byteswap the array length! In BOTW files, it's just
        // a byte (so it can be read as little endian). The same is apparently
        // true of MK8 files.
        EndianUtils.Swap(&h->filenameOffset);
        EndianUtils.Swap(&h->filePathOffset);
        
        EndianUtils.Swap(&h->dataOffset);
        EndianUtils.Swap(&h->mipOffset);
        EndianUtils.Swap(&h->userDataGroupOffset);
        EndianUtils.Swap(&h->userDataEntryCount);
        EndianUtils.Swap(&h->padding);
    } 

    public readonly long CalcSurfaceSize() {
        var numSamples = 1 << (int)aaMode;
        return height * pitch * depth * numSamples;
    }
}
