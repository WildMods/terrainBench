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

}
