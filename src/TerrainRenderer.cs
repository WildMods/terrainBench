using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Native.IO.Handles;
using SmoothGL.Graphics.Shader;
using OperationResult;
using BfresLibrary;
using static OperationResult.Helpers;
namespace terrainBench;

public struct TerrainRenderer {
    const int HGHT_DIM = 256;
    const int TRIS_PER_TILE = 8192;
    const int BYTES_PER_TILE = HGHT_DIM * HGHT_DIM * 2;

    public struct TileDrawRecord {
        public int texHGHT;
        public int texMATE = 0;
        public byte lod;
        public byte numIDs;
        public UInt16 baseId;
        public bool[] ids = new bool[4];

        public TileDrawRecord(int texHGHT, byte lod, UInt16 id) {
            this.texHGHT = texHGHT;
            this.lod = lod;
            baseId = id;
        }

        public void UpdateNumIDs() {
            byte count = 0;
            for (int i = 0; i < ids.Length; i++) {
                if (ids[i]) {
                    count++;
                }
            }
            numIDs = count;
        }

        public void Draw(int indicesLocation) {
            GL.BindTextureUnit(0, texHGHT);
            GL.BindTextureUnit(1, texMATE);

            Int32[] indices = new Int32[4];
            for (int i = 0, pos = 0; i < 4; i++) {
                if (ids[i]) {
                    indices[pos++] = ((Int32)lod << 16) | (Int32)(baseId + i);
                }
            }

            GL.Uniform1(indicesLocation, 4, indices);
            GL.DrawArraysInstanced(PrimitiveType.Patches, 0, 4, numIDs);
        }
    }

    Shader tessShader;
    int vaoBlank = 0;
    Dictionary<UInt16, TileDrawRecord> tiles = new Dictionary<UInt16, TileDrawRecord>();
    int terrainTexArray = 0;

    public TerrainRenderer() { }

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
        tile.UpdateNumIDs();

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

        // Load terrain textures from the game files
        var bfresLoad = Stopwatch.StartNew();
        var bfresData = game.BaseGameDecompressed("Model/Terrain.Tex1.sbfres");
        if (bfresData.IsErr()) {
            Console.WriteLine("Unable to load terrain texture file: {0}", bfresData.GetErrorMessage());
            return false;
        }
        var bfresStream = new MemoryStream(bfresData.Ok().ToArray());
        ResFile bfres = new ResFile(bfresStream);
        bfresLoad.Stop();
        Console.WriteLine("Finished decompressing terrain textures in {0}ms", bfresLoad.ElapsedMilliseconds);

        var deswizzleTime = Stopwatch.StartNew();
        deswizzleTime.Stop();

        var bfresUpload = Stopwatch.StartNew();
        foreach (var pair in bfres.Textures) {
            var t = pair.Value;
            if (t.Name != "MaterialAlb") {
                continue;
            }

            Int32[] order = t.UserData["array_index"].GetValueInt32Array();
            int height = (int)t.Height, width = (int)t.Width;
            GL.CreateTextures(TextureTarget.Texture2DArray, 1, out terrainTexArray);
            if (terrainTexArray == 0) {
                Console.WriteLine("Failed to create texture array!");
                break;
            }
            Console.WriteLine("Found terrain texture array with {0} textures @ {1}x{2}", order.Length, width, height);
            var dxt1 = SizedInternalFormat.CompressedRgbS3tcDxt1Ext;
            GL.TextureStorage3D(terrainTexArray, 1, dxt1, width, height, (int)order.Length);

            for (Int32 i = 0, pos = 0; i < order.Length; i++) {
                deswizzleTime.Start();
                var data = t.GetDeswizzledData(order[i], 0);
                deswizzleTime.Stop();
                if (data == null) {
                    Console.WriteLine("Failed to decode texture {0}", i);
                    continue;
                }
                GL.CompressedTextureSubImage3D(terrainTexArray, 0, 0, 0, pos++, width, height, 1, (PixelFormat)dxt1, data.Length, data);
            }

            break;
        }
        bfresUpload.Stop();
        Console.WriteLine("Uploaded terrain textures to GPU in {0}ms ({1}ms spent deswizzling textures)", bfresUpload.ElapsedMilliseconds, deswizzleTime.ElapsedMilliseconds);

        return true;
    }

    public void Render(Matrix4 projT, Matrix4 viewT) {
        tessShader.Use();
        tessShader.Uniform("matView")?.SetValue(viewT);
        tessShader.Uniform("matProjection")?.SetValue(projT);
        tessShader.Uniform("matModel")?.SetValue(Matrix4.Identity);
        int indicesLocation = GL.GetUniformLocation(tessShader.programId, "indices");

        GL.BindTextureUnit(2, terrainTexArray);
        GL.BindVertexArray(vaoBlank);

        foreach (var tile in tiles) {
            tile.Value.Draw(indicesLocation);
        }

        GL.BindVertexArray(0);
    }
}
