using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTK.Mathematics;

namespace terrainBench.UI.ViewModels;
public partial class EditorState : ObservableObject {
    public enum BootState {
        INIT, TILES_LOADING, SHOW_UPLOAD_MSG, LOAD_TERRAIN_TEXTURES, UPLOADING, DONE,
    }
    
    public const string defaultWindowTitle = "Terrain Workbench";
    public const string gpuUploadWindowTitle = "Uploading to GPU...";

    [ObservableProperty]
    public string _controlsInformation = @"
W, A, S, D
   => Move camera forward/left/backwards/right
Space, Shift
   => Move camera up/down
Hold Middle mouse button & drag
   => Rotate camera
Scroll
   => Change zoom (orbit mode only)
M
   => Change camera mode
            ";
    
    [ObservableProperty]
    private string _creditInformation = "Written by Torphedo & Ginger Chody";
    
    [ObservableProperty]
    private string _glInformation = "Graphics API info placeholder"; // Filled @ runtime

    [ObservableProperty]
    private BootState _bootProgress = BootState.INIT;

    public Game game;
    public Cache.Cache cache = new();
    public TerrainRenderer terrain = new();
    [ObservableProperty] public Brush brush = new();
    public BrushRenderer brushRenderer = new();
    public Camera cam = new();
    public bool runRendererUpload = true;

    // I couldn't get Avalonia to access public fields of objects on this class,
    // so I'm forced to use wrapper properties to access them in XAML.
    // Sorry for all this useless wrapper code. -- torf
    
    // Render settings
    public int renderDistance { get => terrain.renderRadius; set => terrain.renderRadius = value; }
    
    // Brush settings access
    public float brushBaseStrength {
        get => brush.baseStrength; set =>  brush.baseStrength = value;
    }
    public float brushFalloffStrength {
        get => brush.falloffStrength; set =>  brush.falloffStrength = value;
    }
    public int brushRadius {
        get => brush.radius; set =>  brush.radius = value;
    }
    public Brush.FalloffFunc[] brushFalloffTypes { get; } = Enum.GetValues<Brush.FalloffFunc>();
    public Brush.EditFunc[] brushEditTypes { get; } = Enum.GetValues<Brush.EditFunc>();
    public Brush.DistanceType[] brushDistanceTypes { get; } = Enum.GetValues<Brush.DistanceType>();
    public LodComponent[] brushTargetTypes { get; } = { LodComponent.hght, LodComponent.mate };
    
    public Brush.FalloffFunc brushFalloff {
        get => brush.func; set =>  brush.func = value;
    }
    public Brush.EditFunc brushEditType {
        get => brush.editFunc; set =>  brush.editFunc = value;
    }
    public Brush.DistanceType brushDistanceType {
        get => brush.falloffShape; set =>  brush.falloffShape = value;
    }
    public LodComponent brushTargetType { get => brush.target; set =>  brush.target = value; }
    
    // Camera settings access
    public float camSpeed { get => cam.move_speed; set => cam.move_speed = value; }
    public float camFOV { get => cam.fov_degrees; set => cam.fov_degrees = value; }
    public float camMouseSens { get => cam.mouse_sens; set => cam.mouse_sens = value; }
    public bool camInvertX { get => cam.invert_mouse_x; set => cam.invert_mouse_x = value; }
    public bool camInvertY { get => cam.invert_mouse_y; set => cam.invert_mouse_y = value; }
    public Camera.Mode[] camModes { get; } = { Camera.Mode.ORBIT, Camera.Mode.MINECRAFT, Camera.Mode.FLY };
    public Camera.Mode camMode { get => cam.mode; set => cam.mode = value; }

    // Asynchronously updated progress data
    public ProgressReport asyncLoadedTiles = new();
    public int uiTotalTiles = 18100;
    
    [ObservableProperty] public string uiProgressText = "Loaded {0}/{3} tiles ({1:0}%)";

    [RelayCommand]
    private void Save() {
        cache.WriteAllTiles(game.modPath, true, CsOead.Endianness.Big);
    }

    public void RendererUploadThread() {
        while (runRendererUpload) {
            var eyeWorld = new TerrainCoords.WorldPos(cam.eye());
            TerrainCoords.TileGrid8Pos eyeTile = eyeWorld;
            terrain.UpdateGPUTiles(cache, (Vector2i)eyeTile.xz.Truncate());
            Thread.Sleep(1);
        }
    }

    public bool ReloadFromHeightmap(string path) {
        // Clear GPU state except textures which don't change
        runRendererUpload = false;
        Thread.Sleep(5); // Wait for renderer upload thread to exit
        terrain.UnloadTerrainTiles();
        
        // Load into a fresh cache
        cache.Clear();
        // Pass LOD -1 to auto-select
        cache.LoadFromImage(path, -1, asyncLoadedTiles);

        // Trigger a fresh bootup on the GL thread
        runRendererUpload = true;
        BootProgress = BootState.UPLOADING;
        return true;
    }

    public EditorState(string[] args) {
        var settings = Settings.Settings.Load();
        if (!Settings.Settings.Validate(settings)) {
            if (args.Length < 2) {
                // printUsage();
                return;
            }

            settings.gameDir = args[0];
            settings.updateDir = args[1];
            settings.dlcDir = args.Length > 2 ? args[2] : "";
            settings.modDir =  args.Length > 3 ? args[3] : "";
            settings.Save();

        }

        if (settings.modDir.Length != 0 && !settings.modDir.IsWhiteSpace()) {
            Directory.CreateDirectory(settings.modDir);
        }

        Console.WriteLine("Base: '{0}'", settings.gameDir);
        Console.WriteLine("Update: '{0}'", settings.updateDir);
        Console.WriteLine("DLC: '{0}'", settings.dlcDir);
        Console.WriteLine("Mod folder: '{0}'", settings.modDir);
        game = new Game(settings.gameDir, settings.updateDir, settings.dlcDir, settings.modDir);
    }
}
