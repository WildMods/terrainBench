using CommunityToolkit.HighPerformance;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;
using static terrainBench.TerrainCoords;

namespace terrainBench;
using System.Diagnostics;

// Callbacks run by OpenTK throughout the lifetime of the program
public class Window : GameWindow {
    Game game;
    Camera cam = new Camera();
    Cache.Cache cache = new();
    TerrainRenderer terrain = new TerrainRenderer();

    // A simple constructor to let us set properties like window size, title, FPS, etc. on the window.
    public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings, Game game)
        : base(gameWindowSettings, nativeWindowSettings) {
        this.game = game;

        Profiler.AppInfo("BOTW terrain editor");
    }

    // Called from Run() once OpenGL is available
    protected override void OnLoad() {
        base.OnLoad();
        GL.Enable(EnableCap.DepthTest);
        VSync = VSyncMode.On;
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);

        var total = Stopwatch.StartNew();
        var t = Task.Run(delegate {
            cache.Load(game);
        });
        
        using (Profiler.BeginZone("R_LoadTerrainTextures")) {
            terrain.LoadTerrainTextures(game);
        }
        using (Profiler.BeginZone("R_GLInit"))
        {
            terrain.GLInit(cache);
        }

        total.Stop();
        Console.WriteLine("Initialized in {0}ms", total.ElapsedMilliseconds);
    }

    protected override void OnUnload() {
        base.OnUnload();
    }

    protected override void OnResize(ResizeEventArgs e) {
        base.OnResize(e);
        GL.Viewport(0, 0, Size.X, Size.Y);
    }

    protected override void OnUpdateFrame(FrameEventArgs e) {
        if (KeyboardState.IsKeyDown(Keys.Escape)) {
            Close();
        }

        int MAX_EDIT_RANGE = 32;
        if (KeyboardState.IsKeyDown(Keys.U)) {
            var editRangeWorld = ((WorldPos)new TileGrid8Pos(new Vector3(MAX_EDIT_RANGE))).x;
            
            WorldPos eyeWorld = new(cam.eye());
            TileGrid8Pos eyeTile = eyeWorld;
            
            var hght = new UInt16[ZOrder.GRID_SIZE * ZOrder.GRID_SIZE];
            var source = new Vector2i((int)eyeTile.x, (int)eyeTile.z);
            var dir = cam.facing().Xz;
            foreach (var (cell, uv) in Raycast.Iterate2DLine(source, dir, MAX_EDIT_RANGE)) {
                if (cell.X >= ZOrder.GRID_SIZE || cell.Y >= ZOrder.GRID_SIZE) {
                    break;
                }
                if (cell.X < 0 || cell.Y < 0) {
                    break;
                }
                
                var idx = ZOrder.Interleave8To16((byte)cell.X, (byte)cell.Y);
                var tileRes = cache.GetHeightmapTile(8, idx, false);
                if (tileRes.IsErr()) {
                    continue;
                }
                var tile = tileRes.Unwrap();
                var startPixel = (Vector2i)(uv * new Vector2(255));
                var prevPix = startPixel;
                foreach (var (pixel, subpixel) in Raycast.Iterate2DLine(startPixel, dir, Single.PositiveInfinity))
                {
                    if (pixel.X >= ZOrder.GRID_SIZE || pixel.Y >= ZOrder.GRID_SIZE) {
                        break;
                    }
                    if (pixel.X < 0 || pixel.Y < 0) {
                        break;
                    }

                    var dist = pixel - prevPix;
                    if (dist.EuclideanLength > 1.0f) {
                        Console.WriteLine("Jumped from {0} -> {1}", prevPix, pixel);
                    }
                    
                    int linearIdx = pixel.X + pixel.Y * ZOrder.GRID_SIZE;
                    tile[linearIdx] = UInt16.MaxValue;
                    prevPix = pixel;
                }
                
                terrain.ScheduleTileUpdate(idx, 8, tile.AsSpan().AsBytes(), LodComponent.hght);
            }
        }
        cam.update(KeyboardState, MouseState, e.Time);

        base.OnUpdateFrame(e);
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
