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
        var res = 8;
        float radius = 10.0f;
        shader.Uniform("resolution")?.SetValue(res);
        shader.Uniform("radius")?.SetValue(radius);
        
        shader.Uniform("projT")?.SetValue(projT);
        shader.Uniform("viewT")?.SetValue(viewT);
        shader.Uniform("modelT")?.SetValue(modelT);

        GL.BindVertexArray(vaoBlank); // Required despite vertices being baked into the shader
        shader.Use();
        GL.DrawArrays(PrimitiveType.LineStrip, 0, res + 1);
        GL.BindVertexArray(0);
    }
}
