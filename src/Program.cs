// Created Jul. 15 2026
// @author Torphedo

using SDL3;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using terrainBench;
using terrainBench.UI;

public class SDLBindingsContext : OpenTK.IBindingsContext {
    public IntPtr GetProcAddress(string procName) {
        return SDL.GLGetProcAddress(procName);
    }
}

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

        SDL.InitSubSystem(SDL.InitFlags.Video);

        SDL.GLSetSwapInterval(1); // Enable VSync
        SDL.GLSetAttribute(SDL.GLAttr.ContextMajorVersion, 4);
        SDL.GLSetAttribute(SDL.GLAttr.ContextMinorVersion, 2);
        SDL.GLSetAttribute(SDL.GLAttr.DoubleBuffer, 1);
        SDL.GLSetAttribute(SDL.GLAttr.ContextProfileMask, (int)SDL.GLProfile.Core);
        SDL.GLSetAttribute(SDL.GLAttr.ContextFlags, (int)SDL.GLContextFlag.Debug);

        var flags = SDL.WindowFlags.OpenGL | SDL.WindowFlags.Resizable;
        var win = SDL.CreateWindow("Terrain Workbench", 1280, 720, flags);

        var glContext = SDL.GLCreateContext(win);
        OpenTK.Graphics.OpenGL4.GL.LoadBindings(new SDLBindingsContext());

        GL.ClearColor(new Color4(0.2f, 0.3f, 0.3f, 1.0f));

        Window openTKWin = new(args, win);
        openTKWin.OnLoad();

        double deltaTime = 0, now = 0, last = 0;
        bool running = true;
        while (running) {
            now = SDL.GetPerformanceCounter();
            deltaTime = (double)(now - last) / SDL.GetPerformanceFrequency(); 
            
            while (SDL.PollEvent(out var e)) {
                openTKWin.sdlBackend.ProcessEvent(e);
                
                var type = (SDL.EventType)e.Type;
                if (type == SDL.EventType.Quit || type == SDL.EventType.WindowCloseRequested) {
                    running = false;
                }
                if (type == SDL.EventType.PenAxis) {
                    var axis = e.PAxis.Axis;
                    if (axis == SDL.PenAxis.Pressure) {
                        Console.WriteLine("Pressure: {0}", e.PAxis.Value);
                    }
                }
                if (type == SDL.EventType.WindowResized) {
                    var size = new Vector2i(e.Window.Data1, e.Window.Data2);
                    openTKWin.OnResize(size);
                }
            }
            
            try {
                openTKWin.OnRenderFrame(deltaTime);
            } catch (Exception e) {
                Console.WriteLine("Exception thrown by OnRenderFrame(): {0}", e.Message);
            }

            SDL.GLSwapWindow(win);
            last = now;
        }

        openTKWin.OnClosed();
    }
}
