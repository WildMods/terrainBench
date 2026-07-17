using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SmoothGL.Graphics.Shader;
namespace terrainBench;

public class TerrainRenderer {
    const int HGHT_DIM = 256;
    const int TRIS_PER_TILE = 8192;
    const int BYTES_PER_TILE = HGHT_DIM * HGHT_DIM * 2;

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

    private string GetEmbeddedText(string name) {
        var asm = typeof(TerrainRenderer).Assembly;
        Stream? vertStream = asm.GetManifestResourceStream(name);
        if (vertStream == null) {
            return $"#error Unable to load embedded text '{name}'";
        }
        return new StreamReader(vertStream).ReadToEnd();
    }

    public bool GLInit() {
        string vert = GetEmbeddedText("terrainBench.Shaders.quad.vert.glsl");
        string tcs = GetEmbeddedText("terrainBench.Shaders.terrain.tcs.glsl");
        string tess = GetEmbeddedText("terrainBench.Shaders.terrain.tess.glsl");
        string frag = GetEmbeddedText("terrainBench.Shaders.terrain.frag.glsl");

        tessShader = new Shader(vert, tcs, tess, frag);
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
