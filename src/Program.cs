// Created Jul. 15 2026
// @author Torphedo

using terrainBench;
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

        if (args.Length == 1 && args[0] == "--version") {
            Console.WriteLine(AboutSelf.AboutText());
            return;
        }
        if (args.Length == 1 && args[0] == "--help") {
            Console.WriteLine("Usage: terrainBench [base game folder] [update folder] [DLC folder] [mod folder (optional)]");
            Console.WriteLine("Assuming you have a correct copy of the game, the editor will save the paths you give after the first run.");
            Console.WriteLine("The mod folder can be changed in the GUI.");
            return;
        }

        Window w = new(args);
        w.Run();
        w.OnClosed();
    }
}
