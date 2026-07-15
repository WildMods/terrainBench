using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;

// Callbacks run by OpenTK throughout the lifetime of the program
public class Window : GameWindow {
    // These shaders should be moved to their own source files later if possible
    // Basic position-only vertex shader
    static string vertShader = @"#version 330 core
    in vec3 vertexPosition;
    uniform mat4 matView;
    uniform mat4 matProjection;

    void main() {
        gl_Position = matProjection * matView * vec4(vertexPosition, 1);
    }
    ";

    static string fragShader = @"#version 330 core
    out vec4 finalColor;

    void main() {
        finalColor = vec4(1, 0, 0, 1);
    }
    ";


    // A simple constructor to let us set properties like window size, title, FPS, etc. on the window.
    public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings) {
    }

    protected override void OnUpdateFrame(FrameEventArgs e) {
        if (KeyboardState.IsKeyDown(Keys.Escape)) {
            Close();
        }

        base.OnUpdateFrame(e);
    }

    protected override void OnLoad() {
        base.OnLoad();
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
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
        SwapBuffers();
    }
}
