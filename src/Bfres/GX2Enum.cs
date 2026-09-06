
// The GX2 enums come from https://pastebin.com/DCrP1w9x, linked on the MK8 wiki page for FTEX.
// I couldn't find this header file anywhere else on the internet, and DuckDuckGo
// couldn't even find the Pastebin by filename.
// The extremely professional comments and prototype macros are unusually good
// (they're more or less uncommented in DevKitPro, RetroArch, and other versions
// of these enums). I'm a bit worried to be using this source because it seems
// professional enough to be the real thing, maybe an SDK leak or something.
// 
// I'm going to use it anyway because BfresLibrary's enums appear to be based
// directly on this header, and if it hasn't been taken down by now, it's
// probably fine.
// -- torf

/// <summary>Indicates the "shape" of a given surface or texture</summary>
public enum GX2SurfaceDimension {
    DIM_1D = 0x0,
    DIM_2D = 0x1,
    DIM_3D = 0x2,
    DIM_CUBE = 0x3,
    DIM_1D_ARRAY = 0x4,
    DIM_2D_ARRAY = 0x5,
    DIM_2D_MSAA = 0x6,
    DIM_2D_MSAA_ARRAY = 0x7,
    DIM_FIRST = DIM_1D,
    DIM_LAST = DIM_2D_MSAA_ARRAY,
};

/// \brief Indicates desired texture, color-buffer, depth-buffer, or scan-buffer format.
///
/// After the word "format", the following letters indicate the possible uses:
/// T=texture, C=color-buffer, D=depth/stencil-buffer, S=scan-buffer.
///
/// There are some formats with the same enum value, but different use labels.
/// These are provided as a convenience to explain the type information for each use.
///
/// Type conversion options:
/// - UNORM (0): surface unsigned integer is converted to/from [0.0, 1.0] in shader
/// - UINT  (1): surface unsigned integer is copied to/from shader as unsigned int
/// - SNORM (2): surface signed integer is converted to/from [-1.0, 1.0] in shader
/// - SINT  (3): surface signed integer is copied to/from shader as signed int
/// - SRGB  (4): SRGB degamma performed on surface read, then treated as UNORM;
///              SRGB gamma is performed on surface write
/// - FLOAT (8): surface float is copied to/from shader as float
///
/// Note: As textures, all UINT/SINT formats may be point-sampled only!
///
/// The numbers in the names indicate the number of bits per channel, as well as
/// how many channels are present.  An "X" in front of a number indicates padding
/// bits that are present, but do not map to any channel.
///
/// Texture color channel mappings:
/// - 1-channel formats map to R [GBA are undefined]
/// - 2-channel formats map to RG [BA are undefined]
/// - 3-channel formats map to RGB [A is undefined]
/// - 4-channel formats map to RGBA
///
/// Channel mapping can be changed using the GX2InitTextureCompSel API.
/// We advise you avoid referring to channels that don't exist in the format.
/// You should use the component select to choose constant values in those cases.
/// The default component selectors in GX2InitTexture map:
/// - 1-channel formats to R001
/// - 2-channel formats to RG01
/// - 3-channel formats to RGB1
/// - 4-channel formats to RGBA
///
/// To understand exact component bit placement, you must first understand the
/// basic machine unit that the components are packed into.  If each component
/// fits into a single unit, then the order is simply R,G,B,A.  If multiple
/// components are packed into a single unit, then the components are packed
/// in order starting from the LSB end.  In all cases, multi-byte machine units
/// are then written out in little-endian format.
///
/// Note 1: It is not presently possible to switch between depth and color buffer
/// uses for the same surface.  This requires a retiling, since the tile formats
/// are different and incompatible.  The texture unit can read depth-tiled buffers
/// (except for D24_S8 format).  The D24_S8 format requires tile-conversion before
/// it can be read by the texture unit.  Note that the two components have different
/// number formats, and only the depth part can be sampled with any filter more
/// complex than point-sampling.
/// It is needed to use T_R24_UNORM_X8 for reading depth buffer as
/// texture and T_X24_G8_UINT for reading stencil buffer as texture.
/// See \ref GX2ConvertDepthBufferToTextureSurface() for more information.
///
/// Note 2: Similar to depth format D_D24_S8_UNORM and texture formats T_R24_UNORM_X8
/// and T_X24_G8_UINT, format D_D32_FLOAT_S8_UINT_X24 is a 
/// depth/stencil buffer format while T_R32_FLOAT_X8_X24 and T_X32_G8_UINT_X24 are
/// texture formats used to read the depth and stencil data, respectively. 
/// See \ref GX2ConvertDepthBufferToTextureSurface() for more information.
///
/// Note 3: The NV12 format is a special case for video.  It actually consists of
/// two surfaces (an 8-bit surface & a 1/4-size 16-bit surface).  It is only usable
/// in certain situations.
///
/// Final note: there may be additional restrictions not yet specified.
///
public enum GX2SurfaceFormat {
    /// color write performance relative to hardware peak write (x%)
    /// texture read performance relative to hardware peak read (y%)
    /// these numbers do not consider memory bandwidth limit
    /// there are still some investigations for missing areas
    INVALID                  = 0x00000000,
    /// color write (100%), texture read (100%)
    TC_R8_UNORM              = 0x00000001,
    /// color write (50%), texture read (100%)
    TC_R8_UINT               = 0x00000101,
    /// color write (100%), texture read (100%)
    TC_R8_SNORM              = 0x00000201,
    /// color write (50%), texture read (100%)
    TC_R8_SINT               = 0x00000301,
    /// texture read (100%)
    T_R4_G4_UNORM            = 0x00000002,
    /// color write (50%), texture read (100%)
    TCD_R16_UNORM            = 0x00000005,
    /// color write (50%), texture read (100%)
    TC_R16_UINT              = 0x00000105,
    /// color write (50%), texture read (100%)
    TC_R16_SNORM             = 0x00000205,
    /// color write (50%), texture read (100%)
    TC_R16_SINT              = 0x00000305,
    /// color write (100%), texture read (100%)
    TC_R16_FLOAT             = 0x00000806,
    /// color write (100%), texture read (100%)
    TC_R8_G8_UNORM           = 0x00000007,
    /// color write (50%), texture read (100%)
    TC_R8_G8_UINT            = 0x00000107,
    /// color write (100%), texture read (100%)
    TC_R8_G8_SNORM           = 0x00000207,
    /// color write (50%), texture read (100%)
    TC_R8_G8_SINT            = 0x00000307,
    /// color write (100%), texture read (100%)
    TCS_R5_G6_B5_UNORM       = 0x00000008,
    /// color write (100%), texture read (100%)
    TC_R5_G5_B5_A1_UNORM     = 0x0000000a,
    /// color write (100%), texture read (100%)
    TC_R4_G4_B4_A4_UNORM     = 0x0000000b,
    /// color write (100%), texture read (100%)
    TC_A1_B5_G5_R5_UNORM     = 0x0000000c, ///< flipped
    /// color write (50%)
    TC_R32_UINT              = 0x0000010d,
    /// color write (50%)
    TC_R32_SINT              = 0x0000030d,
    /// color write (50%)
    TCD_R32_FLOAT            = 0x0000080e,
    /// color write (50%)
    TC_R16_G16_UNORM         = 0x0000000f,
    /// color write (50%)
    TC_R16_G16_UINT          = 0x0000010f,
    /// color write (50%)
    TC_R16_G16_SNORM         = 0x0000020f,
    /// color write (50%)
    TC_R16_G16_SINT          = 0x0000030f,
    /// color write (100%)
    TC_R16_G16_FLOAT         = 0x00000810,
    D_D24_S8_UNORM           = 0x00000011, ///< note: same value as below
    T_R24_UNORM_X8           = 0x00000011, ///< see Note 1
    T_X24_G8_UINT            = 0x00000111, ///< see Note 1
    D_D24_S8_FLOAT           = 0x00000811,
    /// color write (100%)
    TC_R11_G11_B10_FLOAT     = 0x00000816,
    /// color write (100%)
    TCS_R10_G10_B10_A2_UNORM = 0x00000019,
    /// color write (50%)
    TC_R10_G10_B10_A2_UINT   = 0x00000119,
    /// color write (100%)
    TC_R10_G10_B10_A2_SNORM  = 0x00000219, ///< A2 part is UNORM
    /// color write (50%)
    TC_R10_G10_B10_A2_SINT   = 0x00000319,
    /// color write (100%)
    TCS_R8_G8_B8_A8_UNORM    = 0x0000001a,
    /// color write (50%)
    TC_R8_G8_B8_A8_UINT      = 0x0000011a,
    /// color write (100%)
    TC_R8_G8_B8_A8_SNORM     = 0x0000021a,
    /// color write (50%)
    TC_R8_G8_B8_A8_SINT      = 0x0000031a,
    /// color write (100%)
    TCS_R8_G8_B8_A8_SRGB     = 0x0000041a,
    /// color write (100%)
    TCS_A2_B10_G10_R10_UNORM = 0x0000001b, ///< flipped
    /// color write (50%)
    TC_A2_B10_G10_R10_UINT   = 0x0000011b, ///< flipped
    D_D32_FLOAT_S8_UINT_X24  = 0x0000081c, ///< note: same value as below
    T_R32_FLOAT_X8_X24       = 0x0000081c, ///< note: same value as above
    T_X32_G8_UINT_X24        = 0x0000011c, ///< see Note 2
    /// color write (50%)
    TC_R32_G32_UINT          = 0x0000011d,
    /// color write (50%)
    TC_R32_G32_SINT          = 0x0000031d,
    /// color write (50%)
    TC_R32_G32_FLOAT         = 0x0000081e,
    /// color write (50%)
    TC_R16_G16_B16_A16_UNORM = 0x0000001f,
    /// color write (50%)
    TC_R16_G16_B16_A16_UINT  = 0x0000011f,
    /// color write (50%)
    TC_R16_G16_B16_A16_SNORM = 0x0000021f,
    /// color write (50%)
    TC_R16_G16_B16_A16_SINT  = 0x0000031f,
    /// color write (50%)
    TC_R16_G16_B16_A16_FLOAT = 0x00000820,
    /// color write (25%)
    TC_R32_G32_B32_A32_UINT  = 0x00000122,
    /// color write (25%)
    TC_R32_G32_B32_A32_SINT  = 0x00000322,
    /// color write (25%)
    TC_R32_G32_B32_A32_FLOAT = 0x00000823,
    /// texture read (100%)
    T_BC1_UNORM              = 0x00000031,
    /// texture read (100%)
    T_BC1_SRGB               = 0x00000431,
    /// texture read (100%)
    T_BC2_UNORM              = 0x00000032,
    /// texture read (100%)
    T_BC2_SRGB               = 0x00000432,
    /// texture read (100%)
    T_BC3_UNORM              = 0x00000033,
    /// texture read (100%)
    T_BC3_SRGB               = 0x00000433,
    /// texture read (100%)
    T_BC4_UNORM              = 0x00000034,
    /// texture read (100%)
    T_BC4_SNORM              = 0x00000234,
    /// texture read (100%)
    T_BC5_UNORM              = 0x00000035,
    /// texture read (100%)
    T_BC5_SNORM              = 0x00000235,
    /// texture read (100%)
    T_NV12_UNORM             = 0x00000081, ///< see Note 3
    FIRST                    = TC_R8_UNORM,
    LAST                     = 0x0000083f
};

/// <summary>Indicates the MSAA mode (number of samples) for a surface</summary>
enum GX2AAMode {
    GX2_AA_MODE_1X    = 0x000,
    GX2_AA_MODE_2X    = 0x001,
    GX2_AA_MODE_4X    = 0x002,
    GX2_AA_MODE_8X    = 0x003,
    GX2_AA_MODE_FIRST = GX2_AA_MODE_1X,
    GX2_AA_MODE_LAST  = GX2_AA_MODE_8X
};

/// <summary>Indicates how a given surface may be used</summary>
///
/// A "final" TV render target is one that will be copied to a TV scan buffer.
/// It needs to be designated to handle certain display corner cases.
/// (When a HD surface must be scaled down to display in NTSC/PAL.)
enum GX2SurfaceUse {
    USE_TEXTURE                  = 0x001,
    USE_COLOR_BUFFER             = 0x002,
    USE_DEPTH_BUFFER             = 0x004,
 
    USE_SCAN_BUFFER              = 0x008,   // internal use only
    USE_FTV                      = (1<<31), // modifier, designates a final TV render target
    //** If changing or adding flags, ensure they match up / don't clash with GX2RResourceFlags
 
    USE_COLOR_BUFFER_TEXTURE     = USE_COLOR_BUFFER | USE_TEXTURE,
    USE_DEPTH_BUFFER_TEXTURE     = USE_DEPTH_BUFFER | USE_TEXTURE,
 
    USE_COLOR_BUFFER_FTV         = USE_COLOR_BUFFER | USE_FTV,
    USE_COLOR_BUFFER_TEXTURE_FTV = USE_COLOR_BUFFER_TEXTURE | USE_FTV,
 
    USE_FIRST                    = USE_TEXTURE,
    USE_LAST                     = USE_SCAN_BUFFER // note: without modifiers!
} ;

/// <summary>Indicates the desired tiling mode for a surface</summary>
enum GX2TileMode {
    MODE_DEFAULT        = 0x00000000, // driver will choose best mode
    MODE_LINEAR_SPECIAL = 0x00000010, // typically not supported by HW
    MODE_LINEAR_ALIGNED = 0x00000001, // supported by HW, but not fast
    MODE_1D_TILED_THIN1 = 0x00000002,
    MODE_1D_TILED_THICK = 0x00000003,
    MODE_2D_TILED_THIN1 = 0x00000004, // (a typical default, but not always)
    MODE_2D_TILED_THIN2 = 0x00000005,
    MODE_2D_TILED_THIN4 = 0x00000006,
    MODE_2D_TILED_THICK = 0x00000007,
    MODE_2B_TILED_THIN1 = 0x00000008,
    MODE_2B_TILED_THIN2 = 0x00000009,
    MODE_2B_TILED_THIN4 = 0x0000000a,
    MODE_2B_TILED_THICK = 0x0000000b,
    MODE_3D_TILED_THIN1 = 0x0000000c,
    MODE_3D_TILED_THICK = 0x0000000d,
    MODE_3B_TILED_THIN1 = 0x0000000e,
    MODE_3B_TILED_THICK = 0x0000000f,
    MODE_FIRST          = MODE_DEFAULT,
    MODE_LAST           = MODE_LINEAR_SPECIAL,
};

/// <summary>
/// Used to control attribute and texture component swizzling as well as
/// specifying values for elements not present in the format.
/// Analagous to OpenGL's channel swizzle.
/// </summary>
enum GX2Component {
    [System.ComponentModel.Description("R")]
    COMPONENT_R = 0x00000000, // X or red
    [System.ComponentModel.Description("G")]
    COMPONENT_G = 0x00000001, // Y or green
    [System.ComponentModel.Description("B")]
    COMPONENT_B = 0x00000002, // Z or blue
    [System.ComponentModel.Description("A")]
    COMPONENT_A = 0x00000003, // W or alpha
    [System.ComponentModel.Description("0")]
    CONST_0     = 0x00000004, // constant 0
    [System.ComponentModel.Description("1")]
    CONST_1     = 0x00000005, // constant 1
    GX2_COMPONENT_FIRST = COMPONENT_R,
    GX2_COMPONENT_LAST  = CONST_1,
};


public static class GX2 {
    public static bool IsFormatBCN(GX2SurfaceFormat format) {
        switch (format) {
            case GX2SurfaceFormat.T_BC1_UNORM:
            case GX2SurfaceFormat.T_BC1_SRGB:
            case GX2SurfaceFormat.T_BC2_UNORM:
            case GX2SurfaceFormat.T_BC2_SRGB:
            case GX2SurfaceFormat.T_BC3_UNORM:
            case GX2SurfaceFormat.T_BC3_SRGB:
            case GX2SurfaceFormat.T_BC4_UNORM:
            case GX2SurfaceFormat.T_BC4_SNORM:
            case GX2SurfaceFormat.T_BC5_SNORM:
            case GX2SurfaceFormat.T_BC5_UNORM:
                return true;
            default:
                return false;
        }
    }
    public static int BlockSize(GX2SurfaceFormat format) {
        return IsFormatBCN(format) ? 4 : 1;
    }

    public static uint CalcPitch(GX2SurfaceFormat format, uint width)
    {
        return (uint)(width / BlockSize(format));
    }

    public static uint CalcBitsPerPixel(uint pitch) {
        return (pitch * 8) / 512;
    }
    
    public static long CalcSurfaceSize(uint height, uint pitch, uint depth, uint aaMode) {
        var numSamples = 1 << (int)aaMode;
        return height * pitch * depth * numSamples;
    }
    
    public static long CalcSliceSize(uint height, uint pitch, uint aaMode) {
        var numSamples = 1 << (int)aaMode;
        return height * pitch * numSamples;
    }
}
