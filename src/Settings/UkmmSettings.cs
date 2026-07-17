using static System.String;

namespace BotwFlagUtil.Models;

public class UkmmSettings
{
    public string CurrentMode { get; set; } = "WiiU";
    // ReSharper disable once InconsistentNaming
    public bool system_7z { get; set; } = true;
    public string StorageDir { get; set; } = Empty;
    public string CheckUpdates { get; set; } = Empty;
    public bool ShowChangelog { get; set; } = true;
    public string LastVersion { get; set; } = Empty;
    public PlatformConfig? WiiuConfig { get; set; } = null;
    public PlatformConfig? SwitchConfig { get; set; } = null;
    public string Lang { get; set; } = "USen";
}

public class PlatformConfig
{
    public string Language { get; set; } = Empty;
    public string Profile { get; set; } = Empty;
    public DumpConfig? Dump { get; set; } = null;
    public DeployConfig? DeployConfig { get; set; } = null;
}

public class DumpConfig
{
    public string BinType { get; set; } = Empty;
    public SourceConfig? Source { get; set; } = null;
    public string Endian { get; set; } = Empty;
}

public class DeployConfig
{
    public string Output { get; set; } = Empty;
    public string Method { get; set; } = Empty;
    public bool Auto { get; set; }
    public bool CemuRules { get; set; }
    public string Executable { get; set; } = Empty;
    public string Layout { get; set; } = Empty;
}

public class SourceConfig
{
    public string Type { get; set; } = Empty;
    public string? HostPath { get; set; } = Empty;
    public string? ContentDir { get; set; } = Empty;
    public string? UpdateDir { get; set; } = Empty;
    public string? AocDir { get; set; } = Empty;
}
