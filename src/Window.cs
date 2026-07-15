using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;
using SmoothGL.Graphics.Shader;

// Callbacks run by OpenTK throughout the lifetime of the program
public class Window : GameWindow {
    // These shaders should be moved to their own source files later if possible
    // Basic position-only vertex shader
    static string quadVertShader = @"#version 410 core
    // 1x1 quad vertices
    const vec3 verts[6] = vec3[](
        vec3(0, 0, 0), vec3(0.5, 0, 0), vec3(0, 0, 0.5),
        vec3(0.5, 0, 0.5), vec3(0.5, 0, 0), vec3(0, 0, 0.5)
    );

    void main() {
        vec3 pos = verts[gl_VertexID];
        gl_Position = vec4(pos, 1);
    }
    ";

    static string fragShader = @"#version 410 core
    out vec4 finalColor;

    void main() {
        finalColor = vec4(1, 0, 0, 1);
    }
    ";

    // Tessellation taken mostly from https://learnopengl.com/Guest-Articles/2021/Tessellation/Tessellation
    static string tessControlShader = @"#version 410 core
    layout (vertices=4) out;
    // Tessellate to 256 vertices square
    const int tessLevel = 255;

    void main() {
        vec4 pos = gl_in[gl_InvocationID].gl_Position;
        gl_out[gl_InvocationID].gl_Position = pos;

        // Invocation 0 controls tessellation levels for the entire patch
        if (gl_InvocationID == 0) {
            gl_TessLevelOuter[0] = tessLevel;
            gl_TessLevelOuter[1] = tessLevel;
            gl_TessLevelOuter[2] = tessLevel;
            gl_TessLevelOuter[3] = tessLevel;

            gl_TessLevelInner[0] = tessLevel;
            gl_TessLevelInner[1] = tessLevel;
        }
    }
    ";

    static string tessEvalShader = @"#version 410 core
    layout (quads, equal_spacing, ccw) in;
    uniform mat4 matModel;
    uniform mat4 matView;
    uniform mat4 matProjection;

    out float heightOut; // To be used in fragment shader
    
    void main() {
        // get patch coordinate
        float u = gl_TessCoord.x;
        float v = gl_TessCoord.y;

        vec4 p00 = gl_in[0].gl_Position;
        vec4 p01 = gl_in[1].gl_Position;
        vec4 p10 = gl_in[2].gl_Position;
        vec4 p11 = gl_in[3].gl_Position;
        
        // Interpolate position across patch
        vec4 p0 = (p01 - p00) * u + p00;
        vec4 p1 = (p11 - p10) * u + p10;
        vec4 p = (p1 - p0) * v + p0;

        heightOut = p.y;

        gl_Position = matProjection * matView * matModel * vec4(p);
    }
    ";

    ShaderProgram shader;
    int vaoBlank = 0;

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
        shader.Use();
        GL.BindVertexArray(vaoBlank);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        GL.BindVertexArray(0);

        SwapBuffers();
    }
}
