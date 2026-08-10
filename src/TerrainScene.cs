using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Entish;
using Entish.Attributes;
using static Entish.EndianUtils;
namespace terrainBench;

// Represents data loaded from the Terrain Scene Binary (TSCB) file
public readonly ref struct TerrainScene
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

    private const int TILE_INFO_SIZE = 0x30;
    // [Swappable]
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
    // [Swappable]
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public struct ExtraTileInfo {
        public uint unknown1; // Always 3
        public uint isWater;  // If 0, it's grass
        public uint unknown2;
        public uint unknown3;
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
    
    public TerrainScene(ReadOnlySpan<byte> data) {
        this.data = data;
        int matInfoCount;
        int tileInfoCount;
        unsafe {
            var headerRef = ReadUnsafe<TSCBHeader>(data, 0);
            TSCBHeader.Swap(&headerRef);
            header = headerRef;
            
            // Parse material info.
            // See https://zeldamods.org/wiki/TSCB#Material_Information_Array
            int matInfoSize = Swap(ReadUnsafe<int>(data, sizeof(TSCBHeader)));
            matInfoCount = matInfoSize / sizeof(uint);
            matInfoPos = sizeof(TSCBHeader) + 4;
            matInfoOffsets = SpanSegment<uint>(data, matInfoPos, matInfoCount);

            // Parse tile info (wiki uses "tile" and "area" interchangeably).
            // See https://zeldamods.org/wiki/TSCB#Area_Array
            tileInfoPos = sizeof(TSCBHeader) + matInfoSize;
            int tileInfoSize = Swap(ReadUnsafe<int>(data, tileInfoPos));
            tileInfoCount = tileInfoSize / sizeof(uint);
            tileInfoPos += 4;
            tileInfoOffsets = SpanSegment<uint>(data, tileInfoPos, tileInfoCount);
        }
        Console.WriteLine("World scale = {0}, world height = {1}", header.worldScale, header.worldHeight);
    }

    public TileInfo GetTile(int idx) {
        uint offset = Swap(tileInfoOffsets[idx]);
        int offsetPos = tileInfoPos + idx * 4;
        long finalPos = offsetPos + offset; // Offset is relative to itself

        TileInfo tile = ReadUnsafe<TileInfo>(data, finalPos);
        unsafe {
            TileInfo.Swap(&tile);
        }

        return tile;
    }
}