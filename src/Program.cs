// Created Jul. 15 2026
// @author Torphedo

using SDL3;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using terrainBench;
using terrainBench.UI;
using terrainBench.Core;

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

        // Run the editor in graphical mode
        GraphicalMain(args, true);
    }

    /// <summary>Run the graphical application</summary>
    public static void GraphicalMain(string[] args, bool debugGL) {
        SDL.InitSubSystem(SDL.InitFlags.Video);

        // OpenGL init
        SDL.GLSetAttribute(SDL.GLAttr.ContextMajorVersion, 4);
        SDL.GLSetAttribute(SDL.GLAttr.ContextMinorVersion, 2);
        SDL.GLSetAttribute(SDL.GLAttr.ContextProfileMask, (int)SDL.GLProfile.Core);
        SDL.GLSetAttribute(SDL.GLAttr.DoubleBuffer, 1);
        SDL.GLSetSwapInterval(1); // Enable VSync

        // Allow caller to disable a debug GL context for extra performance
        if (debugGL) {
            SDL.GLSetAttribute(SDL.GLAttr.ContextFlags, (int)SDL.GLContextFlag.Debug);
        }

        // SDL init
        var flags = SDL.WindowFlags.OpenGL | SDL.WindowFlags.Resizable;
        var sdlWindow = SDL.CreateWindow(EditorState.defaultWindowTitle, 1280, 720, flags);

        // Hook up OpenTK's GL bindings to SDL's GL context
        var glContext = SDL.GLCreateContext(sdlWindow);
        OpenTK.Graphics.OpenGL4.GL.LoadBindings(new SDLBindingsContext());

        // Init the actual editor
        TerrainbenchWindow editorWindow = new(args);
        editorWindow.GLInit(sdlWindow);

        double deltaTime, now;
        double last = 0;
        while (editorWindow.running) {
            // Calc delta time
            now = SDL.GetPerformanceCounter();
            deltaTime = (double)(now - last) / SDL.GetPerformanceFrequency(); 
            
            while (SDL.PollEvent(out var e)) {
                // Pass events to the ImGui backend
                editorWindow.sdlBackend.ProcessEvent(e);
                
                var type = (SDL.EventType)e.Type;
                if (type == SDL.EventType.Quit || type == SDL.EventType.WindowCloseRequested) {
                    editorWindow.running = false;
                }
                if (type == SDL.EventType.WindowResized) {
                    var size = new Vector2i(e.Window.Data1, e.Window.Data2);
                    editorWindow.OnResize(size);
                }
            }
            
            editorWindow.OnRenderFrame(deltaTime, sdlWindow);

            // Wait for VSync, assuming it's enabled and double buffered.
            SDL.GLSwapWindow(sdlWindow);
            last = now;
        }

        editorWindow.OnClosed();
    }
}
