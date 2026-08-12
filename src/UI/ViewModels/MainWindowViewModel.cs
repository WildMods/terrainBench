using CommunityToolkit.Mvvm.ComponentModel;

using terrainBench;
namespace terrainBench.UI.ViewModels;
public partial class MainWindowViewModel : ViewModelBase {
    [ObservableProperty]
    private string _baseInformation = "Here the base information will be shown";

    [ObservableProperty]
    private string _controlsInformation = "Here the controls information will be shown";

    public Game game;
    public Cache.Cache cache = new();
    public TerrainRenderer terrain = new();
    public BrushRenderer brush = new();
    public Camera cam = new();

    [ObservableProperty] public bool loadMaxIsIndeterminate = false;
    [ObservableProperty] public AtomicCounter loadedTileCountAsync = new(0);
    [ObservableProperty] public int currentLoadedTileCount = 0;
    [ObservableProperty] public int totalTileCount = 18100;
    [ObservableProperty] public bool cacheLoadFinished = false;
    [ObservableProperty] public bool gpuLoadFinished = false;
    
    public MainWindowViewModel(string[] args) {
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
        
        // cache.Load(game);
        // Console.WriteLine("Finished loading cache.");
    }
}