
public static class TerrainBench {

    public static void Main(string[] args) {
        foreach (string s in args) {
            try {
                Console.WriteLine("Decompressing {0}", s);
                byte[]? buf = null;
                CsOead.Yaz0.TryDecompress(s, out buf);
                if (buf == null) {
                    Console.WriteLine("Failed to decompress.");
                    continue;
                }

                string outPath = s + ".dec";
                using (var f = new FileStream(outPath, FileMode.Create, FileAccess.Write)) {
                    f.Write(buf, 0, buf.Length);
                }
            } catch (System.Exception e) {
                Console.WriteLine(e.Message);
            }
        }
    }
}
