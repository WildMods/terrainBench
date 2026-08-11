using OpenTK.Graphics.OpenGL4;
using SmoothGL.Graphics.Shader;
using OpenTK.Mathematics;

namespace terrainBench;

public class BrushRenderer {
    private Shader? shader = null;
    public Matrix4 modelT = Matrix4.Identity;
    int vaoBlank;

    public bool GLInit() {
        var vert = GLUtil.GetEmbeddedText("terrainBench.Shaders.brush.vert.glsl");
        var frag = GLUtil.GetEmbeddedText("terrainBench.Shaders.brush.frag.glsl");
        shader = new(vert, frag);
        vaoBlank = GL.GenVertexArray();

        return true;
    }

    public void Draw(Matrix4 projT, Matrix4 viewT) {
        const int res = 32;
        const float radius = 10.0f;
        Uniform.Set(shader.programId, "resolution", res);
        Uniform.Set(shader.programId, "radius", radius);
        
        Uniform.Set(shader.programId, "projT", projT);
        Uniform.Set(shader.programId, "viewT", viewT);
        Uniform.Set(shader.programId, "modelT", modelT);
        
        GL.BindVertexArray(vaoBlank); // Required despite vertices being baked into the shader
        shader.Use();
        GL.DrawArrays(PrimitiveType.LineStrip, 0, res + 1);
        GL.BindVertexArray(0);
    }
}
