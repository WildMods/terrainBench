using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;
using static Tracy.PInvoke;

namespace terrainBench;

using System.Diagnostics;
using terrainBench.Cache;

// Callbacks run by OpenTK throughout the lifetime of the program
public class Window : GameWindow {
    Game game;
    Camera cam = new Camera();
    Cache.Cache cache;
    TerrainRenderer terrain = new TerrainRenderer();

    // A simple constructor to let us set properties like window size, title, FPS, etc. on the window.
    public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings, Game game)
        : base(gameWindowSettings, nativeWindowSettings) {
        this.game = game;

        Profiler.AppInfo("BOTW terrain editor");
        Console.WriteLine("Loading all terrain data...");
        var time = Stopwatch.StartNew();
        time.Stop();
        Console.WriteLine("Loaded in {0}ms", time.ElapsedMilliseconds);
    }

    protected override void OnUpdateFrame(FrameEventArgs e) {
        if (KeyboardState.IsKeyDown(Keys.Escape)) {
            Close();
        }
        cam.update(KeyboardState, MouseState, e.Time);

        base.OnUpdateFrame(e);
    }

    protected override void OnLoad() {
        base.OnLoad();
        GL.Enable(EnableCap.DepthTest);
        VSync = VSyncMode.On;
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);

        using (Profiler.BeginZone("CacheInit")) {
            cache = new Cache.Cache(game);
        }

        using (Profiler.BeginZone("R_GLInit")) {
            terrain.GLInit(cache);
        }
        using (Profiler.BeginZone("R_LoadTerrain")) {
            terrain.LoadTerrain(cache, game);
        }
    }

    protected override void OnUnload() {
        base.OnUnload();
    }

    protected override void OnResize(ResizeEventArgs e) {
        base.OnResize(e);
        GL.Viewport(0, 0, Size.X, Size.Y);
    }

    protected override void OnRenderFrame(FrameEventArgs e) {
        base.OnRenderFrame(e);
        using (Profiler.BeginZone("GLClear")) {
            GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
        }

        terrain.Render(cam.proj_matrix(), cam.view_matrix());

        using (Profiler.BeginZone("SwapBuffers")) {
            SwapBuffers();
        }
        Profiler.EmitFrameMark();
    }
}
