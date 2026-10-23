using System.Text;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Entish;
using Entish.Attributes;
using terrainBench.Core;
using static Entish.EndianUtils;
namespace terrainBench;

// Represents data loaded from the Terrain Scene Binary (TSCB) file
public readonly ref struct TerrainSceneBinary
{
    // https://zeldamods.org/wiki/TSCB#Header
    private const int TSCB_HEADER_SIZE = 0x30;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)]
    public struct TSCBHeader : ISwappable<TSCBHeader>
    {
        public uint magic;
        public uint version;
        public uint unknown; // Always 1
        public uint basenamesOffset;
        public float worldScale; // Horizontal scale, default 500
        public float worldHeight; // Vertical scale, default 800
        public uint matInfoCount;
        public uint areaCount;
        public uint unknown2;
        public uint unknown3;
        public float tileBaseSize; // Default 32
        public uint unknown4; // Always 8

        public static unsafe void Swap(TSCBHeader* header)
        {
            if (GetSystemEndianness() == Endianness.Big) {
                return;
            }
            EndianUtils.Swap(&header->magic);
            EndianUtils.Swap(&header->version);
            EndianUtils.Swap(&header->unknown);
            EndianUtils.Swap(&header->basenamesOffset);
            EndianUtils.Swap(&header->worldScale);
            EndianUtils.Swap(&header->worldHeight);
            EndianUtils.Swap(&header->matInfoCount);
            EndianUtils.Swap(&header->areaCount);
            EndianUtils.Swap(&header->unknown2);
            EndianUtils.Swap(&header->unknown3);
            EndianUtils.Swap(&header->tileBaseSize);
            EndianUtils.Swap(&header->unknown4);
        }
    }
    
    // https://zeldamods.org/wiki/TSCB#Material_Information_Value_Table
    private const int MATERIAL_INFO_SIZE = 0x14;
    [StructLayout(LayoutKind.Sequential, Size = 0x14)]
    public struct MaterialInfo : ISwappable<MaterialInfo>
    {
        public uint index;
        public float textureU;
        public float textureV;
        public float unknown1;
        public float unknown2;

        public static unsafe void Swap(MaterialInfo* header) {
            if (GetSystemEndianness() == Endianness.Big) {
                return;
            }
            EndianUtils.Swap(&header->index);
            EndianUtils.Swap(&header->textureU);
            EndianUtils.Swap(&header->textureV);
            EndianUtils.Swap(&header->unknown1);
            EndianUtils.Swap(&header->unknown2);
        }
    }

    private const int TILE_INFO_SIZE = 0x30;
    [StructLayout(LayoutKind.Sequential, Size = 0x30)]
    public struct TileInfo : ISwappable<TileInfo>
    {
        public float x;
        public float z;
        public float tileSize;
        public float minLandHeight;
        public float maxLandHeight;
        public float minWaterHeight;
        public float maxWaterHeight;
        public uint unknown1;
        public uint basenameOffset;
        public uint unknown2;
        public uint unknown3;
        public uint extraInfoPresent;

        public static unsafe void Swap(TileInfo* info)
        {
            if (GetSystemEndianness() == Endianness.Big) {
                return;
            }
            EndianUtils.Swap(&info->x);
            EndianUtils.Swap(&info->z);
            EndianUtils.Swap(&info->tileSize);
            EndianUtils.Swap(&info->minLandHeight);
            EndianUtils.Swap(&info->maxLandHeight);
            EndianUtils.Swap(&info->minWaterHeight);
            EndianUtils.Swap(&info->maxWaterHeight);
            EndianUtils.Swap(&info->unknown1);
            EndianUtils.Swap(&info->basenameOffset);
            EndianUtils.Swap(&info->unknown2);
            EndianUtils.Swap(&info->unknown3);
            EndianUtils.Swap(&info->extraInfoPresent);
        }
    }

    private const int EXTRA_TILE_INFO_SIZE = 0x10;
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct ExtraTileInfo : ISwappable<ExtraTileInfo> {
        public uint unknown1; // Always 3
        public uint isWater;  // If 0, it's grass
        public uint unknown2;
        public uint unknown3;
        
        public static unsafe void Swap(ExtraTileInfo* info) {
            if (GetSystemEndianness() == Endianness.Big) {
                return;
            }
            EndianUtils.Swap(&info->unknown1);
            EndianUtils.Swap(&info->isWater);
            EndianUtils.Swap(&info->unknown2);
            EndianUtils.Swap(&info->unknown3);
        }
    }

    private static unsafe ref T ReadUnsafe<T>(ReadOnlySpan<byte> span, long offset)
    where T : unmanaged
    {
        Debug.Assert(offset >= 0 && (offset + sizeof(T)) < span.Length);
        fixed (byte* ptr = span) {
            return ref *(T*)(ptr + offset);
        }
    }
    
    private static unsafe ReadOnlySpan<T> SpanSegment<T>(ReadOnlySpan<byte> span, long offset, int count)
        where T : unmanaged
    {
        Debug.Assert(offset >= 0 && count >= 0);
        Debug.Assert((offset + sizeof(T) * count) < span.Length);
        fixed (byte* ptr = span) {
            var pos = (T*)(ptr + offset);
            return new ReadOnlySpan<T>(pos, count);
        }
    }
    
    private readonly ReadOnlySpan<byte> data;
    public readonly TSCBHeader header;

    private readonly int tileInfoPos;
    private readonly int matInfoPos;
    
    private readonly ReadOnlySpan<uint> matInfoOffsets;
    private readonly ReadOnlySpan<uint> tileInfoOffsets;
    
    public TerrainSceneBinary(ReadOnlySpan<byte> data) {
        this.data = data;
        uint matInfoCount;
        uint tileInfoCount;
        unsafe {
            var headerRef = ReadUnsafe<TSCBHeader>(data, 0);
            TSCBHeader.Swap(&headerRef);
            header = headerRef;
            
            // Parse material info.
            // See https://zeldamods.org/wiki/TSCB#Material_Information_Array
            int matInfoSize = Swap(ReadUnsafe<int>(data, sizeof(TSCBHeader)));
            matInfoCount = header.matInfoCount;
            matInfoPos = sizeof(TSCBHeader) + 4;
            matInfoOffsets = SpanSegment<uint>(data, matInfoPos, (int)matInfoCount);

            // Parse tile info (wiki uses "tile" and "area" interchangeably).
            // See https://zeldamods.org/wiki/TSCB#Area_Array
            tileInfoPos = sizeof(TSCBHeader) + matInfoSize;
            tileInfoCount = header.areaCount;
            tileInfoOffsets = SpanSegment<uint>(data, tileInfoPos, (int)tileInfoCount);
        }
        Console.WriteLine("World scale = {0}, world height = {1}", header.worldScale, header.worldHeight);
    }

    public TerrainScene BuildManaged()
    {
        TerrainScene scene = new();
        scene.maxTerrainHeight = header.worldHeight;
        scene.tileSize = header.tileBaseSize;
        scene.worldScale = header.worldScale;
        scene.version = header.version;
        
        for (int i = 0; i < tileInfoOffsets.Length; i++) {
            var finalPos = ResolveSelfRelativeTileOffset(i);
            TerrainScene.TileInfo target = ReadManagedTile(data, finalPos);
            scene.tiles.Add(target);
        }
        
        for (int i = 0; i < matInfoOffsets.Length; i++) {
            var finalPos = ResolveSelfRelativeTileOffset(i);
            MaterialInfo mat = ReadUnsafe<MaterialInfo>(data, finalPos);
            unsafe {
                MaterialInfo.Swap(&mat);
            }
            scene.materials.Add(mat);
        }
        
        return scene;
    }

    public static TerrainScene BuildManagedFromData(ReadOnlySpan<byte> data) {
        TerrainSceneBinary bin = new(data);
        return bin.BuildManaged();
    }

    public static TerrainScene.TileInfo ReadManagedTile(ReadOnlySpan<byte> data, long offset)
    {
        TerrainScene.TileInfo target = new();
        TileInfo src = ReadUnsafe<TileInfo>(data, offset);
        unsafe {
            TileInfo.Swap(&src);
        }
        target.x = src.x;
        target.z = src.z;
        target.tileSize = src.tileSize;
        target.minLandHeight = src.minLandHeight;
        target.maxLandHeight = src.maxLandHeight;
        target.minWaterHeight = src.minWaterHeight;
        target.maxWaterHeight = src.maxWaterHeight;
        target.unknown1 = src.unknown1;
        target.unknown2 = src.unknown2;
        target.unknown3 = src.unknown3;

        const int OFFSET_OF_BASENAME_OFFSET = 0x20;
        var basenamePos = (int)(offset + src.basenameOffset + OFFSET_OF_BASENAME_OFFSET);
        // Find length via distance from the offset to the null terminator
        var basenameLen = data.Slice(basenamePos).IndexOf((byte)0);
        var basenameSpan = data.Slice(basenamePos, basenameLen);
        target.basename = Encoding.ASCII.GetString(basenameSpan);

        // We're done if there's no extra grass/water data
        if (src.extraInfoPresent == 0) {
            return target;
        }

        var readPos = offset + TILE_INFO_SIZE;
        
        // This extra array is very weird, check the wiki for more explanation:
        // https://zeldamods.org/wiki/TSCB#Extra_Information_Array
        uint arrayLen = Swap(ReadUnsafe<uint>(data, readPos)) / 4;
        if (arrayLen != 1 && arrayLen != 2) {
            // This should never happen
            Console.WriteLine("TSCB error: extra tile array @ 0x{0:x} had {1} entries", readPos, arrayLen);
            return target;
        }
        
        readPos += 4;
        if (arrayLen == 2) {
            // Skip over some unknown data that only appears in this case
            readPos += 4;
        }

        Debug.Assert(arrayLen >= 1);
        
        // Read the entries to find out if there's grass and/or water data
        for (int i = 0; i < arrayLen; i++) {
            var entry = ReadUnsafe<ExtraTileInfo>(data, readPos);
            unsafe {
                ExtraTileInfo.Swap(&entry);
            }

            if (entry.isWater == 0) {
                target.hasGrass = true;
            } else {
                target.hasWater = true;
            }
            readPos += EXTRA_TILE_INFO_SIZE;
        }

        return target;
    }

    public long ResolveSelfRelativeTileOffset(int idx) {
        uint offset = Swap(tileInfoOffsets[idx]);
        int offsetPos = tileInfoPos + idx * 4;
        long finalPos = offsetPos + offset; // Offset is relative to itself
        return finalPos;
    }
    
    public long ResolveSelfRelativeMaterialoffset(int idx) {
        uint offset = Swap(matInfoOffsets[idx]);
        int offsetPos = matInfoPos + idx * 4;
        long finalPos = offsetPos + offset; // Offset is relative to itself
        return finalPos;
    }
}
