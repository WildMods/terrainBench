using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
namespace terrainBench;

public static class Uniform
{
    public static void Set(int location, Matrix4 value) {
        GL.UniformMatrix4(location, false, ref value);
    }
    
    public static void Set(int location, int value) {
        GL.Uniform1(location, value);
    }
    
    public static void Set(int location, float value) {
        GL.Uniform1(location, value);
    }
    
    public static void Set(int shaderID, string name, Matrix4 value) {
        Set(GL.GetUniformLocation(shaderID, name), value);
    }
    
    public static void Set(int shaderID, string name, int value) {
        Set(GL.GetUniformLocation(shaderID, name), value);
    }
    
    public static void Set(int shaderID, string name, float value) {
        Set(GL.GetUniformLocation(shaderID, name), value);
    }
}