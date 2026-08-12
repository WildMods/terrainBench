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
    public BrushRenderer brush = new();
    public Camera cam = new();

    [ObservableProperty] public bool loadMaxIsIndeterminate = false;
    [ObservableProperty] public AtomicCounter loadedTileCountAsync = new(0);
    [ObservableProperty] public int currentLoadedTileCount = 0;
    [ObservableProperty] public int totalTileCount = 18100;
    
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
