using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Native.IO.Handles;
using SmoothGL.Graphics.Shader;
using OperationResult;
using static OperationResult.Helpers;
namespace terrainBench;

public class TerrainRenderer {
    const int HGHT_DIM = 256;
    const int TRIS_PER_TILE = 8192;
    const int BYTES_PER_TILE = HGHT_DIM * HGHT_DIM * 2;

    public struct TileDrawRecord {
        public int texHGHT;
        public int texMATE = 0;
        public byte lod;
        public UInt16 baseId;
        public bool[] ids = new bool[4];

        public TileDrawRecord(int texHGHT, byte lod, UInt16 id) {
            this.texHGHT = texHGHT;
            this.lod = lod;
            baseId = id;
        }

        public int numIDs() {
            int count = 0;
            for (int i = 0; i < ids.Length; i++) {
                if (ids[i]) {
                    count++;
                }
            }
            return count;
        }

        public void Draw(Shader shader) {
            GL.BindTextureUnit(0, texHGHT);
            GL.BindTextureUnit(1, texMATE);

            Int32[] indices = new Int32[4];
            for (int i = 0, pos = 0; i < 4; i++) {
                if (ids[i]) {
                    indices[pos++] = ((Int32)lod << 16) | (Int32)(baseId + i);
                }
            }

            shader.Uniform("indices")?.SetValue(indices);
            shader.ApplyUniforms();
            GL.DrawArraysInstanced(PrimitiveType.Patches, 0, 4, numIDs());
        }
    }

    Shader tessShader;
    int vaoBlank = 0;
    Dictionary<UInt16, TileDrawRecord> tiles = new Dictionary<UInt16, TileDrawRecord>();

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

    private int CreateTileTexture(SizedInternalFormat inFormat, int squareSize) {
        int tex = 0;
        GL.CreateTextures(TextureTarget.Texture2D, 1, out tex);
        if (tex == 0) {
            Console.WriteLine("Failed to create texture!");
            return 0;
        }

        GL.TextureStorage2D(tex, 1, inFormat, squareSize, squareSize);
        GL.TextureParameter(tex, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TextureParameter(tex, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.TextureParameter(tex, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TextureParameter(tex, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        return tex;
    }

    private void UpdateTileTexture(int tex, PixelFormat fmt, PixelType type, DataMarshal data, UInt16 idx) {
            byte x = 0, y = 0;
            ZOrder.Deinterleave16To8(ZOrder.LocalIdx(idx), out x, out y);

            int xOffset = x * HGHT_DIM, yOffset = y * HGHT_DIM;
            ReadOnlySpan<byte> span = data.AsSpan();
            unsafe {
                fixed (byte* bp = span) {
                    nint ptr = (IntPtr)bp;
                    GL.TextureSubImage2D(tex, 0, xOffset, yOffset, HGHT_DIM, HGHT_DIM, fmt, type, ptr);
                }
            }
    }

    private TileDrawRecord LoadSSTERAHeight(CsOead.Sarc sarc, byte lod, UInt16 baseIdx, ref int tileCount) {
        const int dim = HGHT_DIM * 2;
        int tex = CreateTileTexture(SizedInternalFormat.R16, dim);
        var tile = new TileDrawRecord(tex, lod, baseIdx);

        foreach (var (name, dataMarshal) in sarc) {
            var idx = ZOrder.IndexFromFilename(name);
            if (idx.IsErr()) {
                Console.WriteLine("Failed to get tile index: {0}", idx.GetErrorMessage());
                continue;
            }
            var localIdx = ZOrder.LocalIdx(idx.Ok());
            tile.ids[localIdx] = true;
            Console.WriteLine("\tFound {0} [index {1}]", name, idx.Ok());
            tileCount++;

            UpdateTileTexture(tex, PixelFormat.Red, PixelType.UnsignedShort, dataMarshal, idx.Ok());
        }

        return tile;
    }

    private int LoadSSTERAMaterial(CsOead.Sarc sarc, byte lod, UInt16 baseIdx) {
        const int dim = HGHT_DIM * 2;
        int tex = CreateTileTexture(SizedInternalFormat.Rgba8, dim);
        if (tex == 0) {
            return 0;
        }

        foreach (var (name, dataMarshal) in sarc) {
            var idx = ZOrder.IndexFromFilename(name);
            if (idx.IsErr()) {
                Console.WriteLine("Failed to get tile index: {0}", idx.GetErrorMessage());
                continue;
            }
            var localIdx = ZOrder.LocalIdx(idx.Ok());
            // tile.ids[localIdx] = true;
            Console.WriteLine("\tFound {0} [index {1}]", name, idx.Ok());

            UpdateTileTexture(tex, PixelFormat.Rgba, PixelType.UnsignedByte, dataMarshal, idx.Ok());
        }

        return tex;
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
            sarcCount++;
            if (sarc.Count > 4) {
                Console.WriteLine("Warning: {0} had {1} files, there should be at most 4", archName, sarc.Count);
            }

            var baseIdx = ZOrder.IndexFromFilename(firstFile);
            if (baseIdx.IsErr()) {
                Console.WriteLine("Failed to get SSTERA base index: {0}", baseIdx.GetErrorMessage());
                continue;
            }

            if (firstFile.EndsWith(".hght")) {
                tiles[baseIdx.Ok()] = LoadSSTERAHeight(sarc, lod, baseIdx.Ok(), ref tileCount);
            } else if (firstFile.EndsWith(".mate")) {
                if (tiles.ContainsKey(baseIdx.Ok())) {
                    // This is dumb, seems like C# does not have a key-value collection which returns references
                    var tile = tiles[baseIdx.Ok()];
                    tile.texMATE = LoadSSTERAMaterial(sarc, lod, baseIdx.Ok());
                    tiles[baseIdx.Ok()] = tile;
                }
            } else {
                continue;
            }

            Console.WriteLine("Loaded {0}", archName);
        }
        loadWatch.Stop();

        Console.WriteLine("Loaded {0} tiles from {2} files ({1} triangles) in {3}ms total.", tileCount, tileCount * TRIS_PER_TILE, sarcCount, loadWatch.ElapsedMilliseconds);
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
            tile.Value.Draw(tessShader);
        }

        GL.BindVertexArray(0);
    }
}
