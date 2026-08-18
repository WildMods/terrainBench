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
        GL.Enable(EnableCap.DepthTest);
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        
        var vm = (EditorState)DataContext;
        vm.BootProgress = TILES_LOADING;
        vm.asyncLoadedTiles.Max = 18100;
        Task.Run(delegate {
            vm.cache.Load(vm.game, vm.asyncLoadedTiles);
            vm.BootProgress = SHOW_UPLOAD_MSG;
        });
    }

    protected override void Deinit() {
        Debug.Assert(DataContext is EditorState);
        var vm = (EditorState)DataContext;
        vm.terrain.GLUninit();
        vm.brushRenderer.GLUninit();
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
                vm.brushRenderer.GLInit();
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
            vm.brushRenderer.Draw(projT, viewT);
        }

        // TODO: Does it make sense to do SwapBuffers() in Avalonia?
        Profiler.EmitFrameMark();
    }

    private void DoUpdate(double delta) {
        Debug.Assert(DataContext is EditorState);
        var vm = (EditorState)DataContext;
        vm.UiLoadedTiles = vm.AsyncLoadedTiles.Value;
        vm.UiLoadIndeterminate = vm.AsyncLoadedTiles.IsIndeterminate;
        vm.UiTotalTiles = vm.AsyncLoadedTiles.Max;

        // Just do the progress text ourselves instead of letting Avalonia do it.
        // This is the only practical way to get progress in GB.
        // Pixels can be 2 or 4 bytes, so we use an average of 3 bytes
        var bytesPerTile = ZOrder.GRID_SIZE * ZOrder.GRID_SIZE * 3;
        var bytesPerGB = (long)Math.Pow(1000, 3);
        var loadedGB = ((long)vm.uiLoadedTiles * bytesPerTile) / (float)bytesPerGB;
        var totalGB = ((long)vm.uiTotalTiles * bytesPerTile) / (float)bytesPerGB;
        var percent = (float)vm.uiLoadedTiles / vm.uiTotalTiles * 100f;
        vm.UiProgressText = String.Format("Loaded {0:F1}/{2:F1}GB ({1:F0}%)", loadedGB, percent, totalGB);
        
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
                
                // Please note the actual brush uses pixel coordinates, while
                // the renderer needs the world coordinates
                vm.brush.center = pp;
                vm.brushRenderer.modelT = Matrix4.CreateTranslation(wp);
            }
        }

        if (input.IsKeyDown(Key.O)) {
            var updatedTiles = vm.brush.ApplyToTiles(vm.cache, 1f);
            foreach (var packed in updatedTiles) {
                ZOrder.UnpackIndex(packed, out var idx, out var lod);
                vm.terrain.ScheduleTileUpdate(idx, lod, LodComponent.hght, vm.cache);
            }
        }

        if (input.IsKeyDown(Key.S) && input.IsKeyDown(Key.LeftCtrl)) {
            vm.cache.WriteAllTiles(vm.game.modPath, true, CsOead.Endianness.Big);
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
        var size = e.NewSize;
        cam.aspect = (float)(size.Width / size.Height);
        if (GLinited) {
            GL.Viewport(0, 0, (int)size.Width, (int)size.Height);
        }
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
}
