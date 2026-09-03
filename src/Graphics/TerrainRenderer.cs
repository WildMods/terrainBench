using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using static GLUtil;
using CommunityToolkit.HighPerformance;
using System.Runtime.InteropServices;
namespace terrainBench;
using Graphics;

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
        public List<Int32> indices = new();
        Int32[] drawIndices = Array.Empty<Int32>();

        // CPU-mapped GL buffers for async texture uploading
        private int[] pboHght = new int[2];
        private int[] pboMate = new int[2];

        private int activePBO = 0;
        private int inactivePBO => activePBO == 0 ? 1 : 0;

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
        private void SwapPBOs() {
            pboMapLock.WaitOne();
            var z = Profiler.BeginZone("SwapPBOs");
            z.EmitText($"Active = {activePBO}");
            
            hghtBuf = GL.MapNamedBuffer(pboHght[inactivePBO], BufferAccess.WriteOnly);
            mateBuf = GL.MapNamedBuffer(pboMate[inactivePBO], BufferAccess.WriteOnly);
            GL.UnmapNamedBuffer(pboHght[activePBO]);
            GL.UnmapNamedBuffer(pboMate[activePBO]);
            activePBO = inactivePBO;
            z.Dispose();
            pboMapLock.ReleaseMutex();
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
        private static bool CopyTileToPBOByRow(int posInTexture, ReadOnlySpan<byte> data, nint buf, int pixelSize) {
            if (buf == 0 || pixelSize < 2) {
                return false;
            }

            var z = Profiler.BeginZone("CopyTileToPBO");
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
            
            z.Dispose();
            return true;
        }
        
        public bool ScheduleTileUpdate(UInt16 idx, byte lod, ReadOnlySpan<byte> data, LodComponent type) {
            var z = Profiler.BeginZone("ScheduleTileUpdate");
            var z2 = Profiler.BeginZone("ScheduleTileUpdateFindIdx");
            Int32 packed = ZOrder.PackIndex(idx, lod);
            var pos = indices.FindIndex(0, indices.Count, v => v == packed);
            z2.Dispose();
            if (pos < 0) {
                z.Dispose();
                return false;
            }

            z.Dispose();
            return ScheduleTileUpdate(pos, data, type);
        }
        
        /// <summary>
        /// Update a rendered tile.
        /// The render thread streams tiles to the GPU at the start of each frame.
        /// </summary>
        public bool ScheduleTileUpdate(int pos, ReadOnlySpan<byte> data, LodComponent type) {
            var z = Profiler.BeginZone("ScheduleTileUpdate");

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
            CopyTileToPBOByRow(pos, data, buf, pixelSize);
            z.Dispose();
            pboMapLock.ReleaseMutex();
            return true;
        }

        public IEnumerable<int> GetStaleTiles(HashSet<int> inGroup) {
            for (int i = 0; i < indices.Count; i++) {
                var packed = indices[i];
                if (!inGroup.Contains(packed)) {
                    yield return i;
                }
            }
        }
        
        /// <summary>
        /// Have OpenGL stream all newly updated tiles to the GPU asynchronously
        /// </summary>
        public void ProcessTileUpdates() {
            pboMapLock.WaitOne(); // Make sure no one is using the mapped region
            var z = Profiler.BeginZone("ProcessTileUpdates");
            bool hghtDirty = hghtUpdates.Count > 0;
            bool mateDirty = mateUpdates.Count > 0;
            if (!hghtDirty && !mateDirty) {
                z.Dispose();
                pboMapLock.ReleaseMutex();
                return; // Nothing to do.
            }
            
            // Make the PBO that was being edited inactive so we can upload from it
            SwapPBOs();

            // Tile uploads are done 1 row at a time, because OpenGL expects to
            // find contiguous image data at whatever resolution we specify. The
            // current PBO format is a contiguous image for the whole atlas, but
            // we would need the entire tile to be contiguous to upload the tile
            // in 1 call.
            // 
            // TODO: Write a helper method to eliminate this code duplication
            GL.PixelStore(PixelStoreParameter.UnpackRowLength, MAX_SIZE);
            if (hghtDirty) {
                Console.WriteLine("Processing {0} HGHT updates", hghtUpdates.Count);
                GL.BindTexture(TextureTarget.Texture2D, hghtTex);
                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboHght[inactivePBO]);
                foreach (var pos in hghtUpdates) {
                    int xTarget, yTarget;
                    {
                        ZOrder.Deinterleave16To8((UInt16)pos, out var x, out var y);
                        xTarget = HGHT_DIM * (UInt16)x;
                        yTarget = HGHT_DIM * (UInt16)y;
                    }
                    int rowSize = MAX_SIZE * 2;
                    int linearIdx = GetLinearIndex(pos) * 2;
                    
                    var z2 = Profiler.BeginZone("UploadHGHT");
                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, xTarget, yTarget, HGHT_DIM, HGHT_DIM, PixelFormat.Red, PixelType.UnsignedShort, linearIdx);
                    z2.Dispose();
                }
                hghtUpdates.Clear();
            }
            
            if (mateDirty) {
                Console.WriteLine("Processing {0} MATE updates", mateUpdates.Count);
                GL.BindTexture(TextureTarget.Texture2D, mateTex);
                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboMate[inactivePBO]);
                foreach (var pos in mateUpdates) {
                    int xTarget, yTarget;
                    {
                        ZOrder.Deinterleave16To8((UInt16)pos, out var x, out var y);
                        xTarget = HGHT_DIM * (UInt16)x;
                        yTarget = HGHT_DIM * (UInt16)y;
                    }
                    int rowSize = MAX_SIZE * 4;
                    int linearIdx = GetLinearIndex(pos) * 4;
                    
                    var z2 = Profiler.BeginZone("UploadMATE");
                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, xTarget, yTarget, HGHT_DIM, HGHT_DIM, PixelFormat.Rgba, PixelType.UnsignedByte, linearIdx);
                    z2.Dispose();
                }
                mateUpdates.Clear();
            }
            GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0);

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            z.Dispose();
            pboMapLock.ReleaseMutex();
        }

        public void RemovePresentIDs(HashSet<int> set) {
            set.ExceptWith(indices);
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
            drawIndices = new Int32[MAX_TILES * MAX_TILES];

            // To avoid the GL driver having to synchronously copy all our texture
            // data to GL-controlled memory, we copy tiles directly into a
            // GL-controlled buffer. Then the driver can upload on its own time.

            // Setup buffers to copy pixels into
            pboHght[0] = CreateMappableBuffer(BYTES_PER_HGHT * MAX_TILES * MAX_TILES);
            pboMate[0] = CreateMappableBuffer(BYTES_PER_MATE * MAX_TILES * MAX_TILES);
            pboHght[1] = CreateMappableBuffer(BYTES_PER_HGHT * MAX_TILES * MAX_TILES);
            pboMate[1] = CreateMappableBuffer(BYTES_PER_MATE * MAX_TILES * MAX_TILES);

            if (pboHght[0] == 0 || pboHght[1] == 0) {
                Console.WriteLine("Unable to create HGHT buffer(s)!");
                return;
            }
            if (pboMate[0] == 0 || pboMate[1] == 0) {
                Console.WriteLine("Unable to create MATE buffer(s)!");
                return;
            }
            
            // Map the pixel buffers into CPU address space
            var zMap = Profiler.BeginZone("R_MapTileBuffer");
            SwapPBOs(); // We don't really need a swap but this will map them
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
                CopyTileToPBOByRow(pos, hghtData.AsSpan().AsBytes(), hghtBuf, 2);
                CopyTileToPBOByRow(pos, mateData.AsSpan().AsBytes(), mateBuf, 4);

                idxList[pos] = val;
            });
            zCopy.Dispose();
            
            var zUpload = Profiler.BeginZone("R_UploadTile");
            SwapPBOs(); // Unmap the buffer we were using so we can upload from it
            
            // Tell the driver to upload our buffers as textures on its own time
            // This returns more or less immediately (compared to a normal texture upload)
            GL.BindTexture(TextureTarget.Texture2D, hghtTex);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboHght[inactivePBO]);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, MAX_SIZE, MAX_SIZE, PixelFormat.Red, PixelType.UnsignedShort, 0);
            
            GL.BindTexture(TextureTarget.Texture2D, mateTex);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboMate[inactivePBO]);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, MAX_SIZE, MAX_SIZE, PixelFormat.Rgba, PixelType.UnsignedByte, 0);
            
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);

            GL.InvalidateBufferData(pboHght[inactivePBO]);
            GL.InvalidateBufferData(pboMate[inactivePBO]);
            zUpload.Dispose();
            
            Console.WriteLine("Found {0}/{1} tiles", tilesFound, tilesTried);
            zone.Dispose();
        }

        public void Draw(int tilesPerTexLoc, int indicesLocation, int minDist, int maxDist, UInt16 centerTile) {
            var z = Profiler.BeginZone("R_DrawTileSheet");
            ProcessTileUpdates();
            
            GL.BindTextureUnit(0, hghtTex);
            GL.BindTextureUnit(1, mateTex);
            Uniform.Set(tilesPerTexLoc, MAX_TILES);

            var temp = indices;
            GL.Uniform1(indicesLocation, temp.Count, temp.ToArray());
            GL.DrawArraysInstanced(PrimitiveType.Patches, 0, 4, temp.Count);
            z.Dispose();
        }

        public void GLUninit() {
            GL.UnmapNamedBuffer(pboHght[activePBO]);
            GL.UnmapNamedBuffer(pboMate[activePBO]);
            GL.DeleteBuffer(pboHght[0]);
            GL.DeleteBuffer(pboMate[0]);
            GL.DeleteBuffer(pboHght[1]);
            GL.DeleteBuffer(pboMate[1]);
            GL.DeleteTexture(hghtTex);
            GL.DeleteTexture(mateTex);
        }
    }

    /// <summary>
    /// A region of tiles backed by potentially many atlases
    /// </summary>
    public struct TileRegion {
        public readonly List<CompactTileSheet> sheets = [];

        public TileRegion(Cache.CoverageMap lodCoverage, Cache.Cache cache, byte sizeTiles, byte xCenter, byte yCenter) {
            var zone = Profiler.BeginZone("R_CreateTileRegion");
            zone.EmitValue(sizeTiles);

            // Build deduplicated set of packed index values
            var indices = lodCoverage.FindAllTilesInManhattanRadius(xCenter, yCenter, sizeTiles);
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

        public void UploadNewTiles(Cache.CoverageMap lodCoverage, Cache.Cache cache, byte xCenter, byte yCenter, byte sizeTiles) {
            var z = Profiler.BeginZone("R_FindNewTiles");
            // The set of tiles in the draw radius (i.e. that should be in VRAM)
            var inGroup = lodCoverage.FindAllTilesInManhattanRadius(xCenter, yCenter, sizeTiles);
            // The set of tiles that should be in VRAM, but aren't yet
            var missingGroup = new HashSet<int>(inGroup);
            
            foreach (var s in sheets) {
                s.RemovePresentIDs(missingGroup);
            }
            z.Dispose();

            if (missingGroup.Count == 0) {
                return; // Nothing to update.
            }
            
            var uploadZone = Profiler.BeginZone("UploadNewTiles");
            ConcurrentQueue<int> idxQ = new(missingGroup);
            foreach (var sheet in sheets) {
                foreach (var slot in sheet.GetStaleTiles(inGroup)) {
                    if (!idxQ.TryDequeue(out var packed)) {
                        break;
                    }
                    
                    ZOrder.UnpackIndex(packed, out var idx, out var lod);
                    var hghtRes = cache.GetHeightmapTile(lod, idx, false);
                    var mateRes = cache.GetMaterialTile(lod, idx, false);
                    if (hghtRes.IsErr() || mateRes.IsErr()) {
                        continue; // Tile doesn't exist
                    }

                    // Overwrite the stale tiles with newly in range ones
                    sheet.ScheduleTileUpdate(slot, hghtRes.Unwrap().AsBytes(), LodComponent.hght);
                    sheet.ScheduleTileUpdate(slot, mateRes.Unwrap().AsBytes(), LodComponent.mate);
                    sheet.indices[slot] = packed;
                }
            }
            uploadZone.Dispose();
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
            
            Console.WriteLine("Unable to update tile {0} @ lvl {1}", idx, lod);
            return false;
        }

        public void GLUninit() {
            foreach (var sheet in sheets) {
                sheet.GLUninit();
            }
        }
    }
    
    Shader tessShader;
    int vaoBlank = 0; // We need a blank VAO even when vertices are hardcoded in the shader
    public int terrainTexArray = 0;
    int coverageTex = 0;
    public Cache.CoverageMap lodCoverage;

    TileRegion ring0;
    public int renderRadius = 32;

    /// <summary>
    /// Update a rendered tile.
    /// The render thread streams tiles to the GPU at the start of each frame.
    /// </summary>
    public bool ScheduleTileUpdate(UInt16 idx, byte lod, ReadOnlySpan<byte> data, LodComponent type)
    {
        return ring0.ScheduleTileUpdate(idx, lod, data, type);
    }

    public bool ScheduleTileUpdate(UInt16 idx, byte lod, LodComponent type, Cache.Cache cache)
    {
        ReadOnlySpan<byte> data = new();
        switch (type) {
            case LodComponent.hght: {
                var t = cache.GetHeightmapTile(lod, idx, false);
                if (t.IsErr()) {
                    Console.WriteLine("Couldn't find tile {0} @ lvl {1}", idx, lod);
                    return false;
                }
                data = t.Unwrap().AsBytes();
                break;
            }
            case LodComponent.mate: {
                var t = cache.GetMaterialTile(lod, idx, false);
                if (t.IsErr()) {
                    Console.WriteLine("Couldn't find tile {0} @ lvl {1}", idx, lod);
                    return false;
                }
                data = t.Unwrap().AsBytes();
                break;
            }
            case LodComponent.water: {
                var t = cache.GetWaterTile(lod, idx, false);
                if (t.IsErr()) {
                    Console.WriteLine("Couldn't find tile {0} @ lvl {1}", idx, lod);
                    return false;
                }
                data = t.Unwrap().AsBytes();
                break;
            }
            case LodComponent.grass: {
                var t = cache.GetGrassTile(lod, idx);
                if (t.IsErr()) {
                    Console.WriteLine("Couldn't find tile {0} @ lvl {1}", idx, lod);
                    return false;
                }
                data = t.Unwrap().AsBytes();
                break;
            }
        }

        return ScheduleTileUpdate(idx, lod, data, type);
    }

    public TerrainRenderer() { }

    public bool GLInit() {
        var zone = Profiler.BeginZone("R_GLInit");
        string vert = GetEmbeddedText("terrainBench.Shaders.terrain.vert.glsl");
        string tcs =  GetEmbeddedText("terrainBench.Shaders.terrain.tcs.glsl");
        string tess = GetEmbeddedText("terrainBench.Shaders.terrain.tess.glsl");
        string geom = GetEmbeddedText("terrainBench.Shaders.terrain.geom.glsl");
        string frag = GetEmbeddedText("terrainBench.Shaders.terrain.frag.glsl");

        using (Profiler.BeginZone("R_ShaderCompile")) {
            tessShader = new Shader(vert, tcs, tess, geom, frag);
        }
        vaoBlank = GL.GenVertexArray();
        GL.PatchParameter(PatchParameterInt.PatchVertices, 4);

        zone.Dispose();
        return true;
    }

    public bool LoadTerrainTiles(Cache.Cache cache) {
        lodCoverage = new(cache);
        coverageTex = CreateTileTexture(SizedInternalFormat.R8, HGHT_DIM);
        GL.TextureSubImage2D(coverageTex, 0, 0, 0, HGHT_DIM, HGHT_DIM, PixelFormat.Red, PixelType.UnsignedByte, lodCoverage.map);

        var loadWatch = Stopwatch.StartNew();
        ring0 = new(lodCoverage, cache, 32, 128, 128);
        loadWatch.Stop();
        
        Console.WriteLine("Loaded all detail levels in {0}ms total.", loadWatch.ElapsedMilliseconds);
        return true;
    }

    public void UnloadTerrainTiles() {
        lodCoverage = null;
        GL.DeleteTexture(coverageTex);
        ring0.GLUninit();
    }

    public void GLUninit() {
        foreach (var s in ring0.sheets) {
            s.GLUninit();
        }
        GL.DeleteTexture(coverageTex);
        GL.DeleteTexture(terrainTexArray);
        GL.DeleteVertexArray(vaoBlank);
        tessShader.FreeResources();
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

        string texPath = Path.Combine("Pack", "TitleBG.pack//Model//Terrain.Tex.sbfres");
        string tex1Path = Path.Combine("Model", "Terrain.Tex1.sbfres");
        string tex2Path = Path.Combine("Pack", "TitleBG.pack//Model//Terrain.Tex2.sbfres");
        
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

    public void UpdateGPUTiles(Cache.Cache cache, Vector2i eyeTile) {
        ring0.UploadNewTiles(lodCoverage, cache, (byte)eyeTile.X, (byte)eyeTile.Y, (byte)renderRadius);
    }

    public void Render(Matrix4 projT, Matrix4 viewT, TerrainCoords.WorldPos eyeWorld) {
        var z = Profiler.BeginZone("R_RenderTerrain");
        tessShader.Use();
        // Upload camera state
        tessShader.SetUniform("matView", viewT);
        tessShader.SetUniform("matProjection", projT);
        tessShader.SetUniform("matModel", TerrainCoords.WorldPos.FromTileGridXform());

        int indicesLocation = tessShader.GetUniformLocation("indices");
        int tilesPerTexLoc = tessShader.GetUniformLocation("tilesPerTex");
        
        // This is specified in the shader via an extension, but for some reason
        // Nvidia hardware doesn't respect it and assigns locations seemingly at random
        tessShader.SetUniform("heightTex", 0);
        tessShader.SetUniform("matTex", 1);
        tessShader.SetUniform("colorTextures", 2);
        tessShader.SetUniform("coverageTex", 3);
        
        GL.BindTextureUnit(2, terrainTexArray);
        GL.BindTextureUnit(3, coverageTex);
        GL.BindVertexArray(vaoBlank); // Required despite vertices being baked into the shader

        var eyeTile = (TerrainCoords.TileGrid8Pos)eyeWorld;
        var center = ZOrder.Interleave8To16((byte)eyeTile.x, (byte)eyeTile.z);
        ring0.Draw(tilesPerTexLoc, indicesLocation, 0, renderRadius, center);

        GL.BindVertexArray(0);
        z.Dispose();
    }
}
