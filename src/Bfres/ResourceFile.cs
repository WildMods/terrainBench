using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Entish;
using Entish.Attributes;
using static Entish.EndianUtils;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench;
using static UnsafeUtil;

public class ResourceFile {
    public enum Platform {
        WiiU,
        Switch,
    }
    
    public static Result<Platform, ErrorStack> GetBFRESPlatform(ReadOnlySpan<byte> data) {
        var wiiuHeader = ReadUnsafe<ResFileHeaderWiiU>(data, 0);
        if (!wiiuHeader.IsBFRES()) {
            return Err(new ErrorStack("The data is not a valid BFRES file [wrong magic value]"));
        }

        // This is the version used in BOTW
        if (wiiuHeader.CheckVersion(4, 5, 0, 3)) {
            return Platform.WiiU;
        }
        if (wiiuHeader.CheckVersion(0x20, 0x20, 0x20, 0x20)) {
            // The Switch files have 8-byte magic fields, so the location for the
            // version number on Wii U is filled with spaces
            return Platform.Switch;
        }

        return Err(new ErrorStack("Unknown BFRES platform"));
    }
}
