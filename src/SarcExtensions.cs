namespace terrainBench;

public static class SarcExtensions
{
    extension(CsOead.Sarc sarc)
    {
        public void WriteCompressed(string path, CsOead.Endianness endian, int compressLevel = 7)
        {
            var z = Profiler.BeginZone("Sarc.WriteCompressed");
            
            var toBin = Profiler.BeginZone("Sarc.ToBinary");
            var data = sarc.ToBinary(endian);
            toBin.Dispose();
            
            var compress = Profiler.BeginZone("Yaz0.Compress");
            var compressedData = CsOead.Yaz0.Compress(data, 0, compressLevel);
            compress.Dispose();
            data.Close();
            
            var save = Profiler.BeginZone("File.WriteAllBytes");
            File.WriteAllBytes(path, compressedData);
            save.Dispose();
            
            // Make sure these are cleaned up ASAP to reduce peak usage
            compressedData.Close();
            z.Dispose();
        }
    }
}