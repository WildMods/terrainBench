namespace terrainBench;
using System.Text.Json;
using System.Text.Json.Serialization;
using OperationResult;
using static OperationResult.Helpers;

// Short for "Global Messages".
// This is a common instance all parts of the program can pull from without
// needing to get passed extra context parameters.
public static class GMsg {
    public static Localization l;
}

public class Localization {
    private static JsonSerializerOptions _jsOpt = new() { WriteIndented = true };
    static string jsonPath = "Localization/lang_default.json";

    public sealed class MenuStrings {
        [JsonInclude] public string file;
        [JsonInclude] public string camera;
        [JsonInclude] public string brush;
        [JsonInclude] public string render;
        [JsonInclude] public string settings;
        [JsonInclude] public string help;
    }

    public sealed class CameraStrings {
        [JsonInclude] public string mode;
        [JsonInclude] public string modeOrbit;
        [JsonInclude] public string modeMinecraft;
        [JsonInclude] public string modeFly;
        
        [JsonInclude] public string moveSpeed;
        [JsonInclude] public string fov;
        [JsonInclude] public string mouseSens;
        [JsonInclude] public string invertX;
        [JsonInclude] public string invertY;
    }

    [JsonInclude] public MenuStrings menus;
    [JsonInclude] public CameraStrings camera;

    public static Result<Localization, ErrorStack> Load() {
        var txtRes = IO.ReadAllText(jsonPath);
        if (txtRes.IsErr()) {
            var err = txtRes.Err();
            return Err(err.Context("Failed to load localization file."));
        }

        var txt = txtRes.Unwrap();
        
        var val = JsonSerializer.Deserialize<Localization>(txt, _jsOpt);
        if (val == null) {
            return Err(new ErrorStack("Failed to parse localization file."));
        }

        return val;
    }
    
    public void Save() {
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(this, _jsOpt));
    }
}