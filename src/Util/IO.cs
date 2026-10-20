using System.Security;
using OperationResult;
using static OperationResult.Helpers;
namespace terrainBench;

public static class IO {
    public static Result<FileInfo, ErrorStack> GetFileInfo(string path) {
        using (Profiler.BeginZone("GetFileInfo")) {
            try {
                return new FileInfo(path);
            } catch (UnauthorizedAccessException e) {
                return Err(new ErrorStack($"Unauthorized to access the file '{path}'", e));
            } catch (PathTooLongException) {
                return Err(new ErrorStack($"The path was too long: '{path}'"));
            } catch (NotSupportedException) {
                return Err(new ErrorStack($"The path contains a colon, which is not allowed: '{path}'"));
            } catch (SecurityException) {
                return Err(new ErrorStack($"You don't have the permissions to access the file '{path}'"));
            } catch (Exception e) {
                return Err(new ErrorStack($"Unknown error accessing file '{path}'", e));
            }
        }
    }

    public static Task<Result<MappedFile, ErrorStack>> MapAsync(string path) {
        return Task.Run(() => {
            return MappedFile.Create(path);
        });
    }
}