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

    public static Result<string, ErrorStack> ReadAllText(string path) {
        string msg = "Unable to read text file";
        try {
            return File.ReadAllText(path);
        } catch (PathTooLongException) {
            return Err(new ErrorStack($"{msg}, the path was too long: '{path}'"));
        } catch (DirectoryNotFoundException) {
            return Err(new ErrorStack($"{msg}, a directory in the path wasn't found. '{path}'"));
        } catch (FileNotFoundException) {
            return Err(new ErrorStack($"{msg}, because it couldn't be found: '{path}'"));
        } catch (IOException e) {
            return Err(new ErrorStack($"{msg} due to an I/O error: '{e.Message}'"));
        } catch (Exception e) {
            // There are more potential exception classes, but I can't be
            // bothered to add all the handling. 
            // -- torf
            return Err(new ErrorStack($"{msg} because: '{e.Message}'"));
        }
    }

    public static Task<Result<MappedFile, ErrorStack>> MapAsync(string path) {
        return Task.Run(() => {
            return MappedFile.Create(path);
        });
    }
    
    /// <summary>
    /// OpenWrite wrapper without exceptions
    /// </summary>
    public static Result<Stream, ErrorStack> OpenWrite(string loose) {
        string baseErr = $"Unable to open ${loose} for writing: ";
        try {
            var f = File.OpenWrite(loose);
            return f;
        } catch (DirectoryNotFoundException) {
            return Err(new ErrorStack($"${baseErr} The containing directory couldn't be found"));
        } catch (UnauthorizedAccessException) {
            return Err(new ErrorStack($"${baseErr} Unauthorized access"));
        } catch (PathTooLongException) {
            return Err(new ErrorStack($"${baseErr} path too long"));
        } catch (Exception e) {
            return Err(new ErrorStack($"${baseErr} '${e.Message}'"));
        }
    }

}