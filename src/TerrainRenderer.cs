using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SmoothGL.Graphics.Shader;
namespace terrainBench;

public class TerrainRenderer {
    const int HGHT_DIM = 256;
    const int TRIS_PER_TILE = 8192;
    const int BYTES_PER_TILE = HGHT_DIM * HGHT_DIM * 2;

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
    uniform int idx;

    out float height; // To be used in fragment shader
    out vec2 uv;

    const float targetTileSize = 8;
    const int MAX_LOD = 8;

    // De-interleave the low 16 bits to get an 8-bit X/Z coordinate
    ivec2 idxToGridPos(int idx) {
        ivec2 pos = ivec2(0);
        for (int i = 0; i < 16; i+= 2) {
            pos.y <<= 1;
            pos.y |= ((idx >> 15) & 1);
            idx <<= 1;

            pos.x <<= 1;
            pos.x |= ((idx >> 15) & 1);
            idx <<= 1;
        }
        return pos;
    }
    
    void main() {
        // get patch coordinate
        float u = gl_TessCoord.y / 2;
        float v = gl_TessCoord.x / 2;
        // Bottom 2 bits of the Z-order index tell us where in the 2x2 tile
        // texture to look
        u += (0.5 * float(idx & 1));
        v += (0.5 * float((idx >> 1) & 1));

        uv = vec2(u, v);
        height = texture(tex, uv).x;

        int lod = idx >> 16;
        float tileFactor = float(1 << MAX_LOD) / float(1 << lod);
        ivec2 worldPos = idxToGridPos(idx);

        vec4 p00 = gl_in[0].gl_Position * tileFactor;
        vec4 p01 = gl_in[1].gl_Position * tileFactor;
        vec4 p10 = gl_in[2].gl_Position * tileFactor;
        vec4 p11 = gl_in[3].gl_Position * tileFactor;
        
        // Interpolate position across patch
        vec4 p0 = (p01 - p00) * gl_TessCoord.x + p00;
        vec4 p1 = (p11 - p10) * gl_TessCoord.x + p10;
        vec4 p = (p1 - p0) * gl_TessCoord.y + p0;

        p.y += height * 16;
        p.xz += worldPos * (tileFactor / 2);

        gl_Position = matProjection * matView * matModel * vec4(p.xyz, 1);
    }
    ";

    static string fragShader = @"#version 410 core
    #extension GL_ARB_shading_language_420pack: require
    in float height;
    in vec2 uv;
    out vec4 finalColor;

    void main() {
        // finalColor = vec4(vec2(height) * 0.7 + vec2(uv) * 0.3, 1, 1);
        finalColor = vec4(vec3(height), 1);
    }
    ";


    public struct TileDrawRecord {
        public int tex;
        public byte lod;
        public UInt16 baseId;
        public Int32[] ids = new Int32[4];

        public TileDrawRecord(int tex, byte lod, UInt16 id) {
            this.tex = tex;
            this.lod = lod;
            baseId = id;
            ids[0] = ids[1] = ids[2] = ids[3] = -1;
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

    Shader tessShader;
    int vaoBlank = 0;
    List<TileDrawRecord> tiles = new List<TileDrawRecord>();

    public bool GLInit() {
        tessShader = new Shader(quadVertShader, tessControlShader, tessEvalShader, fragShader);
        vaoBlank = GL.GenVertexArray();
        GL.PatchParameter(PatchParameterInt.PatchVertices, 4);

        return true;
    }

    public bool LoadTerrain(Game game) {
        byte lod = 6;
        int tileCount = 0;
        int sarcCount = 0;
        var loadWatch = System.Diagnostics.Stopwatch.StartNew();
        var glWatch = System.Diagnostics.Stopwatch.StartNew();
        glWatch.Stop();

        var iter = game.GetLod(lod);
        foreach (var (firstFile, sarc) in iter) {
            string archName = firstFile + ".sstera";
            if (!firstFile.EndsWith(".hght")) {
                // Console.WriteLine("Skipping non-heightmap file '{0}'", archName);
                continue;
            }
            sarcCount++;
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

            glWatch.Start();
            GL.CreateTextures(TextureTarget.Texture2D, 1, out tex);
            if (tex == 0) {
                Console.WriteLine("Failed to create texture!");
            }

            GL.TextureStorage2D(tex, 1, SizedInternalFormat.R16, dim, dim);
            GL.TextureParameter(tex, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TextureParameter(tex, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TextureParameter(tex, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TextureParameter(tex, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            glWatch.Stop();
            var tile = new TileDrawRecord(tex, lod, baseIdx.Ok());

            foreach (var (name, dataMarshal) in sarc) {
                var idx = ZOrder.IndexFromFilename(name);
                if (idx.IsErr()) {
                    Console.WriteLine("Failed to get tile index: {0}", idx.GetErrorMessage());
                    continue;
                }
                var localIdx = ZOrder.LocalIdx(idx.Ok());
                byte x = 0, y = 0;
                ZOrder.Deinterleave16To8(localIdx, out x, out y);

                tile.addID(idx.Ok());
                tileCount++;

                Console.WriteLine("\tFound {0} [index {1}, local index {4}, local coord ({2}, {3})", name, idx.Ok(), x, y, localIdx);

                int xOffset = x * HGHT_DIM, yOffset = y * HGHT_DIM;
                ReadOnlySpan<byte> span = dataMarshal.AsSpan();
                unsafe {
                    fixed (byte* bp = span) {
                        nint ptr = (IntPtr)bp;
                        glWatch.Start();
                        GL.TextureSubImage2D(tex, 0, xOffset, yOffset, HGHT_DIM, HGHT_DIM, PixelFormat.Red, PixelType.UnsignedShort, ptr);
                        glWatch.Stop();
                    }
                }
            }

            tiles.Add(tile);
        }
        loadWatch.Stop();

        Console.WriteLine("Loaded {0} tiles from {2} files ({1} triangles) in {3}ms.", tileCount, tileCount * TRIS_PER_TILE, sarcCount, loadWatch.ElapsedMilliseconds);
        Console.WriteLine("Spent {0}ms uploading OpenGL textures", glWatch.ElapsedMilliseconds);
        return true;
    }

    public void Render(Matrix4 projT, Matrix4 viewT) {
        tessShader.Use();

        tessShader.Uniform("matView")?.SetValue(viewT);
        tessShader.Uniform("matProjection")?.SetValue(projT);
        tessShader.Uniform("matModel")?.SetValue(Matrix4.Identity);

        GL.BindVertexArray(vaoBlank);
        foreach (var tile in tiles) {
            GL.BindTextureUnit(0, tile.tex);

            foreach(var id in tile.ids) {
                if (id < 0 || id > 0xFFFF) {
                    continue; // Tile not present
                }

                Int32 idx = ((Int32)tile.lod << 16) | (Int32)id;
                tessShader.Uniform("idx")?.SetValue(idx);
                tessShader.ApplyUniforms();

                GL.DrawArrays(PrimitiveType.Patches, 0, 4);
            }
        }

        GL.BindVertexArray(0);
    }
}
