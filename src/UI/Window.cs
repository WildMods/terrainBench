using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SDL3;
using System.Runtime.InteropServices;
using SNVector2 = System.Numerics.Vector2;

using terrainBench.Core;
using static terrainBench.Core.EditorState.BootState;
namespace terrainBench.UI;

public class TerrainbenchWindow {
    public ImguiImplSDL3 sdlBackend;
    OpenGlFbo fbo = new();
    bool needResize = true;
    Vector2i newSize = new();
    bool showDemo = false;
    bool showAbout = false;
    EditorState editor;
    public bool running = true;

    public TerrainbenchWindow(string[] args) 
    {
        editor = new(args);
    }

    void UpdateLoadingStateMachine(EditorState ed, nint sdlWindow) {
        switch (ed.bootProgress) {
            case SHOW_UPLOAD_MSG:
                SDL.SetWindowTitle(sdlWindow, EditorState.gpuUploadWindowTitle);
                ed.bootProgress = LOAD_TERRAIN_TEXTURES;
                return; // End frame to make sure title is applied
            case LOAD_TERRAIN_TEXTURES:
                // Start the GPU uploads 1 frame after title is changed
                ed.bootProgress = UPLOADING;
                break;
            case UPLOADING:
                using (Profiler.BeginZone("R_GLInit")) {
                    ed.terrain.LoadTerrainTiles(ed.cache);
                }

                ed.bootProgress = DONE;
                Task.Run(ed.RendererUploadThread);
                Task.Run(ed.DownscalingThread);

                SDL.SetWindowTitle(sdlWindow, EditorState.defaultWindowTitle);
                break;
        }
    }

    public void GLInit(nint sdlWindow) {
        ImGui.CreateContext();
        sdlBackend = new(sdlWindow);
        GL.DebugMessageCallback(DebugProcCallback, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);

        ImGuiIOPtr io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
        io.FontGlobalScale = 1f;
        io.Fonts.AddFontFromFileTTF("ProFontIIx.ttf", 32f);

        ImGui.StyleColorsDark();

        ImGuiStylePtr style = ImGui.GetStyle();
        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            style.WindowRounding = 0.0f;
            style.Colors[(int)ImGuiCol.WindowBg].W = 1.0f;
        }

        ImguiImplOpenGL3.Init();
        
        var s = ImGui.GetStyle();
        s.CellPadding += new SNVector2(0, 1.0f);
        s.ItemSpacing += new SNVector2(5.0f, 2.0f);
        s.ItemInnerSpacing += new SNVector2(5.0f, 0.0f);
        s.GrabRounding = s.FrameRounding = 6.0f;
        s.WindowRounding = s.FrameRounding;

        // Swap most colors to be green-ish instead of blue-ish
        var skipList = new ImGuiCol[]{
            ImGuiCol.PlotHistogram, ImGuiCol.PlotHistogramHovered,
            ImGuiCol.PlotLinesHovered, ImGuiCol.DragDropTarget
        };
        for (int i = 0; i < s.Colors.Count; i++) {
            if (skipList.Contains((ImGuiCol)i)) {
                continue;
            }
            
            (s.Colors[i].Y, s.Colors[i].Z) = (s.Colors[i].Z, s.Colors[i].Y);
        }

        using (Profiler.BeginZone("R_LoadTerrainTextures")) {
            editor.terrain.LoadTerrainTextures(editor.game);
        }
        
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Back);
        fbo.GLInit();
        
        // Start terrain loading
        editor.bootProgress = TILES_LOADING;
        editor.asyncLoadedTiles.Max = 18100;
        editor.terrain.GLInit();
        editor.brushRenderer.GLInit();
        var ed = editor;
        Task.Run(delegate {
            ed.cache.Load(ed.game, ed.asyncLoadedTiles);
            ed.bootProgress = SHOW_UPLOAD_MSG;
        });
    }

    void AboutMenu() {
        if (!showAbout) {
            return;
        }
        if (ImGui.Begin("About")) {
            ImGui.Text(AboutSelf.AboutText());

            var glVersion = GL.GetString(StringName.Version);
            var glVendor = GL.GetString(StringName.Vendor);
            var glslVersion = GL.GetString(StringName.ShadingLanguageVersion);
            var glRenderer = GL.GetString(StringName.Renderer);
            ImGui.Text($"\nGL Version: {glVendor} {glVersion}\nGLSL Version: {glslVersion}\nRenderer: {glRenderer}\n\n");
            ImGui.End();
        }
    }

    /// <summary>
    /// ImGui component for a non-editable text display with a button
    /// to pick a folder in the OS file picker
    /// </summary>
    bool PickablePath(string label, ref string txt) {
        ImGui.Text(String.Format("{0}: {1}", label, txt));
        ImGui.SameLine();
        if (ImGui.Button($"Select##{label}")) {
            var res = NativeFileDialogSharp.Dialog.FolderPicker();
            if (res.IsOk) {
                txt = res.Path;
                return true;
            }
        }

        return false;
    }
    
    void Update(double delta, nint sdlWindow) {
        UpdateLoadingStateMachine(editor, sdlWindow);
        ImGui.DockSpaceOverViewport();
        
        // Pixels can be 2 or 4 bytes, so we use an average of 3 bytes
        var bytesPerTile = ZOrder.GRID_SIZE * ZOrder.GRID_SIZE * 3;
        var bytesPerGB = (long)Math.Pow(1000, 3);
        var loadedGB = ((long)editor.asyncLoadedTiles.Value * bytesPerTile) / (float)bytesPerGB;
        var totalGB = ((long)editor.asyncLoadedTiles.Max * bytesPerTile) / (float)bytesPerGB;
        var percent = (float)editor.asyncLoadedTiles.Value / editor.uiTotalTiles * 100f;
        editor.uiProgressText = String.Format("Loaded {0:F1}/{2:F1}GB ({1:F0}%)", loadedGB, percent, totalGB);
        
        Vector2 fbStart = new();
        Vector2 fbDisplayedSize = new();
        bool temp = false;
        bool shouldClose =  ImGui.IsKeyDown(ImGuiKey.LeftCtrl) && ImGui.IsKeyDown(ImGuiKey.Q);
        bool shouldSave  =  ImGui.IsKeyDown(ImGuiKey.LeftCtrl) && ImGui.IsKeyDown(ImGuiKey.S);
        bool shouldImportHeightmap = false;
        bool isViewportHovered = false;
        if (ImGui.Begin("Terrain Workbench", ref temp, ImGuiWindowFlags.MenuBar)) {
            if (ImGui.BeginMenuBar()) {
                if (ImGui.BeginMenu("File")) {
                    shouldImportHeightmap = ImGui.MenuItem("Import heightmap image");
                    shouldSave |= ImGui.MenuItem("Save", "Ctrl-S");
                    shouldClose |= ImGui.MenuItem("Quit", "Ctrl-Q");
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Camera")) {
                    editor.cam.ImGuiEdit();
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Brush")) {
                    editor.brush.ImGuiEdit();
                    ImGui.EndMenu();
                }
                
                if (ImGui.BeginMenu("Render")) {
                    ImGui.SliderInt("Render distance (tiles)", ref editor.terrain.renderRadius, 0, 128);
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Settings")) {
                    if (PickablePath("Base game folder", ref editor.game._basePath)) {
                        var s = Settings.Settings.Load();
                        s.gameDir = editor.game._basePath;
                        s.Save();
                    }
                    if (PickablePath("Update folder", ref editor.game._updatePath)) {
                        var s = Settings.Settings.Load();
                        s.updateDir = editor.game._updatePath;
                        s.Save();
                    }
                    if (PickablePath("DLC folder", ref editor.game._dlcPath)) {
                        var s = Settings.Settings.Load();
                        s.dlcDir = editor.game._dlcPath;
                        s.Save();
                    }
                    if (PickablePath("Mod folder", ref editor.game.modPath)) {
                        var s = Settings.Settings.Load();
                        s.modDir = editor.game.modPath;
                        s.Save();
                    }
                    ImGui.EndMenu();
                }
                
                if (ImGui.BeginMenu("Help")) {
                    if (ImGui.MenuItem("About")) {
                        showAbout = !showAbout;
                    }
                    if (ImGui.MenuItem("Show demo window")) {
                        // TODO: There's a MenuItem overload for this, just dont know how the bindings expose it.
                        showDemo = !showDemo;
                    }
                    ImGui.EndMenu();
                }

                ImGui.ProgressBar(percent / 100f, new SNVector2(), editor.uiProgressText);
                ImGui.EndMenuBar();
            }

            
            fbStart = (Vector2)ImGui.GetCursorScreenPos();
            fbDisplayedSize = (Vector2)ImGui.GetContentRegionAvail();
            ImGui.Image(fbo.colorTexture, (SNVector2)fbDisplayedSize, new SNVector2(0, 1), new SNVector2(1, 0));
            isViewportHovered = ImGui.IsItemHovered();
        }
        ImGui.End();

        if (showDemo) {
            ImGui.ShowDemoWindow();
        }
        AboutMenu();
        
        if (shouldSave) {
            editor.cache.WriteAllTiles(editor.game.modPath, true, CsOead.Endianness.Big);
        }

        if (shouldClose) {
            running = false;
        }

        if (shouldImportHeightmap) {
            var filters = "png,jpeg,webp,heif,heic,avif,jpegxl,jxl,ktx,ktx2,astc,bmp,pkm";
            var res = NativeFileDialogSharp.Dialog.FileOpen(filters);
            if (res.IsOk) {
                editor.ReloadFromHeightmap(res.Path);
            }
        }
        
        editor.cam.aspect = (float)fbo.Size.X / (float)fbo.Size.Y;

        bool shouldUseKeyboard = isViewportHovered;
        if (shouldUseKeyboard) {
            editor.cam.update(sdlBackend, delta);

            // When the pen is up (i.e. clicks can only come from
            // non-pressure-sensitve sources), treat it as full pressure
            var pressure = sdlBackend.penDown ? sdlBackend.GetPenAxis(SDL.PenAxis.Pressure) : 1f;
            editor.brush.UpdateFromInput(sdlBackend, pressure);
        }

        
        if (editor.bootProgress != DONE) {
            // Everything beyond this point relies on terrain data being loaded
            return;
        }

        Vector2 mouseVec;
        unsafe {
            SDL.GetMouseState(out var x, out var y);
            // TODO: Do we want GetWindowSize() or GetWindowSizeInPixels()?
            // They differ on high DPI displays, just need to figure out what
            // coordinate space the mouse coordinates are in.
            SDL.GetWindowSize(sdlWindow, out var screenX, out var screenY);
            
            mouseVec = new Vector2((float)x, (float)y);
            var sizeDiff = new Vector2(screenX, screenY) - fbDisplayedSize;
            mouseVec -= sizeDiff;
        }

        var ray = Raycast.ScreenToRay(mouseVec, editor.cam.proj_matrix(), editor.cam.view_matrix(), fbDisplayedSize, new(), true);
        
        TerrainCoords.WorldPos eyeWorld = new(editor.cam.eye());
        TerrainCoords.TileGrid8Pos eyeTile = eyeWorld;
        var dir = ray.Dir;
        var r = Raycast.RaycastTerrain(editor.cache, eyeTile, dir, editor.terrain.renderRadius);
        if (r.IsOk()) {
            var pp = r.Unwrap();
            TerrainCoords.WorldPos wp = pp;
            
            editor.brush.center = pp;
        }

        bool leftPress = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        bool rightPress = ImGui.IsMouseDown(ImGuiMouseButton.Right);
        if (leftPress || rightPress) {
            var updatedTiles = editor.brush.ApplyToTiles(editor.cache, editor.terrain.lodCoverage, 1f);

            // Have the full-detail tiles updated with the renderer immediately
            foreach (var packed in updatedTiles) {
                ZOrder.UnpackIndex(packed, out var idx, out var lod);
                editor.terrain.ScheduleTileUpdate(idx, lod, editor.brush.target, editor.cache);
            }

            // Submit the edited tiles for async downscaling, which automatically
            // updates the lower-res tiles with the renderer
            editor.downscaleSetLock.WaitOne();
            editor.downscaleSet.UnionWith(updatedTiles);
            editor.downscaleSetLock.ReleaseMutex();
        }
    }

    public void OnRenderFrame(double delta, nint sdlWindow) {
        Profiler.EmitFrameMark();

        ImguiImplOpenGL3.NewFrame();
        sdlBackend.NewFrame();
        ImGui.NewFrame();
        
        using (Profiler.BeginZone("Update")) {
            Update(delta, sdlWindow);
        }

        if (needResize) {
            fbo.Resize(newSize);
        }

        using (Profiler.BeginZone("Main Render")) {
            fbo.Bind();
            GL.Enable(EnableCap.DepthTest);
            GL.ClearColor(new Color4(0.2f, 0.3f, 0.3f, 1.0f));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            if (editor.bootProgress == DONE) {
                var eyeWorld = new TerrainCoords.WorldPos(editor.cam.eye());
                TerrainCoords.TileGrid8Pos eyeTile = eyeWorld;

                var projT = editor.cam.proj_matrix();
                var viewT = editor.cam.view_matrix();
                editor.terrain.Render(projT, viewT, eyeWorld);

                var textures = new int[2];
                GL.GenTextures(2, textures);

                var fmt = PixelInternalFormat.CompressedRgbS3tcDxt1Ext;
                var target = TextureTarget.Texture2D;
                var texArray = editor.terrain.terrainTexArray;
                GL.TextureView(textures[0], target, texArray, fmt, 0, 1, editor.brush.textureIndices[0], 1);
                GL.TextureView(textures[1], target, texArray, fmt, 0, 1, editor.brush.textureIndices[1], 1);

                editor.brushRenderer.radius = editor.brush.effectiveRadius() * TerrainCoords.PixelToWorldScale;
                editor.brushRenderer.Draw(editor.brush, projT, viewT, textures[0], textures[1]);
                GL.DeleteTextures(2, textures);
            }
            fbo.Unbind();
        }

        using (Profiler.BeginZone("ImGui Render")) {
            ImGui.Render();
            GL.ClearColor(new Color4(0.2f, 0.3f, 0.3f, 1.0f));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            ImguiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

            if (ImGui.GetIO().ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable)) {
                ImGui.UpdatePlatformWindows();
                ImGui.RenderPlatformWindowsDefault();
            }
        }

        sdlBackend.InputNewFrame();
    }

    public void OnResize(Vector2i size) {
        // We do this indirectly in case this is called from another thread
        // (which doesn't have a GL context)
        newSize = size;
        needResize = true;
    }

    public void OnClosed() {
        fbo.Dispose();
        editor.terrain.GLUninit();
        editor.brushRenderer.GLUninit();
        ImguiImplOpenGL3.Shutdown();
        sdlBackend.Dispose();
    }

    // OpenGL debug logger callback
    public readonly static DebugProc DebugProcCallback = Window_DebugProc;
    private static void Window_DebugProc(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr messagePtr, IntPtr userParam)
    {
        string message = Marshal.PtrToStringAnsi(messagePtr, length);

        bool showMessage = true;

        switch (source)
        {
            case DebugSource.DebugSourceApplication:
                showMessage = false;
                break;
            case DebugSource.DontCare:
            case DebugSource.DebugSourceApi:
            case DebugSource.DebugSourceWindowSystem:
            case DebugSource.DebugSourceShaderCompiler:
            case DebugSource.DebugSourceThirdParty:
            case DebugSource.DebugSourceOther:
            default:
                showMessage = true;
                break;
        }

        if (showMessage)
        {
            switch (severity)
            {
                case DebugSeverity.DontCare:
                    Console.WriteLine($"[DontCare] [{source}] {message}");
                    break;
                case DebugSeverity.DebugSeverityNotification:
                    // Console.WriteLine($"Notification: [{source}] {message}");
                    break;
                case DebugSeverity.DebugSeverityHigh:
                    Console.Error.WriteLine($"Error: [{source}] {message}");
                    break;
                case DebugSeverity.DebugSeverityMedium:
                    Console.WriteLine($"Warning: [{source}] {message}");
                    break;
                case DebugSeverity.DebugSeverityLow:
                    Console.WriteLine($"Info: [{source}] {message}");
                    break;
                default:
                    Console.WriteLine($"[default] [{source}] {message}");
                    break;
            }
        }
    }
}
