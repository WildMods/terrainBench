using System.Text.Json;
using static System.String;

namespace BotwFlagUtil.Models;

public class BcmlSettings
{
    public static JsonSerializerOptions JsOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };
    public string CemuDir { get; set; } = Empty;
    public string GameDir { get; set; } = Empty;
    public string GameDirNx { get; set; } = Empty;
    public string UpdateDir { get; set; } = Empty;
    public string DlcDir { get; set; } = Empty;
    public string DlcDirNx { get; set; } = Empty;
    public string StoreDir { get; set; } = Empty;
    public string ExportDir { get; set; } = Empty;
    public string ExportDirNx { get; set; } = Empty;
    public bool LoadReverse { get; set; }
    public string SiteMeta { get; set; } = Empty;
    public bool NoGuess { get; set; }
    public string Lang { get; set; } = Empty;
    public bool NoCemu { get; set; }
    public bool Wiiu { get; set; }
    public bool NoHardlinks { get; set; }
    public bool Force7z { get; set; }
    public bool SuppressUpdate { get; set; }
    public bool Loaded { get; set; }
    public bool Nsfw { get; set; }
    public bool Changelog { get; set; }
    public bool StripGfx { get; set; }
    public bool AutoGb { get; set; }
    public bool ShowGb { get; set; }
    public bool DarkTheme { get; set; }
    public string LastVersion { get; set; } = Empty;
}