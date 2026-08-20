using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using OpenTK.Mathematics;
using terrainBench.UI.ViewModels;
using static Avalonia.OpenGL.GlConsts;
// ReSharper disable StringLiteralTypo

namespace terrainBench.UI;
using static EditorState.BootState;

internal class OpenGlContent {
    // Timing - to calculate delta time
    private double _lastFrameTime;

    public void Init(GlInterface gl, GlVersion version, EditorState vm) {
        vm.BootProgress = TILES_LOADING;
        vm.asyncLoadedTiles.Max = 18100;
        vm.terrain.GLInit();
        Task.Run(delegate {
            vm.cache.Load(vm.game, vm.asyncLoadedTiles);
            vm.BootProgress = SHOW_UPLOAD_MSG;
        });
    }

    public void Deinit(GlInterface GL, EditorState vm) {
        vm.terrain.GLUninit();
        vm.brushRenderer.GLUninit();
    }

    public void OnOpenGlRender(GlInterface gl, int fb, PixelSize size, EditorState vm, InputState input) {
        var now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        var delta = now - _lastFrameTime;
        _lastFrameTime = now;
        
        var z = Profiler.BeginZone("AvaloniaRender");
        DoUpdate(delta, vm, input);
        switch (vm.BootProgress) {
            case SHOW_UPLOAD_MSG:
                // SetWindowTitle(EditorState.gpuUploadWindowTitle);
                vm.BootProgress = UPLOADING;
                z.Dispose();
                return; // End frame to make sure title is applied
            case UPLOADING:
                // Start the GPU uploads 1 frame after title is changed
                using (Profiler.BeginZone("R_LoadTerrainTextures")) {
                    vm.terrain.LoadTerrainTextures(vm.game);
                }
        
                using (Profiler.BeginZone("R_GLInit")) {
                    vm.brushRenderer.GLInit();
                    vm.terrain.LoadTerrainTiles(vm.cache);
                }

                vm.BootProgress = DONE;
                // SetWindowTitle(EditorState.defaultWindowTitle);
                break;
        }

        
        gl.Viewport(0, 0, size.Width, size.Height);
        gl.ClearDepth(1);
        gl.Enable(GL_DEPTH_TEST);
        gl.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        gl.DepthFunc(GL_LESS);
        gl.DepthMask(1);
        
        using (Profiler.BeginZone("GLClear")) {
            gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        }

        if (vm.BootProgress == DONE) {
            var eyeWorld = new TerrainCoords.WorldPos(vm.cam.eye());
            TerrainCoords.TileGrid8Pos eyeTile = eyeWorld;
            vm.terrain.UpdateGPUTiles(vm.cache, (Vector2i)eyeTile.xz);

            var projT = vm.cam.proj_matrix();
            var viewT = vm.cam.view_matrix();
            vm.terrain.Render(projT, viewT, eyeWorld);
            vm.brushRenderer.Draw(projT, viewT);
        }

        z.Dispose();
    }
    
    private void DoUpdate(double delta, EditorState vm, InputState input) {
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

        if (vm.BootProgress != DONE) {
            // Everything beyond this point relies on terrain data being loaded
            return;
        }
        
        int MAX_EDIT_RANGE = vm.terrain.renderRadius;
        TerrainCoords.WorldPos eyeWorld = new(vm.cam.eye());
        TerrainCoords.TileGrid8Pos eyeTile = eyeWorld;
        var dir = vm.cam.facing();
        var r = Raycast.RaycastTerrain(vm.cache, eyeTile, dir, MAX_EDIT_RANGE);
        if (r.IsOk()) {
            var pp = r.Unwrap();
            TerrainCoords.WorldPos wp = pp;
            wp.y += 8f;
            
            // Please note the actual brush uses pixel coordinates, while
            // the renderer needs the world coordinates
            vm.brush.center = pp;
            vm.brushRenderer.modelT = Matrix4.CreateTranslation(wp);
        }

        if (input.IsKeyDown(Key.O)) {
            var updatedTiles = vm.brush.ApplyToTiles(vm.cache, vm.terrain.lodCoverage, 1f);
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
}
