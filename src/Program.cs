// Created Jul. 15 2026
// @author Torphedo

using Avalonia;
using terrainBench.Settings;

public static class Program
{
    public static void printUsage()
    {
        Console.WriteLine("Usage: terrainBench [base game folder] [update folder] [dlc folder (optional)]");
    }

    public static void Main(string[] args)
    {
        var settings = Settings.Load();
        if (!Settings.Validate(settings))
        {
            if (args.Length < 2)
            {
                printUsage();
                return;
            }

            settings.gameDir = args[0];
            settings.updateDir = args[1];
            settings.dlcDir = args.Length > 2 ? args[2] : "";
            settings.Save();
        }
        Profiler.AppInfo("BOTW terrain editor");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() {
        return AppBuilder.Configure<terrainBench.UI.App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

}
