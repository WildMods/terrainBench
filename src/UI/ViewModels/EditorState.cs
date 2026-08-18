using CommunityToolkit.Mvvm.ComponentModel;

namespace terrainBench.UI.ViewModels;
public partial class EditorState : ObservableObject {
    public enum BootState {
        INIT, TILES_LOADING, SHOW_UPLOAD_MSG, UPLOADING, DONE,
    }
    
    public const string defaultWindowTitle = "Terrain Workbench";
    public const string gpuUploadWindowTitle = "Uploading to GPU...";

    [ObservableProperty]
    public string _controlsInformation = @"
W, A, S, D
   => Move camera forward/left/backwards/right
Space, Shift
   => Move camera up/down
Hold right mouse button & drag
   => Rotate camera
Scroll
   => Change zoom (orbit mode only)
Middle click
   => Change camera mode
            ";
    
    [ObservableProperty]
    private string _creditInformation = "Written by Torphedo & Ginger Chody";
    
    [ObservableProperty]
    private string _glInformation = "Here the base information will be shown";

    [ObservableProperty]
    private BootState _bootProgress = BootState.INIT;

    public Game game;
    public Cache.Cache cache = new();
    public TerrainRenderer terrain = new();
    [ObservableProperty] public Brush brush = new();
    public BrushRenderer brushRenderer = new();
    public Camera cam = new();

    // I couldn't get Avalonia to access public fields of objects on this class,
    // so I'm forced to use wrapper properties to access them in XAML.
    // Sorry for all this useless wrapper code. -- torf
    
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
    
    public Brush.FalloffFunc brushFalloff {
        get => brush.func; set =>  brush.func = value;
    }
    public Brush.EditFunc brushEditType {
        get => brush.editFunc; set =>  brush.editFunc = value;
    }
    public Brush.DistanceType brushDistanceType {
        get => brush.falloffShape; set =>  brush.falloffShape = value;
    }
    
    // Camera settings access
    public float camSpeed { get => cam.move_speed; set => cam.move_speed = value; }
    public float camFOV { get => cam.fov_degrees; set => cam.fov_degrees = value; }
    public float camMouseSens { get => cam.mouse_sens; set => cam.mouse_sens = value; }
    public bool camInvertX { get => cam.invert_mouse_x; set => cam.invert_mouse_x = value; }
    public bool camInvertY { get => cam.invert_mouse_y; set => cam.invert_mouse_y = value; }
    public Camera.Mode[] camModes { get; } = { Camera.Mode.ORBIT, Camera.Mode.MINECRAFT, Camera.Mode.FLY };
    public Camera.Mode camMode { get => cam.mode; set => cam.mode = value; }

    // Asynchronously updated progress data
    [ObservableProperty] public ProgressReport asyncLoadedTiles = new();
    
    // Copies of async progress data pulled by the UI
    // Avalonia will only trigger UI updates when values have their setter called.
    // This apparently doesn't trivially extend to whole objects being copied or
    // edited via methods. I couldn't get Avalonia to forcefully refresh a binding
    // even by manually invoking OnPropertyChanged().
    // So, the only working solution I've found is to constantly copy the
    // atomically updated fields to these primitive properties, forcing Avalonia
    // to recognize that the value has changed.
    [ObservableProperty] public bool uiLoadIndeterminate = false;
    [ObservableProperty] public int uiLoadedTiles = 0;
    [ObservableProperty] public int uiTotalTiles = 18100;
    
    [ObservableProperty] public string uiProgressText = "Loaded {0}/{3} tiles ({1:0}%)";
    
    public EditorState(string[] args) {
        string modPath = "./TerrainMod";
        var settings = Settings.Settings.Load();
        if (!Settings.Settings.Validate(settings)) {
            if (args.Length < 2) {
                // printUsage();
                return;
            }

            settings.gameDir = args[0];
            settings.updateDir = args[1];
            settings.dlcDir = args.Length > 2 ? args[2] : "";
            settings.Save();

            if (args.Length > 3) {
                modPath = args[3];
            }
        }

        Directory.CreateDirectory(modPath);
        
        Console.WriteLine("Base: '{0}'", settings.gameDir);
        Console.WriteLine("Update: '{0}'", settings.updateDir);
        Console.WriteLine("DLC: '{0}'", settings.dlcDir);
        game = new Game(settings.gameDir, settings.updateDir, settings.dlcDir, modPath);
    }
}
