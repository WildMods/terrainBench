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
    
    public Brush.FalloffFunc brushFalloff {
        get => brush.func; set =>  brush.func = value;
    }

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
            settings.Save();
        }
        
        Console.WriteLine("Base: '{0}'", settings.gameDir);
        Console.WriteLine("Update: '{0}'", settings.updateDir);
        Console.WriteLine("DLC: '{0}'", settings.dlcDir);
        game = new Game(settings.gameDir, settings.updateDir, settings.dlcDir);
    }
}
