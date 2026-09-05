using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System.Runtime.InteropServices;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ErrorCode = OpenTK.Windowing.GraphicsLibraryFramework.ErrorCode;
using SNVector2 = System.Numerics.Vector2;

using terrainBench.Core;
using static terrainBench.Core.EditorState.BootState;
namespace terrainBench.UI;

public class Window : GameWindow {
    OpenGlFbo fbo = new();
    bool needResize = true;
    Vector2i newSize = new();
    bool showDemo = false;
    bool showAbout = false;
    EditorState editor;

    public Window(string[] args) : base(GameWindowSettings.Default, new NativeWindowSettings()
    {
        ClientSize = new Vector2i(1600, 900),
        APIVersion = new Version(4, 2)
    })
    {
        this.VSync = VSyncMode.On;
        editor = new(args);
    }

    void UpdateLoadingStateMachine(EditorState ed) {
        switch (ed.bootProgress) {
            case SHOW_UPLOAD_MSG:
                Title = EditorState.gpuUploadWindowTitle;
                ed.bootProgress = LOAD_TERRAIN_TEXTURES;
                return; // End frame to make sure title is applied
            case LOAD_TERRAIN_TEXTURES:
                // Start the GPU uploads 1 frame after title is changed
                using (Profiler.BeginZone("R_LoadTerrainTextures")) {
                    ed.terrain.LoadTerrainTextures(ed.game);
                }
                ed.bootProgress = UPLOADING;
                break;
            case UPLOADING:
                using (Profiler.BeginZone("R_GLInit")) {
                    ed.terrain.LoadTerrainTiles(ed.cache);
                }

                ed.bootProgress = DONE;
                Task.Run(ed.RendererUploadThread);

                Title = EditorState.defaultWindowTitle;
                break;
        }
    }

    protected override void OnLoad() {
        base.OnLoad();

        Title = EditorState.defaultWindowTitle;

        GL.DebugMessageCallback(DebugProcCallback, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);

        ImGui.CreateContext();
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

        ImguiImplOpenTK4.Init(this);
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

        fbo.GLInit();
        GLFWProvider.SetErrorCallback(GLFWErrorCallback);
        
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
    
    void Update(double delta) {
        UpdateLoadingStateMachine(editor);
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
        bool shouldClose = KeyboardState.IsKeyDown(Keys.LeftControl) && KeyboardState.IsKeyDown(Keys.Q);
        bool shouldSave = KeyboardState.IsKeyDown(Keys.LeftControl) && KeyboardState.IsKeyDown(Keys.S);
        bool shouldImportHeightmap = false;
        bool shouldPickModFolder = false;
        bool isViewportHovered = false;
        if (ImGui.Begin("Terrain Workbench", ref temp, ImGuiWindowFlags.MenuBar)) {
            if (ImGui.BeginMenuBar()) {
                if (ImGui.BeginMenu("File")) {
                    shouldPickModFolder = ImGui.MenuItem("Set mod folder");
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
            ImGui.End();
        }

        if (showDemo) {
            ImGui.ShowDemoWindow();
        }
        AboutMenu();
        
        if (shouldSave) {
            editor.cache.WriteAllTiles(editor.game.modPath, true, CsOead.Endianness.Big);
        }

        if (shouldClose) {
            Close();
        }

        if (shouldPickModFolder) {
            var res = NativeFileDialogSharp.Dialog.FolderPicker();
            if (res.IsOk) {
                var s = Settings.Settings.Load();
                s.modDir = res.Path;
                editor.game.modPath = res.Path;
                s.Save();
                Console.WriteLine("Set mod folder to {0}", res.Path);
            }
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
            editor.cam.update(KeyboardState, MouseState, delta);
            editor.brush.UpdateFromInput(KeyboardState, MouseState, 1f);
        }

        
        if (editor.bootProgress != DONE) {
            // Everything beyond this point relies on terrain data being loaded
            return;
        }

        Vector2 mouseVec;
        unsafe {
            GLFW.GetCursorPos(this.WindowPtr, out var x, out var y);
            GLFW.GetFramebufferSize(WindowPtr, out var screenX, out var screenY);
            
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
            editor.brushRenderer.modelT = Matrix4.CreateTranslation(wp);
        }

        bool leftPress = MouseState.IsButtonDown(MouseButton.Left);
        bool rightPress = MouseState.IsButtonDown(MouseButton.Right);
        if (leftPress || rightPress) {
            var updatedTiles = editor.brush.ApplyToTiles(editor.cache, editor.terrain.lodCoverage, 1f);
            foreach (var packed in updatedTiles) {
                ZOrder.UnpackIndex(packed, out var idx, out var lod);
                editor.terrain.ScheduleTileUpdate(idx, lod, LodComponent.hght, editor.cache);
                editor.terrain.ScheduleTileUpdate(idx, lod, LodComponent.mate, editor.cache);
            }
        }
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        Profiler.EmitFrameMark();

        ImguiImplOpenGL3.NewFrame();
        ImguiImplOpenTK4.NewFrame();
        ImGui.NewFrame();
        
        using (Profiler.BeginZone("Update")) {
            Update(e.Time);
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
                editor.brushRenderer.Draw(projT, viewT, textures[0], textures[1]);
                GL.DeleteTextures(2, textures);
            }
            fbo.Unbind();
        }

        using (Profiler.BeginZone("ImGui Render")) {
            ImGui.Render();
            GL.Viewport(0, 0, FramebufferSize.X, FramebufferSize.Y);
            GL.ClearColor(new Color4(0.2f, 0.3f, 0.3f, 1.0f));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            ImguiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());
    
            if (ImGui.GetIO().ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable)) {
                ImGui.UpdatePlatformWindows();
                ImGui.RenderPlatformWindowsDefault();
                Context.MakeCurrent();
            }
        }

        using (Profiler.BeginZone("SwapBuffers")) {
            SwapBuffers();
        }
    }

    protected override void OnResize(ResizeEventArgs e) {
        base.OnResize(e);
        newSize = e.Size;
        needResize = true;
    }

    public void OnClosed()
    {
        fbo.Dispose();
        ImguiImplOpenGL3.Shutdown();
        ImguiImplOpenTK4.Shutdown();
    }

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
    
    private static void GLFWErrorCallback(ErrorCode errorCode, string description) {
        if (errorCode == ErrorCode.CursorUnavailable) {
            return;
        }
        Console.WriteLine("GLFW error code {0}: '{1}'", errorCode, description);
    }
}
