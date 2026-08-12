// Adapted from:
// https://github.com/Marco2011T2/AvaloniaOpenTK/blob/main/AvaloniaOpenTK/OpenTK/OpenTkControl.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.HighPerformance;
using static terrainBench.TerrainCoords;
namespace terrainBench.UI;
using ViewModels;
using static ViewModels.EditorState.BootState;

public sealed class OpenTkControl : OpenTkControlBase {

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
        WriteGLInfo();
        WriteControlsInfo();
        GL.Enable(EnableCap.DepthTest);
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        
        var vm = (EditorState)DataContext;
        vm.BootProgress = TILES_LOADING;
        Task.Run(delegate {
            vm.cache.Load(vm.game, vm.LoadedTileCountAsync);
            vm.BootProgress = SHOW_UPLOAD_MSG;
        });
    }

    protected override void Deinit() {
        Debug.Assert(DataContext is EditorState);
        var vm = (EditorState)DataContext;
        vm.terrain.GLUninit();
    }

    protected override void Render() {
        Debug.Assert(DataContext is EditorState);
        var vm = (EditorState)DataContext;
        var now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        var delta = now - _lastFrameTime;
        DoUpdate(delta);
        
        switch (vm.BootProgress) {
        case SHOW_UPLOAD_MSG:
            SetWindowTitle(EditorState.gpuUploadWindowTitle);
            vm.BootProgress = UPLOADING;
            return; // End frame to make sure title is applied
        case UPLOADING:
            // Start the GPU uploads 1 frame after title is changed
            using (Profiler.BeginZone("R_LoadTerrainTextures")) {
                vm.terrain.LoadTerrainTextures(vm.game);
            }
        
            using (Profiler.BeginZone("R_GLInit")) {
                vm.brush.GLInit();
                vm.terrain.GLInit(vm.cache);
            }

            vm.BootProgress = DONE;
            SetWindowTitle(EditorState.defaultWindowTitle);
            break;
        }

        if (delta < _frameInterval) // only render if enough time has passed (to limit FPS)
        {
            return;
        }
        _lastFrameTime = now;
        
        // set correct viewport size
        var correctRenderSize = GetCorrectRenderSize();
        GL.Viewport(0, 0, correctRenderSize.X, correctRenderSize.Y);
        
        using (Profiler.BeginZone("GLClear")) {
            GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
        }

        if (vm.BootProgress == DONE) {
            var projT = vm.cam.proj_matrix();
            var viewT = vm.cam.view_matrix();
            vm.terrain.Render(projT, viewT, new WorldPos(vm.cam.eye()));
            vm.brush.Draw(projT, viewT);
        }

        // TODO: Does it make sense to do SwapBuffers() in Avalonia?
        Profiler.EmitFrameMark();
    }

    private void DoUpdate(double delta) {
        Debug.Assert(DataContext is EditorState);
        var vm = (EditorState)DataContext;
        vm.CurrentLoadedTileCount = vm.LoadedTileCountAsync.val;
        if (input.IsKeyDown(Key.Escape)) {
            // Close();
        }

        int MAX_EDIT_RANGE = 32;
        if (input.IsKeyDown(Key.R)) {
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

        if (input.IsKeyDown(Key.S) && input.IsKeyDown(Key.LeftCtrl)) {
            vm.cache.WriteAllTiles(".", false, CsOead.Endianness.Big);
        }

        vm.cam.update(input, delta);
    }
    
    private Vector2i GetCorrectRenderSize() {
        var renderScaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        return new Vector2i(
            Math.Max(1, (int)(Bounds.Width * renderScaling)),
            Math.Max(1, (int)(Bounds.Height * renderScaling)));
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e) {
        base.OnSizeChanged(e);
        //do something if needed when the control size changes
        Console.WriteLine("Control was resized");
    }

    private void SetWindowTitle(string text) {
        if (VisualRoot is Avalonia.Controls.Window window) {
            window.Title = text;
        }
    }

    private void WriteGLInfo() {
        if (DataContext is not EditorState vm) return;
        var space = "    ";
        var sb = new StringBuilder();
        sb.AppendLine("OpenGL Version:");
        sb.Append(space).AppendLine(GL.GetString(StringName.Version));
        vm.GlInformation = sb.ToString();
    }

    private void WriteControlsInfo() {
        if (DataContext is not EditorState vm) return;
        var space = "        ";
        var sb = new StringBuilder();
        sb.AppendLine("Controls: ");
        sb.Append(space).AppendLine("W, A, S, D");
        sb.Append(space).Append(space).AppendLine("=> move camera forward, left, backwards, right");
        sb.Append(space).AppendLine("Space, Shift");
        sb.Append(space).Append(space).AppendLine("=> move camera up, down");
        sb.Append(space).AppendLine("Hold right mouse button and move mouse");
        sb.Append(space).Append(space).AppendLine("=> rotates the camera");
        sb.Append(space).AppendLine("Mouse wheel");
        sb.Append(space).Append(space).AppendLine("=> change zoom (orbit mode only)");
        sb.Append(space).AppendLine("Middle click");
        sb.Append(space).Append(space).AppendLine("=> Change camera mode");
        vm.ControlsInformation = sb.ToString();
    }
}
