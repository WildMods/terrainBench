using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace terrainBench;
using Graphics;

public class BrushRenderer {
    private Shader? brushShader = null;
    private Shader? texShader = null;
    public Matrix4 modelT = Matrix4.Identity;
    public float radius = 50f;
    int vaoBlank;

    public bool GLInit() {
        var brushVert = GLUtil.GetEmbeddedText("terrainBench.Shaders.brush.vert.glsl");
        var brushFrag = GLUtil.GetEmbeddedText("terrainBench.Shaders.brush.frag.glsl");
        var texVert = GLUtil.GetEmbeddedText("terrainBench.Shaders.texQuad.vert.glsl");
        var texFrag = GLUtil.GetEmbeddedText("terrainBench.Shaders.texQuad.frag.glsl");
        brushShader = new(brushVert, brushFrag);
        texShader = new(texVert, texFrag);
        vaoBlank = GL.GenVertexArray();

        return true;
    }
    
    public void GLUninit() {
        brushShader.FreeResources();
        texShader.FreeResources();
        GL.DeleteVertexArray(vaoBlank);
    }

    public void Draw(Matrix4 projT, Matrix4 viewT, int textureA, int textureB) {
        const int res = 32;
        brushShader.Use();
        brushShader.SetUniform("resolution", res);
        brushShader.SetUniform("radius", radius);
        
        brushShader.SetUniform("projT", projT);
        brushShader.SetUniform("viewT", viewT);
        brushShader.SetUniform("modelT", modelT);
        
        GL.BindVertexArray(vaoBlank); // Required despite vertices being baked into the shader
        GL.DrawArrays(PrimitiveType.LineStrip, 0, res + 1);

        texShader.Use();
        texShader.SetUniform("tex", 0);
        
        // TODO: Properly adjust these NDC sizes by aspect ratio
        texShader.SetUniform("rect", new Vector4(0.4f, 0.5f, 0.7f, 1.0f));
        GL.BindTextureUnit(0, textureA);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        
        texShader.SetUniform("rect", new Vector4(0.7f, 0.5f, 1.0f, 1.0f));
        GL.BindTextureUnit(0, textureB);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        
        GL.BindVertexArray(0);
    }
}
