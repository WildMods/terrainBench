using ImGuiNET;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ErrorCode = OpenTK.Windowing.GraphicsLibraryFramework.ErrorCode;
using SNVector2 = System.Numerics.Vector2;

using terrainBench.UI.ViewModels;
using static terrainBench.UI.ViewModels.EditorState.BootState;
namespace terrainBench.UI;

public class Window : GameWindow {
    OpenGlFbo fbo = new();
    bool needResize = true;
    Vector2i newSize = new();

    EditorState editor;

    public Window(string[] args) : base(GameWindowSettings.Default, new NativeWindowSettings()
    {
        ClientSize = new Vector2i(1600, 900),
        APIVersion = new Version(4, 2)
    })
    {
        editor = new(args);
    }

    void UpdateLoadingStateMachine(EditorState ed) {
        switch (ed.BootProgress) {
            case SHOW_UPLOAD_MSG:
                Title = EditorState.gpuUploadWindowTitle;
                ed.BootProgress = LOAD_TERRAIN_TEXTURES;
                return; // End frame to make sure title is applied
            case LOAD_TERRAIN_TEXTURES:
                // Start the GPU uploads 1 frame after title is changed
                using (Profiler.BeginZone("R_LoadTerrainTextures")) {
                    ed.terrain.LoadTerrainTextures(ed.game);
                }
                ed.BootProgress = UPLOADING;
                break;
            case UPLOADING:
                using (Profiler.BeginZone("R_GLInit")) {
                    ed.terrain.LoadTerrainTiles(ed.cache);
                }

                ed.BootProgress = DONE;
                Task.Run(() => ed.RendererUploadThread());

                Title = EditorState.defaultWindowTitle;
                break;
        }
    }

    protected override void OnLoad() {
        base.OnLoad();

        Title = $"Terrain Workbench [{GL.GetString(StringName.Version)}]";

        GL.DebugMessageCallback(DebugProcCallback, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);

        ImGui.CreateContext();
        ImGuiIOPtr io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
        io.FontGlobalScale = 2f;

        ImGui.StyleColorsDark();

        ImGuiStylePtr style = ImGui.GetStyle();
        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            style.WindowRounding = 0.0f;
            style.Colors[(int)ImGuiCol.WindowBg].W = 1.0f;
        }

        ImguiImplOpenTK4.Init(this);
        ImguiImplOpenGL3.Init();

        fbo.GLInit();
        GLFWProvider.SetErrorCallback(GLFWErrorCallback);
        
        // Start terrain loading
        editor.BootProgress = TILES_LOADING;
        editor.asyncLoadedTiles.Max = 18100;
        editor.terrain.GLInit();
        editor.brushRenderer.GLInit();
        var ed = editor;
        Task.Run(delegate {
            ed.cache.Load(ed.game, ed.asyncLoadedTiles);
            ed.BootProgress = SHOW_UPLOAD_MSG;
        });
    }

    void Update(double delta) {
        UpdateLoadingStateMachine(editor);
        ImGui.DockSpaceOverViewport();
        
        bool closed = false;
        if (ImGui.Begin("Terrain Workbench", ref closed, ImGuiWindowFlags.MenuBar)) {
            if (ImGui.BeginMenuBar()) {
                if (ImGui.BeginMenu("File")) {
                    ImGui.MenuItem("Quit", "Ctrl-Q");
                    ImGui.EndMenu();
                }

                ImGui.EndMenuBar();
            }

            var s = new SNVector2(fbo.Size.X, fbo.Size.Y);
            ImGui.Image(fbo.colorTexture, s, new SNVector2(0, 1), new SNVector2(1, 0));
            ImGui.End();
        }
        
        ImGui.ShowDemoWindow();
        
        editor.UiLoadedTiles = editor.AsyncLoadedTiles.Value;
        editor.UiLoadIndeterminate = editor.AsyncLoadedTiles.IsIndeterminate;
        editor.UiTotalTiles = editor.AsyncLoadedTiles.Max;

        // Just do the progress text ourselves instead of letting Avalonia do it.
        // This is the only practical way to get progress in GB.
        // Pixels can be 2 or 4 bytes, so we use an average of 3 bytes
        var bytesPerTile = ZOrder.GRID_SIZE * ZOrder.GRID_SIZE * 3;
        var bytesPerGB = (long)Math.Pow(1000, 3);
        var loadedGB = ((long)editor.uiLoadedTiles * bytesPerTile) / (float)bytesPerGB;
        var totalGB = ((long)editor.uiTotalTiles * bytesPerTile) / (float)bytesPerGB;
        var percent = (float)editor.uiLoadedTiles / editor.uiTotalTiles * 100f;
        editor.UiProgressText = String.Format("Loaded {0:F1}/{2:F1}GB ({1:F0}%)", loadedGB, percent, totalGB);
        
        if (KeyboardState.IsKeyDown(Keys.Escape)) {
            // Close();
        }

        if (editor.BootProgress != DONE) {
            // Everything beyond this point relies on terrain data being loaded
            return;
        }

        editor.cam.aspect = (float)fbo.Size.X / (float)fbo.Size.Y;
        editor.cam.update(KeyboardState, MouseState, delta);
        editor.brush.UpdateFromInput(KeyboardState, MouseState, 1f);

        var fbSize = new Vector2(fbo.Size.X, fbo.Size.Y);
        // var mouseVec = new Vector2((float)MouseState.MousePosition.X, (float)MouseState.MousePosition.Y);
        var mouseVec = new Vector2();
        mouseVec.Y = fbSize.Y - mouseVec.Y; // Invert Y axis
        
        var ray = Raycast.ScreenToRay(mouseVec, editor.cam.proj_matrix(), editor.cam.view_matrix(), fbSize, new());
        
        int MAX_EDIT_RANGE = editor.terrain.renderRadius;
        TerrainCoords.WorldPos eyeWorld = new(editor.cam.eye());
        TerrainCoords.TileGrid8Pos eyeTile = eyeWorld;
        var dir = ray.Dir;
        var r = Raycast.RaycastTerrain(editor.cache, eyeTile, dir, MAX_EDIT_RANGE);
        if (r.IsOk()) {
            var pp = r.Unwrap();
            TerrainCoords.WorldPos wp = pp;
            
            editor.brush.center = pp;
            editor.brushRenderer.radius = editor.brush.effectiveRadius * TerrainCoords.PixelToWorldScale;
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

        if (KeyboardState.IsKeyDown(Keys.S) && KeyboardState.IsKeyDown(Keys.LeftControl)) {
            editor.cache.WriteAllTiles(editor.game.modPath, true, CsOead.Endianness.Big);
        }
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        ImguiImplOpenGL3.NewFrame();
        ImguiImplOpenTK4.NewFrame();
        ImGui.NewFrame();

        if (needResize) {
            fbo.Resize(newSize);
        }

        fbo.Bind();
        GL.Enable(EnableCap.DepthTest);
        GL.ClearColor(new Color4(0.2f, 0.3f, 0.3f, 1.0f));
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        
        if (editor.BootProgress == DONE) {
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
            
            editor.brushRenderer.Draw(projT, viewT, textures[0], textures[1]);
            GL.DeleteTextures(2, textures);
        }
        fbo.Unbind();
        
        Update(e.Time);
        
        ImGui.Render();
        GL.Viewport(0, 0, FramebufferSize.X, FramebufferSize.Y);
        GL.ClearColor(new Color4(0.2f, 0.3f, 0.3f, 1.0f));
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        ImguiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

        if (ImGui.GetIO().ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
        {
            ImGui.UpdatePlatformWindows();
            ImGui.RenderPlatformWindowsDefault();
            Context.MakeCurrent();
        }

        SwapBuffers();
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
                    //Logger?.LogDebug($"[{source}] {message}");
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
