using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using SmoothGL.Graphics.Shader;
using SmoothGL.Graphics.Texturing;
namespace terrainBench;

// Callbacks run by OpenTK throughout the lifetime of the program
public class Window : GameWindow {
    const int HGHT_DIM = 256;

    public struct TileDrawRecord {
        public int tex;
        public byte lod;
        public UInt16 baseId;
        public Int32[] ids = new Int32[4];

        public TileDrawRecord(int tex, byte lod, UInt16 id) {
            this.tex = tex;
            this.lod = lod;
            ids[0] = baseId = id;
            ids[1] = ids[2] = ids[3] = -1;
        }

        public bool addID(UInt16 id) {
            for (int i = 0; i < ids.Length; i++) {
                if (ids[i] < 0) {
                    ids[i] = id;
                    return true;
                }
            }
            return false;
        }
    }

    // These shaders should be moved to their own source files later if possible
    // Basic position-only vertex shader
    static string quadVertShader = @"#version 410 core
    // 1x1 quad vertices
    const vec2 base = vec2(0, 0.5);
    const vec3 verts[6] = vec3[](
        base.xxx, base.yxx, base.xyx,
        base.yyx, base.yxx, base.xyx
    );

    void main() {
        vec3 pos = verts[gl_VertexID];
        gl_Position = vec4(pos.yzx, 1);
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
    #extension GL_ARB_shading_language_420pack: require
    layout (quads, equal_spacing, ccw) in;
    layout (binding = 0) uniform sampler2D tex;
    uniform mat4 matModel;
    uniform mat4 matView;
    uniform mat4 matProjection;

    out float height; // To be used in fragment shader
    out vec2 uv;
    
    void main() {
        // get patch coordinate
        float u = gl_TessCoord.x;
        float v = gl_TessCoord.y;

        uv = vec2(u, v);
        height = texture(tex, uv).x;

        vec4 p00 = gl_in[0].gl_Position;
        vec4 p01 = gl_in[1].gl_Position;
        vec4 p10 = gl_in[2].gl_Position;
        vec4 p11 = gl_in[3].gl_Position;
        
        // Interpolate position across patch
        vec4 p0 = (p01 - p00) * u + p00;
        vec4 p1 = (p11 - p10) * u + p10;
        vec4 p = (p1 - p0) * v + p0;

        p.y += height;

        gl_Position = matProjection * matView * matModel * vec4(p.xyz, 1);
    }
    ";

    static string fragShader = @"#version 410 core
    #extension GL_ARB_shading_language_420pack: require
    in vec2 uv;
    in float height;
    out vec4 finalColor;
    layout (binding = 0) uniform sampler2D tex;

    void main() {
        float s = texture(tex, uv).x;
        s = height;
        finalColor = vec4(vec3(s), 1);
    }
    ";

    Game game;
    Shader tessShader;
    Texture2D dummyTexture;
    int vaoBlank = 0;
    List<TileDrawRecord> tiles = new List<TileDrawRecord>();
    Camera cam = new Camera();

    // A simple constructor to let us set properties like window size, title, FPS, etc. on the window.
    public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings, Game game)
        : base(gameWindowSettings, nativeWindowSettings) {
        this.game = game;
    }

    protected override void OnUpdateFrame(FrameEventArgs e) {
        if (KeyboardState.IsKeyDown(Keys.Escape)) {
            Close();
        }
        cam.update(KeyboardState, MouseState, e.Time);

        base.OnUpdateFrame(e);
    }

    protected override void OnLoad() {
        base.OnLoad();
        GL.Enable(EnableCap.DepthTest);
        VSync = VSyncMode.On;
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        tessShader = new Shader(quadVertShader, tessControlShader, tessEvalShader, fragShader);
        dummyTexture = new Texture2D(1, 1);
        vaoBlank = GL.GenVertexArray();
        GL.PatchParameter(PatchParameterInt.PatchVertices, 4);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);

        byte lod = 2;
        var iter = game.GetLod(lod);
        foreach (var (firstFile, sarc) in iter) {
            string archName = firstFile + ".sstera";
            Console.WriteLine("Loaded {0}", archName);
            if (sarc.Count > 4) {
                Console.WriteLine("Warning: {0} had {1} files, there should be at most 4", archName, sarc.Count);
            }

            var baseIdx = ZOrder.IndexFromFilename(firstFile);
            if (baseIdx.IsErr()) {
                Console.WriteLine("Failed to get SSTERA base index: {0}", baseIdx.GetErrorMessage());
                continue;
            }

            const int dim = HGHT_DIM * 2;
            int tex = 0;
            GL.CreateTextures(TextureTarget.Texture2D, 1, out tex);
            if (tex == 0) {
                Console.WriteLine("Failed to create texture!");
            }

            GL.TextureStorage2D(tex, 1, SizedInternalFormat.R16, dim, dim);
            GL.TextureParameter(tex, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TextureParameter(tex, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            var tile = new TileDrawRecord(tex, lod, baseIdx.Ok());

            foreach (var (name, dataMarshal) in sarc) {
                ReadOnlySpan<byte> span = dataMarshal.AsSpan();
                var idx = ZOrder.IndexFromFilename(name);
                if (idx.IsErr()) {
                    Console.WriteLine("Failed to get tile index: {0}", idx.GetErrorMessage());
                    continue;
                }
                var localIdx = ZOrder.LocalIdx(idx.Ok());
                byte x = 0, y = 0;
                ZOrder.Deinterleave16To8(localIdx, out x, out y);

                tile.addID(idx.Ok());

                Console.WriteLine("\tFound {0} [index {1}, local index {4}, local coord ({2}, {3})", name, idx.Ok(), x, y, localIdx);

                int xOffset = x * HGHT_DIM, yOffset = y * HGHT_DIM;
                unsafe {
                    fixed (byte* bp = span) {
                        nint ptr = (IntPtr)bp;
                        GL.TextureSubImage2D(tex, 0, xOffset, yOffset, HGHT_DIM, HGHT_DIM, PixelFormat.Red, PixelType.UnsignedShort, ptr);
                    }
                }
                Console.WriteLine("\tOffset ({0}, {1})", xOffset, yOffset);
            }

            tiles.Add(tile);
        }
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

        tessShader.Use();

        tessShader.Uniform("matView")?.SetValue(cam.view_matrix());
        tessShader.Uniform("matProjection")?.SetValue(cam.proj_matrix());
        var modelT = tessShader.Uniform("matModel");

        GL.BindVertexArray(vaoBlank);

        foreach (var tile in tiles) {
            GL.BindTextureUnit(0, tile.tex);

            foreach(var id in tile.ids) {
                if (id < 0 || id > 0xFFFF) {
                    continue; // Tile not present
                }

                byte x, y, xLocal, yLocal;
                ZOrder.Deinterleave16To8((UInt16)id, out x, out y);
                ZOrder.Deinterleave16To8(ZOrder.LocalIdx((UInt16)id), out xLocal, out yLocal);
                UInt32 xWorld = (UInt32)x;
                UInt32 yWorld = (UInt32)y;

                var xform = Matrix4.CreateTranslation(xWorld, 0, yWorld);

                tessShader.ApplyUniforms();
                tessShader.Uniform("matModel")?.SetValue(xform);

                GL.DrawArrays(PrimitiveType.Patches, 0, 4);
            }
        }

        GL.BindVertexArray(0);

        SwapBuffers();
    }
}
