// Created Jul. 15 2026
// @author Torphedo
using terrainBench;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using terrainBench.Settings;

public static class Program {
    public static void printUsage() {
        Console.WriteLine("Usage: terrainBench [base game folder] [update folder] [dlc folder (optional)]");
    }

    public static void Main(string[] args) {
        if (args.Length < 2) {
            printUsage();
            return;
        }

        string basePath = args[0];
        string updatePath = args[1];
        string dlcPath = (args.Length > 2) ? args[2] : "";
        var settings = Settings.Load();
        if (!Settings.Validate(settings))
        {
            settings.gameDir = basePath;
            settings.updateDir = updatePath;
            settings.dlcDir = dlcPath;
            settings.Save();
        }
        var game = new Game(settings.gameDir, settings.updateDir, settings.dlcDir);

        var nativeWindowSettings = new NativeWindowSettings() {
            ClientSize = new Vector2i(800, 600),
            Title = "Terrain Workbench",
            Flags = ContextFlags.ForwardCompatible, // Required to run on MacOS
        };

        using (var window = new Window(GameWindowSettings.Default, nativeWindowSettings, game)) {
            window.Run();
        }
    }
}
