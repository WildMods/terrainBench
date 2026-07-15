using CsOead;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

public static class TerrainBench {
    public static void Main(string[] args) {
        // Decompress input files
        foreach (string s in args) {
            if (!s.EndsWith(".sstera")) {
                Console.WriteLine("Skipping {0}", s);
                continue;
            }

            try {
                Console.WriteLine("Decompressing {0}", s);
                byte[]? buf = null;
                Yaz0.TryDecompress(s, out buf);
                if (buf == null) {
                    Console.WriteLine("Failed to decompress.");
                    continue;
                }

                string contentFile = s.Replace(".sstera", "");
                string outPath = contentFile + ".dec";

                var sarc = CsOead.Sarc.FromBinary(buf);
                foreach ((var file, var buffer) in sarc) {
                    Console.WriteLine("{0}", file);
                }
                /*
                // Write decompressed file
                using (var f = new FileStream(outPath, FileMode.Create, FileAccess.Write)) {
                    f.Write(buf, 0, buf.Length);
                }
                */
            } catch (System.Exception e) {
                Console.WriteLine(e.Message);
            }
        }

        var nativeWindowSettings = new NativeWindowSettings() {
            ClientSize = new Vector2i(800, 600),
            Title = "Terrain Workbench",
            Flags = ContextFlags.ForwardCompatible, // Required to run on MacOS
        };

        using (var window = new Window(GameWindowSettings.Default, nativeWindowSettings)) {
            window.Run();
        }
   }
}
