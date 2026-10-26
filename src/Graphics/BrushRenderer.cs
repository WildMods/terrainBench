using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using terrainBench.Core;
using SNVector2 = System.Numerics.Vector2;
using SNVector4 = System.Numerics.Vector4;

namespace terrainBench.Graphics;
using static Core.TerrainCoords;

public class BrushRenderer {
    private Shader? brushShader = null;
    private Shader? texShader = null;
    public float radius = 50f;
    int vaoBlank;

    private int brushStrengthTex = 0;

    public void UploadBrushStrengthTex(Brush brush) {
        var size = brush.Width;
        GL.CreateTextures(TextureTarget.Texture2D, 1, out brushStrengthTex);
        GL.TextureStorage2D(brushStrengthTex, 1, SizedInternalFormat.R32f, size, size);
        GL.TextureSubImage2D(brushStrengthTex, 0, 0, 0, size, size, PixelFormat.Red, PixelType.Float, brush.strengthTable);

        int[] temp = { (int)TextureSwizzle.Red };
        GL.TextureParameterI(brushStrengthTex, TextureParameterName.TextureSwizzleG, temp);
        GL.TextureParameterI(brushStrengthTex, TextureParameterName.TextureSwizzleB, temp);
    }

    public bool GLInit(Brush brush) {
        string prefix = "terrainBench.Graphics.Shaders.";
        var brushVert = GLUtil.GetEmbeddedText(prefix + "brush.vert.glsl");
        var brushFrag = GLUtil.GetEmbeddedText(prefix + "brush.frag.glsl");
        var texVert = GLUtil.GetEmbeddedText(prefix + "texQuad.vert.glsl");
        var texFrag = GLUtil.GetEmbeddedText(prefix + "texQuad.frag.glsl");
        brushShader = new(brushVert, brushFrag);
        texShader = new(texVert, texFrag);
        vaoBlank = GL.GenVertexArray();
        UploadBrushStrengthTex(brush);

        return true;
    }
    
    public void GLUninit() {
        brushShader?.FreeResources();
        texShader?.FreeResources();
        GL.DeleteVertexArray(vaoBlank);
    }

    public void ImGuiShowBrushTex(Brush brush) {
        var size = brush.Width;
        var mult = 1.0f / brush.baseStrength;
        SNVector2 uv0 = new(0, 0);
        SNVector2 uv1 = new(1, 1);
        
        // Apply a "tint" to rescale the strength back to a 0 to 1 range.
        SNVector4 tint = new(mult, mult, mult, 1.0f);
        ImGui.Image(brushStrengthTex, new SNVector2(size), uv0, uv1, tint);
    }

    public void Draw(Brush brush, Matrix4 projT, Matrix4 viewT, int textureA, int textureB) {
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
