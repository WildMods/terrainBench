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
        var settings = Settings.Load();
        if (!Settings.Validate(settings))
        {
            if (args.Length < 2) {
                printUsage();
                return;
            }
            settings.gameDir = args[0];
            settings.updateDir = args[1];
            settings.dlcDir = args.Length > 2 ? args[2] : "";
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
