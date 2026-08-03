using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using SmoothGL.Graphics.Shader;
using BfresLibrary;
using static GLUtil;
using CommunityToolkit.HighPerformance;
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
        // GL textures for the tile atlases
        private int hghtTex = 0;
        private int mateTex = 0;
        // The packed index + LOD values to be used by the shader
        List<Int32> indices = new();

        // CPU-mapped GL buffers for async texture uploading
        private int pboHght = 0;
        private int pboMate = 0;

        // Controls access to all PBO mapping state
        private Mutex pboMapLock = new();
        private bool mapped = false;
        
        // Mapped HGHT buffer
        private nint hghtBuf = 0;
        // Mapped MATE buffer
        private nint mateBuf = 0;

        // The set of Z-order tile indices (within the tile atlas) that need to
        // be streamed to the GPU
        private List<int> hghtUpdates = [];
        private List<int> mateUpdates = [];

        /// <summary>
        /// Map or unmap PBOs to/from CPU address space.
        /// Please lock the mapping mutex before calling this, to make sure other
        /// threads don't try to use a newly unmapped buffer or miss a newly mapped buffer.
        /// </summary>
        private void MapPBOs(bool mapState) {
            if (mapped == mapState) {
                return; // No change needed
            }
            
            if (mapState) {
                hghtBuf = GL.MapNamedBuffer(pboHght, BufferAccess.WriteOnly);
                mateBuf = GL.MapNamedBuffer(pboMate, BufferAccess.WriteOnly);
            } else {
                GL.UnmapNamedBuffer(pboHght);
                GL.UnmapNamedBuffer(pboMate);
                hghtBuf = 0;
                mateBuf = 0;
            }
            mapped = mapState;
        }
        
        /// <summary>
        /// Convert a Z-order tile index to a linear index in an array of pixels
        /// </summary>
        /// <param name="posInTexture">The Z-order index of the tile in the atlas</param>
        /// <returns>The linear index (in units of pixels, not bytes)</returns>
        private static int GetLinearIndex(int posInTexture) {
            int xTarget, yTarget;
            {
                ZOrder.Deinterleave16To8((UInt16)posInTexture, out var x, out var y);
                xTarget = HGHT_DIM * (UInt16)x;
                yTarget = HGHT_DIM * (UInt16)y;
            }
            int linearIdx = xTarget + (yTarget * MAX_SIZE);
            return linearIdx;
        }
        
        /// <summary>
        /// Update tile data in an OpenGL-controlled buffer
        /// </summary>
        /// <param name="posInTexture">The Z-order index of the tile in the atlas</param>
        /// <param name="data">The tile data to upload</param>
        /// <param name="buf">The pixel buffer object to copy data to</param>
        /// <param name="pixelSize">The size in bytes of each pixel in the tile</param>
        private static bool UpdatePBOTile(int posInTexture, ReadOnlySpan<byte> data, nint buf, int pixelSize) {
            if (buf == 0 || pixelSize < 2) {
                return false;
            }
            int linearIdx = GetLinearIndex(posInTexture) * pixelSize;
            
            // Copy 1 row of data at a time
            int srcRowSize = HGHT_DIM * pixelSize;
            int dstRowSize = MAX_SIZE * pixelSize;
            for (int j = 0; j < HGHT_DIM; j++) {
                int srcIdx = j * srcRowSize; // Current row in tile
                nint dst = buf + linearIdx + (j * dstRowSize);
                
                // glMapBuffer() returns a raw unmanaged pointer, which the C# bindings
                // give as an nint. We can't use Marshal.Copy() because it only accepts
                // Int16 and Int32 arrays, not UInt16 or Material. So, we have to cast
                // everything down to raw pointers and do a blind unsafe copy.
                unsafe {
                    fixed (byte* src = data) {
                        System.Buffer.MemoryCopy(&src[srcIdx], (void*)dst, srcRowSize, srcRowSize);
                    }
                }
            }
            
            return true;
        }

        
        /// <summary>
        /// Update a rendered tile.
        /// The render thread streams tiles to the GPU at the start of each frame.
        /// </summary>
        public bool ScheduleTileUpdate(UInt16 idx, byte lod, ReadOnlySpan<byte> data, LodComponent type) {
            Int32 packed = ZOrder.PackIndex(idx, lod);
            var pos = indices.FindIndex(0, indices.Count, val => val == packed);
            if (pos < 0) {
                return false;
            }

            // Ensure the buffer is actually mapped and available
            pboMapLock.WaitOne();
            nint buf;
            int pixelSize;
            List<int> updateList;
            if (type == LodComponent.hght) {
                buf = hghtBuf;
                pixelSize = 2;
                updateList = hghtUpdates;
            } else if (type == LodComponent.mate) {
                buf = mateBuf;
                pixelSize = 4;
                updateList = mateUpdates;
            } else {
                Console.WriteLine("Unsupported terrain tile type");
                return false; // Unsupported tile type
            }
            if (buf == 0) {
                Console.WriteLine("PBO is not mapped, PBO map state reads {0}, hghtBuf = {1}, mateBuf = {2}.", mapped, hghtBuf, mateBuf);
                return false; // Buffer isn't mapped right now
            }

            updateList.Add(pos);
            UpdatePBOTile(pos, data, buf, pixelSize);
            pboMapLock.ReleaseMutex();
            return true;
        }
        
        /// <summary>
        /// Have OpenGL stream all newly updated tiles to the GPU asynchronously
        /// </summary>
        public void ProcessTileUpdates() {
            pboMapLock.WaitOne(); // Make sure no one is using the mapped region
            bool hghtDirty = hghtUpdates.Count > 0;
            bool mateDirty = mateUpdates.Count > 0;
            if (!hghtDirty && mateDirty) {
                pboMapLock.ReleaseMutex();
                return; // Nothing to do.
            }
            MapPBOs(false); // Buffers can't be bound while mapped

            // Tile uploads are done 1 row at a time, because OpenGL expects to
            // find contiguous image data at whatever resolution we specify. The
            // current PBO format is a contiguous image for the whole atlas, but
            // we would need the entire tile to be contiguous to upload the tile
            // in 1 call.
            // 
            // TODO: Treat the PBO as tile-contiguous after initialization
            // TODO: Write a helper method to eliminate this code duplication
            if (hghtDirty) {
                Console.WriteLine("Processing {0} HGHT updates", hghtUpdates.Count);
                GL.BindTexture(TextureTarget.Texture2D, hghtTex);
                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboHght);
                foreach (var pos in hghtUpdates) {
                    int xTarget, yTarget;
                    {
                        ZOrder.Deinterleave16To8((UInt16)pos, out var x, out var y);
                        xTarget = HGHT_DIM * (UInt16)x;
                        yTarget = HGHT_DIM * (UInt16)y;
                    }
                    int rowSize = MAX_SIZE * 2;
                    int linearIdx = (xTarget + (yTarget * MAX_SIZE)) * 2;
                    
                    for (int row = 0; row < HGHT_DIM; row++) {
                        GL.TexSubImage2D(TextureTarget.Texture2D, 0, xTarget, yTarget + row, HGHT_DIM, 1, PixelFormat.Red, PixelType.UnsignedShort, linearIdx);
                        linearIdx += rowSize;
                    }
                }
                hghtUpdates.Clear();
            }
            
            if (mateDirty) {
                Console.WriteLine("Processing {0} MATE updates", mateUpdates.Count);
                GL.BindTexture(TextureTarget.Texture2D, mateTex);
                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboMate);
                foreach (var pos in mateUpdates) {
                    int xTarget, yTarget;
                    {
                        ZOrder.Deinterleave16To8((UInt16)pos, out var x, out var y);
                        xTarget = HGHT_DIM * (UInt16)x;
                        yTarget = HGHT_DIM * (UInt16)y;
                    }
                    int rowSize = MAX_SIZE * 4;
                    int linearIdx = (xTarget + (yTarget * MAX_SIZE)) * 4;
                    
                    for (int row = 0; row < HGHT_DIM; row++) {
                        GL.TexSubImage2D(TextureTarget.Texture2D, 0, xTarget, yTarget + row, HGHT_DIM, 1, PixelFormat.Rgba, PixelType.UnsignedByte, linearIdx);
                        linearIdx += rowSize;
                    }
                }
                mateUpdates.Clear();
            }

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            MapPBOs(true); // Allow updates again
            pboMapLock.ReleaseMutex();
        }
        
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
            pboHght = CreateMappableBuffer(BYTES_PER_HGHT * MAX_TILES * MAX_TILES);
            pboMate = CreateMappableBuffer(BYTES_PER_MATE * MAX_TILES * MAX_TILES);
            
            // Map the pixel buffers into CPU address space
            var zMap = Profiler.BeginZone("R_MapTileBuffer");
            MapPBOs(true);
            zMap.Dispose();
            if (this.hghtBuf == 0 || this.mateBuf == 0) {
                Console.WriteLine("Unable to map buffer(s)!");
                return;
            }
            
            int posInTexture = 0;
            int tilesTried = 0, tilesFound = 0;
            // Lambda can't access class members, it needs local variables
            var idxList = indices;
            nint hghtBuf = this.hghtBuf, mateBuf = this.mateBuf;

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

                // Copy the tile data
                UpdatePBOTile(pos, hghtData.AsSpan().AsBytes(), hghtBuf, 2);
                UpdatePBOTile(pos, mateData.AsSpan().AsBytes(), mateBuf, 4);

                idxList[pos] = val;
            });
            zCopy.Dispose();
            
            var zUpload = Profiler.BeginZone("R_UploadTile");
            MapPBOs(false);
            
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

            GL.InvalidateBufferData(pboHght);
            GL.InvalidateBufferData(pboMate);
            MapPBOs(true);
            zUpload.Dispose();
            
            Console.WriteLine("Found {0}/{1} tiles", tilesFound, tilesTried);
            zone.Dispose();
        }

        public void Draw(int tilesPerTexLoc, int indicesLocation, int minDist, int maxDist, UInt16 centerTile) {
            ProcessTileUpdates();
            
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

        public TileRegion(byte[] lodCoverage, Cache.Cache cache, byte sizeTiles, byte xCenter, byte yCenter) {
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
                var cov = lodCoverage[linearIdx];
                byte best = ZOrder.MAX_LOD;
                while ((cov & 0x80) == 0) {
                    cov <<= 1;
                    best--;
                }
                
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

        /// <summary>
        /// Update a rendered tile.
        /// The render thread streams tiles to the GPU at the start of each frame.
        /// </summary>
        public bool ScheduleTileUpdate(UInt16 idx, byte lod, ReadOnlySpan<byte> data, LodComponent type) {
            foreach (var sheet in sheets) {
                if (sheet.ScheduleTileUpdate(idx, lod, data, type)) {
                    return true;
                }
            }
            
            return false;
        }
    }
    
    Shader tessShader;
    int vaoBlank = 0; // We need a blank VAO even when vertices are hardcoded in the shader
    int terrainTexArray = 0;
    int coverageTex = 0;
    byte[] lodCoverage = new byte[HGHT_DIM * HGHT_DIM];

    TileRegion ring0;

    /// <summary>
    /// Update a rendered tile.
    /// The render thread streams tiles to the GPU at the start of each frame.
    /// </summary>
    public bool ScheduleTileUpdate(UInt16 idx, byte lod, ReadOnlySpan<byte> data, LodComponent type) {
        return ring0.ScheduleTileUpdate(idx, lod, data, type);
    }
    
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

        lodCoverage = CreateCoverageTexture(cache);
        coverageTex = CreateTileTexture(SizedInternalFormat.R8, HGHT_DIM);
        GL.TextureSubImage2D(coverageTex, 0, 0, 0, HGHT_DIM, HGHT_DIM, PixelFormat.Red, PixelType.UnsignedByte, lodCoverage);

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
        
        for (sbyte lvl = 8; lvl > 0; lvl--) {
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
                byte val = (byte)(1 << (lvl - 1));
                for (int i = 0; i < drawSize; i++) {
                    int targetPos = lvl8Idx + i;
                    ZOrder.Deinterleave16To8((UInt16)targetPos, out var x, out var y);
                    int linearIdx = HGHT_DIM * y + x;

                    // We have 8 bits and 9 detail levels, so exclude level 0.
                    Debug.Assert(lvl > 0);
                    // MSB = level 8, LSB = level 1.
                    buf[linearIdx] |= val;
                    tilesWritten++;
                }
            }
            Console.WriteLine("Found {0}/{1} level {2} tiles, covering {3} level 8 tiles", tilesFound, sizeofLevel, lvl, tilesWritten);
        }

        zone.Dispose();
        return buf;
    }
    
    // Load terrain texture array from the game files
    public bool LoadTerrainTextures(Game game) {
        // Texture indices for the terrain texture array. We use this instead of
        // loading it from the file on Switch dumps, since the BFRES library
        // doesn't parse the user data section of Switch BNTX.
        Int32[] fallbackIndices = {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13,
            14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 17, 18,
            0, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45,
            46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 7, 60, 61, 62,
            63, 64, 65, 66, 67, 68, 69, 70, 71, 0, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82 };
        
        Console.WriteLine("Loading terrain textures...");
        var total = Stopwatch.StartNew();
        var bfresLoad = Profiler.BeginZone("R_LoadTerrainBFRES");

        string texPath = "Pack/TitleBG.pack//Model//Terrain.Tex.sbfres";
        string tex1Path = "Model/Terrain.Tex1.sbfres";
        string tex2Path = "Pack/TitleBG.pack//Model//Terrain.Tex2.sbfres";
        
        var res = BfresTextureReader.Create(game, texPath, tex1Path, tex2Path);
        if (res.IsErr()) {
            Console.WriteLine("Unable to load terrain textures.\n{0}", res.Err()?.Message);
            return false;
        }
        var bfresReader = res.Unwrap();
        bfresLoad.Dispose();

        var deswizzleTime = Stopwatch.StartNew();
        deswizzleTime.Stop();

        var bfresUpload = Profiler.BeginZone("R_UploadTerrainTex");
        var texResult = bfresReader.GetTexture("MaterialAlb", 0);
        if (texResult.IsErr()) {
            Console.WriteLine("Unable to find terrain texture array!");
            return false;
        }
        var t = texResult.Unwrap();
        var order = fallbackIndices;
        if (t.UserData != null) {
            // Load indices from the BFRES if possible for accuracy
            order = t.UserData["array_index"].GetValueInt32Array();
        }

        int height = (int)t.Height, width = (int)t.Width;
        GL.CreateTextures(TextureTarget.Texture2DArray, 1, out terrainTexArray);
        if (terrainTexArray == 0) {
            Console.WriteLine("Failed to create texture array!");
        }
        var dxt1 = SizedInternalFormat.CompressedRgbS3tcDxt1Ext;
        GL.TextureStorage3D(terrainTexArray, (int)Math.Log2(width), dxt1, width, height, (int)order.Length);
        GL.TextureParameter(terrainTexArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TextureParameter(terrainTexArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.MirroredRepeat);

        for (Int32 i = 0, pos = 0; i < order.Length; i++) {
            deswizzleTime.Start();

            // The BFRES library's mipmap loading doesn't work and I couldn't
            // easily fix it, so we only load mip 0. -- torf
            int mip = 0;
            var curTex = bfresReader.GetTexture("MaterialAlb", mip);
            if (curTex.IsErr()) {
                Console.WriteLine("Unable to get texture for mip level {0}", mip);
                continue;
            }
            
            var data = curTex.Unwrap().GetDeswizzledData(order[i], mip);
            deswizzleTime.Stop();
            if (data.Length == 0) {
                Console.WriteLine("Failed to decode texture {0}", i);
                continue;
            }
            unsafe {
                fixed (byte* bp = data) {
                    nint ptr = (IntPtr)bp;
                    GL.CompressedTextureSubImage3D(terrainTexArray, mip, 0, 0, pos, width, height, 1, (PixelFormat)dxt1, data.Length, ptr);
                }
            }
            pos++;
        }
        bfresUpload.Dispose();
        Console.WriteLine("Generating mipmaps...");
        GL.GenerateTextureMipmap(terrainTexArray);

        total.Stop();
        Console.WriteLine("Loaded terrain textures in {0}ms (spent {1}ms deswizzling)", total.ElapsedMilliseconds, deswizzleTime.ElapsedMilliseconds);

        return true;
    }

    public void Render(Matrix4 projT, Matrix4 viewT) {
        tessShader.Use();
        // Upload camera state
        tessShader.Uniform("matView")?.SetValue(viewT);
        tessShader.Uniform("matProjection")?.SetValue(projT);
        tessShader.Uniform("matModel")?.SetValue(TerrainCoords.WorldPos.FromTileGridXform());

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
