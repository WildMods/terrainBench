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
    const int MAX_LOD = 8;
    const int BYTES_PER_TILE = HGHT_DIM * HGHT_DIM * 2;

    public struct TileDrawRecord {
        public int texHGHT;
        public int texMATE = 0;
        public byte lod;
        public byte numIDs;
        public UInt16 baseId;
        public bool[] ids = new bool[4];

        public TileDrawRecord(byte lod, UInt16 id) {
            this.lod = lod;
            baseId = id;
            ids = new bool[4];
        }
        
        public TileDrawRecord(int texHGHT, byte lod, UInt16 id) {
            this.texHGHT = texHGHT;
            this.lod = lod;
            baseId = id;
            ids = new bool[4];
        }

        public void UpdateNumIDs()
        {
            byte count = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i])
                {
                    count++;
                }
            }
            numIDs = count;
        }

        public void checkTextures() {
            if (texHGHT == 0) {
                Console.WriteLine("HGHT texture is missing for tile base {0}!", baseId);
            }
            if (texMATE == 0) {
                Console.WriteLine("MATE texture is missing for tile base {0}!", baseId);
            }
        }

        public void Draw(int indicesLocation) {
            GL.BindTextureUnit(0, texHGHT);
            GL.BindTextureUnit(1, texMATE);
            checkTextures();

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
    int terrainTexArray = 0;
    int coverageTex = 0;
    List<Dictionary<UInt16, TileDrawRecord>> levels = new List<Dictionary<UInt16, TileDrawRecord>>();
    byte[] bestLevels = new byte[HGHT_DIM * HGHT_DIM];
    byte[] drawn = new byte[HGHT_DIM * HGHT_DIM];

    public TerrainRenderer() {
        for (int i = 0; i < 9; i++) {
            levels.Add(new Dictionary<UInt16, TileDrawRecord>());
        }
    }

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
        GL.CreateTextures(TextureTarget.Texture2D, 1, out int tex);
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

    private void UpdateTileTexture<T>(int tex, PixelFormat fmt, PixelType type, ReadOnlySpan<T> data, UInt16 idx) {
        if (tex == 0) {
            Console.WriteLine("The given texture is 0!");
            return;
        }
        ZOrder.Deinterleave16To8(ZOrder.LocalIdx(idx), out byte x, out byte y);

        int xOffset = x * HGHT_DIM, yOffset = y * HGHT_DIM;
        unsafe {
            fixed (void* bp = data) {
                nint ptr = (IntPtr)bp;
                GL.TextureSubImage2D(tex, 0, xOffset, yOffset, HGHT_DIM, HGHT_DIM, fmt, type, ptr);
            }
        }
    }

    public Dictionary<UInt16, TileDrawRecord> LoadTerrainLevel(Cache.Cache cache, Game game, byte lod) {
        int sarcCount = 0;
        int tileCount = 0;
        var tiles = new Dictionary<UInt16, TileDrawRecord>();
        var loadWatch = Stopwatch.StartNew();
        int maxIdx = ((1 << lod) * (1 << lod));

        TileDrawRecord tile = new TileDrawRecord(lod, 0);
        for (UInt16 idx = 0; idx < maxIdx; idx++) {
            UInt16 localIdx = (UInt16)(idx & 0b11);
            if (localIdx == 0) {
                tile.UpdateNumIDs();
                tile.checkTextures();
                tiles[tile.baseId] = tile;
                tiles[tile.baseId].checkTextures();

                tile = new(lod, idx);
                tile.texHGHT = CreateTileTexture(SizedInternalFormat.R16, HGHT_DIM * 2);
                tile.texMATE = CreateTileTexture(SizedInternalFormat.Rgba8, HGHT_DIM * 2);
                tile.checkTextures();
            }
            var resHGHT = cache.GetHeightmapTile(lod, idx, false);
            var resMATE = cache.GetMaterialTile(lod, idx, false);
            if (resHGHT.IsErr() || resMATE.IsErr()) {
                continue;
            }
            tile.ids[localIdx] = true;
            UpdateTileTexture(tile.texHGHT, PixelFormat.Red, PixelType.UnsignedShort, resHGHT.Ok().AsSpan(), localIdx);
            UpdateTileTexture(tile.texMATE, PixelFormat.Rgba, PixelType.UnsignedByte, resMATE.Ok().AsSpan(), localIdx);
        }

        loadWatch.Stop();

        Console.WriteLine("Loaded {0} level {3} tiles from {1} files in {2}ms.", tileCount, sarcCount, loadWatch.ElapsedMilliseconds, lod);
        return tiles;
    }

    public bool LoadTerrain(Cache.Cache cache, Game game) {
        var loadWatch = Stopwatch.StartNew();
        for (byte i = 0; i < 8; i++) {
            levels[i] = LoadTerrainLevel(cache, game, i);
        }
        loadWatch.Stop();
        Console.WriteLine("Loaded all detail levels in {0}ms total.", loadWatch.ElapsedMilliseconds);

        Console.WriteLine("Building tile coverage texture...");
        for (sbyte lvl = 8; lvl >= 0; lvl--) {
            byte lvlDiff = (byte)(MAX_LOD - lvl);
            UInt32 drawSize = (UInt32)((1 << lvlDiff) * (1 << lvlDiff));
            foreach (TileDrawRecord tile in levels[lvl].Values) {
                if (tile.ids == null) {
                    Console.WriteLine("Skipping coverage for a tile in level {0} since ID array is null.", lvl);
                    continue;
                }
                for (int pos = 0; pos < tile.ids.Length; pos++) {
                    if (!tile.ids[pos]) {
                        continue;
                    }
                    UInt16 idx = (UInt16)(tile.baseId + pos);
                    // Find the index of this tile's starting point on the level 8 grid
                    UInt16 lvl8Idx = (UInt16)(idx << (2 * lvlDiff));
                    for (int i = 0; i < drawSize; i++) {
                        int targetPos = lvl8Idx + i;
                        ZOrder.Deinterleave16To8((UInt16)targetPos, out var x, out var y);
                        int linearIdx = HGHT_DIM * y + x;
                        if (bestLevels[linearIdx] < lvl) {
                            bestLevels[linearIdx] = (byte)lvl;
                        }
                    }
                }
            }
        }
        GL.CreateTextures(TextureTarget.Texture2D, 1, out coverageTex);
        GL.TextureStorage2D(coverageTex, 1, SizedInternalFormat.R8, HGHT_DIM, HGHT_DIM);
        GL.TextureSubImage2D(coverageTex, 0, 0, 0, HGHT_DIM, HGHT_DIM, PixelFormat.Red, PixelType.UnsignedByte, bestLevels);
        GL.TextureParameter(coverageTex, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TextureParameter(coverageTex, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TextureParameter(coverageTex, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TextureParameter(coverageTex, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

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
                if (data.Length == 0) {
                    Console.WriteLine("Failed to decode texture {0}", i);
                    continue;
                }
                unsafe {
                    fixed (byte* bp = data) {
                        nint ptr = (IntPtr)bp;
                        GL.CompressedTextureSubImage3D(terrainTexArray, 0, 0, 0, pos++, width, height, 1, (PixelFormat)dxt1, data.Length, ptr);
                    }
                }
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
        int indicesLocation = tessShader.GetUniformLocation("indices");
        int hghtLoc = tessShader.GetUniformLocation("heightTex");
        int matLoc = tessShader.GetUniformLocation("matTex");
        int arrayLoc = tessShader.GetUniformLocation("colorTextures");
        int coverageLoc = tessShader.GetUniformLocation("coverageTex");
        GL.Uniform1(hghtLoc, 0);
        GL.Uniform1(matLoc, 1);
        GL.Uniform1(arrayLoc, 2);
        GL.Uniform1(coverageLoc, 3);

        GL.BindTextureUnit(2, terrainTexArray);
        GL.BindTextureUnit(3, coverageTex);
        GL.BindVertexArray(vaoBlank);

        foreach (var l in levels) {
            foreach (var tile in l) {
                tile.Value.Draw(indicesLocation);
            }
        }

        GL.BindVertexArray(0);
    }
}
