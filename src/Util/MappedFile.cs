using System.IO.MemoryMappedFiles;
using Microsoft.Win32.SafeHandles;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench;

public unsafe class MappedFile
{
    private byte* data = null;
    private long size = 0;
    MemoryMappedFile? file = null;
    private MemoryMappedViewAccessor view;
    
    public static Result<MappedFile, ErrorStack> Create(string path) {
        var infoRes = IO.GetFileInfo(path);
        if (infoRes.IsErr()) {
            return Err(infoRes.Err().Context("Unable to get file info for the memory mapped file."));
        }

        using (Profiler.BeginZone("MappedFile.Create")) {
            MappedFile f = new();
            f.size = infoRes.Unwrap().Length;
            f.file = MemoryMappedFile.CreateFromFile(path, FileMode.Open);
            f.view = f.file.CreateViewAccessor(0, 0,  MemoryMappedFileAccess.Read);
            f.view.SafeMemoryMappedViewHandle.AcquirePointer(ref f.data);

            return f;
        }
    }

    public Span<byte> AsSpan() {
        return new Span<byte>(data, (int)size);
    }
    
    public ReadOnlySpan<byte> AsBytes() {
        return new ReadOnlySpan<byte>(data, (int)size);
    }
    
    public void Dispose() {
        view.SafeMemoryMappedViewHandle.ReleasePointer();
        view.Dispose();
        file!.Dispose();
        GC.SuppressFinalize(this);
    }

    ~MappedFile() => Dispose();
}