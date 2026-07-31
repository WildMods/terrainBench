using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using SmoothGL.Graphics.Shader;
using BfresLibrary;
using static GLUtil;
using System.Runtime.InteropServices;
namespace terrainBench;

public struct TerrainRenderer {
    const int HGHT_DIM = 256;
    const int BYTES_PER_HGHT = HGHT_DIM * HGHT_DIM * 2;
    const int BYTES_PER_MATE = HGHT_DIM * HGHT_DIM * 4;

    /// <summary>
    /// Builds a large atlas of textures which can be drawn in a single draw call
    /// </summary>
    public struct CompactTileSheet {
        const int MAX_SIZE = 4096;
        const int MAX_TILES = MAX_SIZE / HGHT_DIM;
        readonly int hghtTex = 0;
        readonly int mateTex = 0;
        readonly List<Int32> indices = new();

        // Persistent buffers for tile sheets to avoid reallocation
        static int pboHght = 0;
        static int pboMate = 0;
        
        /// <summary>
        /// Build an atlas from packed index values
        /// </summary>
        public CompactTileSheet(Cache.Cache cache, ConcurrentQueue<Int32> iter) {
            var zone = Profiler.BeginZone("R_CreateCompactTileSheet");
            hghtTex = CreateTileTexture(SizedInternalFormat.R16, MAX_SIZE);
            mateTex = CreateTileTexture(SizedInternalFormat.Rgba8, MAX_SIZE);

            // Reserve space for all our indices
            CollectionsMarshal.SetCount(indices, MAX_TILES * MAX_TILES);

            // To avoid the GL driver having to synchronously copy all our texture
            // data to GL-controlled memory, we copy tiles directly into a
            // GL-controlled buffer. Then the driver can upload on its own time.

            // Setup buffers to copy pixels into
            if (pboHght == 0) {
                pboHght = CreateMappableBuffer(BYTES_PER_HGHT * MAX_TILES * MAX_TILES);
            }
            if (pboMate == 0) {
                pboMate = CreateMappableBuffer(BYTES_PER_MATE * MAX_TILES * MAX_TILES);
            }
            
            // Map the pixel buffers into CPU address space
            var zMap = Profiler.BeginZone("R_MapTileBuffer");
            nint hghtBuf = GL.MapNamedBuffer(pboHght, BufferAccess.WriteOnly);
            nint mateBuf = GL.MapNamedBuffer(pboMate, BufferAccess.WriteOnly);
            zMap.Dispose();
            if (hghtBuf == 0 || mateBuf == 0) {
                Console.WriteLine("Unable to map buffer(s)!");
                return;
            }
            
            int posInTexture = 0;
            int tilesTried = 0, tilesFound = 0;
            var idxList = indices; // Lambda can't access class members, it needs a local variable

            // Use all threads to copy tile data to the GL-controlled buffer
            var zCopy = Profiler.BeginZone("R_BuildTileBuffer");
            Parallel.For(0, MAX_TILES * MAX_TILES, (i, state) => {
                if (!iter.TryDequeue(out var val)) {
                    return;
                }
                ZOrder.UnpackIndex(val, out var idx, out var lod);
                Interlocked.Increment(ref tilesTried);

                // Retrieve tile data
                var resHGHT = cache.GetHeightmapTile(lod, idx, false);
                var resMATE = cache.GetMaterialTile(lod, idx, false);
                if (resHGHT.IsErr() || resMATE.IsErr()) {
                    return;
                }
                var hghtData = resHGHT.Unwrap();
                var mateData = resMATE.Unwrap();
                Interlocked.Increment(ref tilesFound);

                // We subtract because we need the value before the increment
                int pos = Interlocked.Increment(ref posInTexture) - 1;

                // Figure out position in the pixel buffer
                int xTarget, yTarget;
                {
                    ZOrder.Deinterleave16To8((UInt16)pos, out var x, out var y);
                    xTarget = HGHT_DIM * (UInt16)x;
                    yTarget = HGHT_DIM * (UInt16)y;
                }
                int linearIdx = xTarget + (yTarget * MAX_SIZE);

                // Copy 1 row of data at a time
                for (int j = 0; j < HGHT_DIM; j++) {
                    int srcIdx = j * HGHT_DIM; // Current row in tile

                    nint hghtDst = hghtBuf + (linearIdx * 2); // * 2 because height values are 16-bit
                    // glMapBuffer() returns a raw unmanaged pointer, which the C# bindings
                    // give as an nint. We can't use Marshal.Copy() because it only accepts
                    // Int16 and Int32 arrays, not UInt16 or Material. So, we have to cast
                    // everything down to raw pointers and do a blind unsafe copy.
                    unsafe {
                        fixed (UInt16* srcHght = hghtData) {
                            int size = BYTES_PER_HGHT / HGHT_DIM;
                            System.Buffer.MemoryCopy(&srcHght[srcIdx], &((UInt16*)hghtBuf)[linearIdx], size, size);
                        }
                        fixed (void* src = mateData) {
                            int size = BYTES_PER_MATE / HGHT_DIM;
                            var srcMate = (UInt32*)src;
                            System.Buffer.MemoryCopy(&srcMate[srcIdx], &((UInt32*)mateBuf)[linearIdx], size, size);
                        }
                    }
                    linearIdx += MAX_SIZE; // Skip down by 1 row
                }
                idxList[pos] = val;
            });
            zCopy.Dispose();
            
            var zUpload = Profiler.BeginZone("R_UploadTile");
            GL.UnmapNamedBuffer(pboHght);
            GL.UnmapNamedBuffer(pboMate);
            
            // Tell the driver to upload our buffers as textures on its own time
            // This returns more or less immediately (compared to a normal texture upload)
            GL.BindTexture(TextureTarget.Texture2D, hghtTex);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboHght);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, MAX_SIZE, MAX_SIZE, PixelFormat.Red, PixelType.UnsignedShort, 0);
            
            GL.BindTexture(TextureTarget.Texture2D, mateTex);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboMate);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, MAX_SIZE, MAX_SIZE, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);

            zUpload.Dispose();
            
            Console.WriteLine("Found {0}/{1} tiles", tilesFound, tilesTried);
            zone.Dispose();
        }

        public void Draw(int tilesPerTexLoc, int indicesLocation, int minDist, int maxDist, UInt16 centerTile) {
            GL.BindTextureUnit(0, hghtTex);
            GL.BindTextureUnit(1, mateTex);
            GL.Uniform1(tilesPerTexLoc, MAX_TILES);

            // Cull by distance by filtering the index list
            var temp = new Int32[indices.Count];
            Array.Fill(temp, -1); // Skip everything unless we explicitly copy the value over
            for (int i = 0; i < temp.Length; i++) {
                var val = indices[i];
                ZOrder.UnpackIndex(val, out var idx, out var lod);
                var lvlDiff = ZOrder.MAX_LOD - lod;
                idx <<= 2 * lvlDiff;
                
                var d = ZOrder.ManhattanDist(centerTile, idx);
                if (d >= minDist && d <= maxDist) {
                    temp[i] = val; // In range, use this index
                }
            }
            
            GL.Uniform1(indicesLocation, temp.Length, temp);
            GL.DrawArraysInstanced(PrimitiveType.Patches, 0, 4, temp.Length);
        }
    }

    /// <summary>
    /// A region of tiles backed by potentially many atlases
    /// </summary>
    public struct TileRegion {
        readonly List<CompactTileSheet> sheets = [];

        public TileRegion(byte[] bestLevels, Cache.Cache cache, byte sizeTiles, byte xCenter, byte yCenter) {
            var zone = Profiler.BeginZone("R_CreateTileRegion");
            zone.EmitValue(sizeTiles);
            var centerIdx = ZOrder.Interleave8To16(xCenter, yCenter);
            UInt16 minIdx, maxIdx;
            {
                byte xMin = (byte)Math.Max(0, xCenter - sizeTiles);
                byte yMin = (byte)Math.Max(0, yCenter - sizeTiles);
                byte xMax = (byte)Math.Min(0xFF, xCenter + sizeTiles);
                byte yMax = (byte)Math.Min(0xFF, yCenter + sizeTiles);
                minIdx = ZOrder.Interleave8To16(xMin, yMin);
                maxIdx = ZOrder.Interleave8To16(xMax, yMax);
            }

            // Build deduplicated set of packed index values
            var indices = new HashSet<Int32>();
            for (UInt16 idx = minIdx; idx < maxIdx; idx++) {
                ZOrder.Deinterleave16To8(idx, out var x, out var y);
                var linearIdx = (HGHT_DIM * y + x);
                var best = bestLevels[linearIdx];
                var lvlDiff = ZOrder.MAX_LOD - best;
                UInt16 targetIdx = (UInt16)(idx >> (2 * lvlDiff));
                indices.Add(ZOrder.PackIndex(targetIdx, best));
            }
            Console.WriteLine("Found {0} tiles in region via coverage texture", indices.Count);

            // Build tile atlases and do GPU upload
            var idxQ = new ConcurrentQueue<Int32>(indices);
            var uploadWatch = Stopwatch.StartNew();
            while (idxQ.Count > 0) {
                sheets.Add(new(cache, idxQ));
            }
            uploadWatch.Stop();
            Console.WriteLine("Uploaded all tiles in {0}ms", uploadWatch.ElapsedMilliseconds);

            zone.Dispose();
        }

        public void Draw(int tilesPerTexLoc, int indicesLocation, int minDist, int maxDist, UInt16 centerTile) {
            foreach (var lvl in sheets) {
                lvl.Draw(tilesPerTexLoc, indicesLocation, minDist, maxDist, centerTile);
            }
        }
    }
    
    Shader tessShader;
    int vaoBlank = 0; // We need a blank VAO even when vertices are hardcoded in the shader
    int terrainTexArray = 0;
    int coverageTex = 0;
    byte[] bestLevels = new byte[HGHT_DIM * HGHT_DIM];

    TileRegion ring0;

    public TerrainRenderer() { }

    private readonly string GetEmbeddedText(string name) {
        var asm = typeof(TerrainRenderer).Assembly;
        Stream? vertStream = asm.GetManifestResourceStream(name);
        if (vertStream == null) {
            return $"#error Unable to load embedded text '{name}'";
        }
        return new StreamReader(vertStream).ReadToEnd();
    }

    public bool GLInit(Cache.Cache cache) {
        var zone = Profiler.BeginZone("R_GLInit");
        string vert = GetEmbeddedText("terrainBench.Shaders.quad.vert.glsl");
        string tcs = GetEmbeddedText("terrainBench.Shaders.terrain.tcs.glsl");
        string tess = GetEmbeddedText("terrainBench.Shaders.terrain.tess.glsl");
        string frag = GetEmbeddedText("terrainBench.Shaders.terrain.frag.glsl");

        using (Profiler.BeginZone("R_ShaderCompile")) {
            tessShader = new Shader(vert, tcs, tess, frag);
        }
        vaoBlank = GL.GenVertexArray();
        GL.PatchParameter(PatchParameterInt.PatchVertices, 4);

        bestLevels = CreateCoverageTexture(cache);
        coverageTex = CreateTileTexture(SizedInternalFormat.R8, HGHT_DIM);
        GL.TextureSubImage2D(coverageTex, 0, 0, 0, HGHT_DIM, HGHT_DIM, PixelFormat.Red, PixelType.UnsignedByte, bestLevels);

        var loadWatch = Stopwatch.StartNew();
        ring0 = new(bestLevels, cache, 16, 128, 128);
        loadWatch.Stop();
        
        Console.WriteLine("Loaded all detail levels in {0}ms total.", loadWatch.ElapsedMilliseconds);
        zone.Dispose();
        return true;
    }

    // Create a square GL texture
    static private int CreateTileTexture(SizedInternalFormat inFormat, int squareSize) {
        var zone = Profiler.BeginZone("R_CreateTileTexture");
        GL.CreateTextures(TextureTarget.Texture2D, 1, out int tex);
        if (tex == 0) {
            Console.WriteLine("Failed to create texture!");
            zone.Dispose();
            return 0;
        }

        GL.TextureStorage2D(tex, 1, inFormat, squareSize, squareSize);
        GL.TextureParameter(tex, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TextureParameter(tex, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.TextureParameter(tex, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TextureParameter(tex, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        
        zone.Dispose();
        return tex;
    }

    /// <summary>
    /// Build a buffer with 1 byte per tile in the level 8 grid, indicating the
    /// highest LOD level that's available to cover that location
    /// </summary>
    public static byte[] CreateCoverageTexture(Cache.Cache cache) {
        var zone = Profiler.BeginZone("R_CreateCoverageTexture");
        byte[] buf = new byte[HGHT_DIM * HGHT_DIM];
        
        for (sbyte lvl = 8; lvl >= 0; lvl--) {
            byte lvlDiff = (byte)(ZOrder.MAX_LOD - lvl);
            UInt32 drawSize = (UInt32)((1 << lvlDiff) * (1 << lvlDiff));
            int sizeofLevel = (int)((1 << lvl) * (1 << lvl));

            // Index is an int because level 8 uses the whole 16-bit range, so
            // otherwise the loop will never end
            int tilesFound = 0;
            int tilesWritten = 0;
            for (int idx = 0; idx < sizeofLevel; idx++) {
                var res = cache.GetHeightmapTile(lvl, (UInt16)idx, false);
                if (res.IsErr()) {
                    continue;
                }
                tilesFound++;

                // Find the index of this tile's starting point on the level 8 grid
                UInt16 lvl8Idx = (UInt16)(idx << (2 * lvlDiff));
                for (int i = 0; i < drawSize; i++) {
                    int targetPos = lvl8Idx + i;
                    ZOrder.Deinterleave16To8((UInt16)targetPos, out var x, out var y);
                    int linearIdx = HGHT_DIM * y + x;
                    if (buf[linearIdx] < lvl) {
                        buf[linearIdx] = (byte)lvl;
                        tilesWritten++;
                    }
                }
            }
            Console.WriteLine("Found {0}/{1} level {2} tiles, covering {3} level 8 tiles", tilesFound, sizeofLevel, lvl, tilesWritten);
        }

        zone.Dispose();
        return buf;
    }
    
    // Load terrain texture array from the game files
    public bool LoadTerrainTextures(Game game) {
        Console.WriteLine("Loading terrain textures...");
        var total = Stopwatch.StartNew();
        var bfresLoad = Profiler.BeginZone("R_LoadTerrainBFRES");
        var bfresData = game.ReadDecompressed("Model/Terrain.Tex1.sbfres", Game.Section.Base);
        if (bfresData.IsErr()) {
            Console.WriteLine("Unable to load terrain texture file: {0}", bfresData.GetErrorMessage());
            return false;
        }
        var bfresStream = new MemoryStream(bfresData.Ok().ToArray());
        ResFile bfres = new ResFile(bfresStream);
        bfresLoad.Dispose();

        var deswizzleTime = Stopwatch.StartNew();
        deswizzleTime.Stop();

        var bfresUpload = Profiler.BeginZone("R_UploadTerrainTex");
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
        bfresUpload.Dispose();
        total.Stop();
        Console.WriteLine("Loaded terrain textures in {0}ms (spent {1}ms deswizzling)", total.ElapsedMilliseconds, deswizzleTime.ElapsedMilliseconds);

        return true;
    }

    public void Render(Matrix4 projT, Matrix4 viewT) {
        tessShader.Use();
        // Upload camera state
        tessShader.Uniform("matView")?.SetValue(viewT);
        tessShader.Uniform("matProjection")?.SetValue(projT);
        tessShader.Uniform("matModel")?.SetValue(Matrix4.Identity);

        // We have to upload texture uniforms ourselves, because the terrible
        // SmoothGL wrappers want you to use a cumbersome Sampler2D wrapper
        int indicesLocation = tessShader.GetUniformLocation("indices");
        int hghtLoc = tessShader.GetUniformLocation("heightTex");
        int matLoc = tessShader.GetUniformLocation("matTex");
        int arrayLoc = tessShader.GetUniformLocation("colorTextures");
        int coverageLoc = tessShader.GetUniformLocation("coverageTex");
        int tilesPerTexLoc = tessShader.GetUniformLocation("tilesPerTex");
        // This is specified in the shader via an extension, but for some reason
        // Nvidia hardware doesn't respect it and assigns locations seemingly at random
        GL.Uniform1(hghtLoc, 0);
        GL.Uniform1(matLoc, 1);
        GL.Uniform1(arrayLoc, 2);
        GL.Uniform1(coverageLoc, 3);

        GL.BindTextureUnit(2, terrainTexArray);
        GL.BindTextureUnit(3, coverageTex);
        GL.BindVertexArray(vaoBlank); // Required despite vertices being baked into the shader

        var center = ZOrder.Interleave8To16(128, 128);
        ring0.Draw(tilesPerTexLoc, indicesLocation, 0, 16, center);

        GL.BindVertexArray(0);
    }
}
