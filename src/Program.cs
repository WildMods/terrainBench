// Created Jul. 15 2026
// @author Torphedo

using Avalonia;
using terrainBench.Settings;

public static class Program
{
    const bool overrideEnableTracyProfiler = false;

    public static void printUsage()
    {
        Console.WriteLine("Usage: terrainBench [base game folder] [update folder] [dlc folder (optional)]");
    }

    public static void Main(string[] args)
    {
        bool isDebug = false;
#if DEBUG
            isDebug = true;
#endif
        if (!isDebug && !overrideEnableTracyProfiler) {
            Profiler.tracyDisabled = true;
        }
        
        Profiler.AppInfo("BOTW terrain editor");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() {
        return AppBuilder.Configure<terrainBench.UI.App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions { RenderingMode = new List<Win32RenderingMode> { Win32RenderingMode.Wgl } })
            .WithInterFont()
            .LogToTrace();
    }

}
