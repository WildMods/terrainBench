// Created Jul. 15 2026
// @author Torphedo
using terrainBench;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

public static class Program {
    public static void printUsage() {
        Console.WriteLine("Usage: terrainBench [base game folder]");
    }

    public static void Main(string[] args) {
        if (args.Length < 1) {
            printUsage();
            return;
        }

        string basePath = args[0];
        string updatePath = (args.Length > 1) ? args[1] : "";
        string dlcPath = (args.Length > 2) ? args[2] : "";
        var game = new Game(basePath, updatePath, dlcPath);

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
