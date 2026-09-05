using OpenTK.Mathematics;

namespace terrainBench.Core;

public partial class EditorState {
    public enum BootState {
        INIT, TILES_LOADING, SHOW_UPLOAD_MSG, LOAD_TERRAIN_TEXTURES, UPLOADING, DONE,
    }
    
    public static string defaultWindowTitle = $"{AboutSelf.name} {AboutSelf.version}";
    public const string gpuUploadWindowTitle = "Uploading to GPU...";

    public string controlsInfo = @"
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
    
    public string creditInfo = "Written by Torphedo & Ginger Chody";
    
    private string glInfo = "Graphics API info placeholder"; // Filled @ runtime

    public BootState bootProgress = BootState.INIT;

    public Game game;
    public Cache cache = new();
    public Graphics.TerrainRenderer terrain = new();
    public Brush brush = new();
    public Graphics.BrushRenderer brushRenderer = new();
    public Camera cam = new();
    public bool runRendererUpload = true;

    // Asynchronously updated progress data
    public ProgressReport asyncLoadedTiles = new();
    public int uiTotalTiles = 18100;
    
    public string uiProgressText = "Loaded {0}/{3} tiles ({1:0}%)";

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
        bootProgress = BootState.UPLOADING;
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
