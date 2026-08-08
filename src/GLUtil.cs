using OpenTK.Graphics.OpenGL4;

public static class GLUtil {
    public static int CreateMappableBuffer(int size) {
        int[] buffers = new int[1];
        GL.CreateBuffers(1, buffers);
        int buf = buffers[0];
        if (buf == 0) {
            return 0;
        }

        GL.NamedBufferStorage(buf, size, 0, BufferStorageFlags.ClientStorageBit | BufferStorageFlags.MapWriteBit);
        return buf;
    }

    public static string GetEmbeddedText(string name) {
        var asm = typeof(GLUtil).Assembly;
        Stream? vertStream = asm.GetManifestResourceStream(name);
        if (vertStream == null) {
            return $"#error Unable to load embedded text '{name}'";
        }
        return new StreamReader(vertStream).ReadToEnd();
    }
}
