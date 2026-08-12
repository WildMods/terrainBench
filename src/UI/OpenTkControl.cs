using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.HighPerformance;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static terrainBench.TerrainCoords;
namespace terrainBench.UI;
using ViewModels;

public sealed class OpenTkControl : OpenTkControlBase {
    private readonly Color4 _clearColor = new(0.2f, 0.3f, 0.3f, 1.0f);

    //mouse => see if mouse is clicked and dragged
    private bool _isDragging;

    // Timing - to calculate delta time
    private double _lastFrameTime;
    private readonly double _frameInterval; // target 60 FPS or any other FPS, see ctor and Fps constant

    public OpenTkControl() {
        _frameInterval = 1.0 / Fps;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnDetachedFromVisualTree(e);
        // here you can do something specific when the control is no longer in the tree
        Console.WriteLine("Control was detached from tree");
    }

    //Initialize all needed resources
    protected override void Init()
    {
        GL.Enable(EnableCap.DepthTest);
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        
        WriteInfos();
        WriteControlsInfos();
        
        var vm = (MainWindowViewModel)DataContext;
        var t1 = Task.Run(delegate {
            vm.cache.Load(vm.game, vm.LoadedTileCountAsync);
            vm.cacheLoadFinished = true;
        });
        
        var t2 = Task.Run(delegate {
        });
        // t.Wait();
    }

    protected override void Deinit() {
    }

    protected override void Render() {
        Debug.Assert(DataContext is MainWindowViewModel);
        var vm = (MainWindowViewModel)DataContext;
        WriteInfos();
        var now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        var delta = now - _lastFrameTime;
        DoUpdate(delta);
        if (delta < _frameInterval) // only render if enough time has passed (to limit FPS)
        {
            return;
        }
        _lastFrameTime = now;
        if (vm.cacheLoadFinished && !vm.gpuLoadFinished) {
            // Upload to the GPU
            using (Profiler.BeginZone("R_LoadTerrainTextures")) {
                vm.terrain.LoadTerrainTextures(vm.game);
            }
            
            using (Profiler.BeginZone("R_GLInit")) {
                vm.brush.GLInit();
                vm.terrain.GLInit(vm.cache);
            }

            vm.gpuLoadFinished = true;
        }

        // set correct viewport size
        var correctRenderSize = GetCorrectRenderSize();
        GL.Viewport(0, 0, correctRenderSize.X, correctRenderSize.Y);
        
        using (Profiler.BeginZone("GLClear")) {
            GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
        }

        if (vm.gpuLoadFinished) {
            var projT = vm.cam.proj_matrix();
            var viewT = vm.cam.view_matrix();
            vm.terrain.Render(projT, viewT, new WorldPos(vm.cam.eye()));
            vm.brush.Draw(projT, viewT);
        }

        using (Profiler.BeginZone("SwapBuffers")) {
            // SwapBuffers();
        }
        Profiler.EmitFrameMark();

        DrawScene();
    }

    private void DoUpdate(double delta) {
        Debug.Assert(DataContext is MainWindowViewModel);
        var vm = (MainWindowViewModel)DataContext;
        vm.CurrentLoadedTileCount = vm.LoadedTileCountAsync.val;
        /*
        if (KeyboardState.IsKeyDown(Keys.Escape)) {
            Close();
        }

        int MAX_EDIT_RANGE = 32;
        if (KeyboardState.IsKeyDown(Keys.R)) {
            WorldPos eyeWorld = new(vm.cam.eye());
            TileGrid8Pos eyeTile = eyeWorld;
            var dir = vm.cam.facing();
            var r = Raycast.RaycastTerrain(vm.cache, eyeTile, dir, MAX_EDIT_RANGE);
            if (r.IsOk()) {
                var pp = r.Unwrap();
                WorldPos wp = pp;
                wp.y += 8f;
                vm.brush.modelT = Matrix4.CreateTranslation(wp);
            }
        }

        if (KeyboardState.IsKeyDown(Keys.U)) {
            var editRangeWorld = ((WorldPos)new TileGrid8Pos(new Vector3(MAX_EDIT_RANGE))).x;

            WorldPos eyeWorld = new(vm.cam.eye());
            TileGrid8Pos eyeTile = eyeWorld;

            var hght = new UInt16[ZOrder.GRID_SIZE * ZOrder.GRID_SIZE];
            var source = new Vector2i((int)eyeTile.x, (int)eyeTile.z);
            var dir = vm.cam.facing().Xz;
            foreach (var (cell, uv) in Raycast.Iterate2DLine(source, dir, MAX_EDIT_RANGE)) {
                if (cell.X >= ZOrder.GRID_SIZE || cell.Y >= ZOrder.GRID_SIZE) {
                    break;
                }
                if (cell.X < 0 || cell.Y < 0) {
                    break;
                }

                var idx = ZOrder.Interleave8To16((byte)cell.X, (byte)cell.Y);
                var tileRes = vm.cache.GetHeightmapTile(8, idx, false);
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

                vm.terrain.ScheduleTileUpdate(idx, 8, tile.AsSpan().AsBytes(), LodComponent.hght);
            }
        }

        if (KeyboardState.IsKeyDown(Keys.S) && KeyboardState.IsKeyDown(Keys.LeftControl)) {
            vm.cache.WriteAllTiles(".", false, Endianness.Big);
        }

        vm.cam.update(KeyboardState, MouseState, delta);
        */
    }
    
    private Vector2i GetCorrectRenderSize() {
        var renderScaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        return new Vector2i(
            Math.Max(1, (int)(Bounds.Width * renderScaling)),
            Math.Max(1, (int)(Bounds.Height * renderScaling)));
    }

    private void DrawScene() {
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e) {
        base.OnSizeChanged(e);
        //do something if needed when the control size changes
        Console.WriteLine("Control was resized");
    }

    //mouse control - rotate camera by clicking and dragging
    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        _isDragging = true;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        _isDragging = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e) {
        if (!_isDragging)
            return;
    }

    protected override void OnLostFocus(RoutedEventArgs e) {
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) {
    }

    private void SetWindowTitle(string text) {
        if (VisualRoot is Window window) {
            window.Title = text;
        }
    }

    private void WriteInfos() {
        if (DataContext is not MainWindowViewModel vm) return;
        var space = "    ";
        var sb = new StringBuilder();
        sb.AppendLine("OpenGL Version:");
        sb.Append(space).AppendLine(GL.GetString(StringName.Version));
        vm.GlInformation = sb.ToString();
    }

    private void WriteControlsInfos() {
        if (DataContext is not MainWindowViewModel vm) return;
        var space = "        ";
        var sb = new StringBuilder();
        sb.AppendLine("Controls: ");
        sb.Append(space).AppendLine("W, A, S, D");
        sb.Append(space).Append(space).AppendLine("=> move camera forward, left, backwards, right");
        sb.Append(space).AppendLine("Space, Shift");
        sb.Append(space).Append(space).AppendLine("=> move camera up, down");
        sb.Append(space).AppendLine("Hold left mouse button and move mouse");
        sb.Append(space).Append(space).AppendLine("=> rotates the camera");
        sb.Append(space).AppendLine("Mouse wheel");
        sb.Append(space).Append(space).AppendLine("=> change zoom (orbit mode only)");
        sb.Append(space).AppendLine("Middle click");
        sb.Append(space).Append(space).AppendLine("=> Change camera mode");
        vm.ControlsInformation = sb.ToString();
    }
}
