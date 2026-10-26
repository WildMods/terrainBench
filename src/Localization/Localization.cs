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

    public sealed class StdStrings {
        [JsonInclude] public string save;
        [JsonInclude] public string quit;
        [JsonInclude] public string open;
        [JsonInclude] public string close;
        [JsonInclude] public string apply;
        [JsonInclude] public string browse;
    }

    public sealed class MenuStrings {
        [JsonInclude] public string file;
        [JsonInclude] public string fileImportHeightmap;
        [JsonInclude] public string camera;
        [JsonInclude] public string brush;
        [JsonInclude] public string render;
        [JsonInclude] public string settings;
        [JsonInclude] public string help;
        [JsonInclude] public string about;
        [JsonInclude] public string showDemo;
        [JsonInclude] public string ringSettings;
        [JsonInclude] public string wireframeMode;
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
    
    public sealed class BrushStrings {
        [JsonInclude] public string strength;
        [JsonInclude] public string falloff;
        [JsonInclude] public string radiusPixels;
        [JsonInclude] public string editFunction;
        [JsonInclude] public string falloffFunction;
        [JsonInclude] public string falloffShape;
        [JsonInclude] public string brushMode;
        
        [JsonInclude] public string falloffLinear;
        [JsonInclude] public string falloffInverse;
        [JsonInclude] public string falloffSquare;
        [JsonInclude] public string falloffInverseSquare;
        [JsonInclude] public string shapeSquare;
        [JsonInclude] public string shapeCircle;
        
        [JsonInclude] public string tileTypeHGHT;
        [JsonInclude] public string tileTypeMATE;
        [JsonInclude] public string tileTypeGRASS;
        [JsonInclude] public string tileTypeWATER;
    }

    [JsonInclude] public StdStrings std;
    [JsonInclude] public MenuStrings menus;
    [JsonInclude] public CameraStrings camera;
    [JsonInclude] public BrushStrings brush;

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