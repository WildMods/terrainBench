using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;

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

        Console.WriteLine("Loading all terrain data...");
        var time = Stopwatch.StartNew();
        cache = new Cache.Cache(game);
        time.Stop();
        Console.WriteLine("Loaded in {0}ms", time.ElapsedMilliseconds);
        GC.Collect();
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

        terrain.GLInit();
        terrain.LoadTerrain(cache, game);
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
        GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);

        terrain.Render(cam.proj_matrix(), cam.view_matrix());
        SwapBuffers();
    }
}
