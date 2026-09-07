using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using terrainBench.Core;

namespace terrainBench.Graphics;
using static Core.TerrainCoords;

public class BrushRenderer {
    private Shader? brushShader = null;
    private Shader? texShader = null;
    public float radius = 50f;
    int vaoBlank;

    public bool GLInit() {
        string prefix = "terrainBench.Graphics.Shaders.";
        var brushVert = GLUtil.GetEmbeddedText(prefix + "brush.vert.glsl");
        var brushFrag = GLUtil.GetEmbeddedText(prefix + "brush.frag.glsl");
        var texVert = GLUtil.GetEmbeddedText(prefix + "texQuad.vert.glsl");
        var texFrag = GLUtil.GetEmbeddedText(prefix + "texQuad.frag.glsl");
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

    public void Draw(Core.Brush brush, Matrix4 projT, Matrix4 viewT, int textureA, int textureB) {
        const int res = 32;
        var pp = new PixelGrid8Pos(brush.center);
        WorldPos wp = pp;

        var modelT = Matrix4.CreateTranslation(wp);

        Vector4 color = brush.target switch {
            LodComponent.hght => new(1, 1, 0, 1),
            LodComponent.mate => new(1, 0, 0, 1),
            LodComponent.grass => new(0, 1, 0, 1),
            LodComponent.water => new(0, 0, 1, 1),
            _ => new(),
        };
        
        brushShader?.Use();
        brushShader?.SetUniform("resolution", res);
        brushShader?.SetUniform("radius", radius);
        
        brushShader?.SetUniform("projT", projT);
        brushShader?.SetUniform("viewT", viewT);
        brushShader?.SetUniform("modelT", modelT);
        brushShader?.SetUniform("color", color);
        
        GL.BindVertexArray(vaoBlank); // Required despite vertices being baked into the shader
        GL.Disable(EnableCap.DepthTest);
        GL.DrawArrays(PrimitiveType.LineStrip, 0, res + 1);
        GL.Enable(EnableCap.DepthTest);

        texShader?.Use();
        texShader?.SetUniform("tex", 0);
        
        // TODO: Properly adjust these NDC sizes by aspect ratio
        texShader?.SetUniform("rect", new Vector4(0.4f, 0.5f, 0.7f, 1.0f));
        GL.BindTextureUnit(0, textureA);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        
        texShader?.SetUniform("rect", new Vector4(0.7f, 0.5f, 1.0f, 1.0f));
        GL.BindTextureUnit(0, textureB);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        
        GL.BindVertexArray(0);
    }
}
