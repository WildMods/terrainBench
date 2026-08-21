using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace terrainBench;
using Graphics;

public class BrushRenderer {
    private Shader? shader = null;
    public Matrix4 modelT = Matrix4.Identity;
    public float radius = 50f;
    int vaoBlank;

    public bool GLInit() {
        var vert = GLUtil.GetEmbeddedText("terrainBench.Shaders.brush.vert.glsl");
        var frag = GLUtil.GetEmbeddedText("terrainBench.Shaders.brush.frag.glsl");
        shader = new(vert, frag);
        vaoBlank = GL.GenVertexArray();

        return true;
    }
    
    public void GLUninit() {
        shader.FreeResources();
        GL.DeleteVertexArray(vaoBlank);
    }

    public void Draw(Matrix4 projT, Matrix4 viewT) {
        const int res = 32;
        shader.Use();
        shader.SetUniform("resolution", res);
        shader.SetUniform("radius", radius);
        
        shader.SetUniform("projT", projT);
        shader.SetUniform("viewT", viewT);
        shader.SetUniform("modelT", modelT);
        
        GL.BindVertexArray(vaoBlank); // Required despite vertices being baked into the shader
        GL.DrawArrays(PrimitiveType.LineStrip, 0, res + 1);
        GL.BindVertexArray(0);
    }
}
