using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BotwFlagUtil.Models
{
    internal class Settings(string gameDir, string updateDir, string dlcDir, string gameDirNx, string dlcDirNx)
    {
        private static JsonSerializerOptions _jsOpt = new() { WriteIndented = true };
        private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "botw_tools", "settings.json");
        public bool WiiU = true;
        [JsonInclude]
        public string gameDir = gameDir;
        [JsonInclude]
        public string gameDirNx = gameDirNx;
        [JsonInclude]
        public string updateDir = updateDir;
        [JsonInclude]
        public string dlcDir = dlcDir;
        [JsonInclude]
        public string dlcDirNx = dlcDirNx;

        public static Settings Load()
        {
            Settings value;
            string settingsPath;
            if (File.Exists(SettingsPath))
            {
                value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath))!;
                if (Validate(value))
                {
                    return value;
                }
            }
            else if (
                (
                    settingsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "ukmm",
                        "settings.yml"
                    )
                ) != null
                && File.Exists(settingsPath)
            )
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();
                UkmmSettings ukmmSettings = deserializer.Deserialize<UkmmSettings>(File.ReadAllText(settingsPath));
                value = new(
                    ukmmSettings.WiiuConfig?.Dump?.Source?.ContentDir ?? "",
                    ukmmSettings.WiiuConfig?.Dump?.Source?.UpdateDir ?? "",
                    ukmmSettings.WiiuConfig?.Dump?.Source?.AocDir ?? "",
                    ukmmSettings.SwitchConfig?.Dump?.Source?.ContentDir ?? "",
                    ukmmSettings.SwitchConfig?.Dump?.Source?.AocDir ?? ""
                );
            }
            else if (
                (
                    settingsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "bcml",
                        "settings.json"
                    )
                ) != null
                && File.Exists(settingsPath)
            )
            {
                BcmlSettings? bcmlSettings =
                    JsonSerializer.Deserialize<BcmlSettings>(File.ReadAllText(settingsPath), BcmlSettings.JsOpt);
                value = new(
                    bcmlSettings?.GameDir ?? "",
                    bcmlSettings?.UpdateDir ?? "",
                    bcmlSettings?.DlcDir ?? "",
                    bcmlSettings?.GameDirNx ?? "",
                    bcmlSettings?.DlcDirNx ?? ""
                );
            }
            else
            {
                value = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            }

            if (!Directory.Exists(Directory.GetParent(SettingsPath)!.FullName))
            {
                Directory.CreateDirectory(Directory.GetParent(SettingsPath)!.FullName);
            }
            File.WriteAllText(
                SettingsPath,
                JsonSerializer.Serialize(value, _jsOpt)
            );
            return value;
        }

        public void Save()
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, _jsOpt));
        }

        public static bool Validate(Settings value)
        {
            if (value is null or { gameDir: "", gameDirNx: "" })
            {
                return false;
            }
            if (value.gameDir != string.Empty)
            {
                if (!ValidateGameDir(value.gameDir))
                {
                    return false;
                }
                if (!ValidateUpdateDir(value.updateDir))
                {
                    return false;
                }
                if (value.dlcDir != string.Empty && !ValidateDlcDir(value.dlcDir))
                {
                    return false;
                }
            }

            if (value.gameDirNx == string.Empty) return true;
            if (!ValidateGameDirNx(value.gameDirNx))
            {
                return false;
            }
            return value.dlcDirNx == string.Empty || ValidateDlcDirNx(value.dlcDirNx);
        }

        public static bool ValidateGameDir(string path) => File.Exists(Path.Combine(path, "Pack", "Dungeon000.pack"));
        public static bool ValidateUpdateDir(string path) => File.Exists(Path.Combine(path, "Actor", "Pack", "FldObj_MountainSnow_A_M_02.sbactorpack"));
        public static bool ValidateDlcDir(string path) => File.Exists(Path.Combine(path, "Pack", "AocMainField.pack"));
        public static bool ValidateGameDirNx(string path) => ValidateGameDir(path) & ValidateUpdateDir(path);
        public static bool ValidateDlcDirNx(string path) => ValidateDlcDir(path);
    }
}
