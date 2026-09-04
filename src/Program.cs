// Created Jul. 15 2026
// @author Torphedo

using Avalonia;
using terrainBench.Settings;
using terrainBench.UI;

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

        Window w = new(args);
        w.Run();
        w.OnClosed();
    }
}
