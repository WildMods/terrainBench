// Adapted from Avalonia samples:
// https://github.com/AvaloniaUI/Avalonia/blob/main/samples/ControlCatalog/Pages/OpenGl/OpenGlFbo.cs
using SkiaSharp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
namespace terrainBench.UI;

internal class OpenGlFbo : IDisposable {
    private int _fbo;
    private int _depthBuffer;
    public int colorTexture;
    public Vector2i Size;

    public void GLInit()
    {
        _fbo = GL.GenFramebuffer();
    }

    public void Bind() {
        GL.Viewport(0, 0, Size.X, Size.Y);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
    }
    public void Unbind() {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }
    
    public void Resize(Vector2i newSize) {
        if(Size == newSize)
            return;

        Console.WriteLine("Resizing from {0} -> {1}", Size, newSize);
        if (colorTexture != 0)
            GL.DeleteTexture(colorTexture);
        colorTexture = 0;
        if(_depthBuffer != 0)
            GL.DeleteRenderbuffer(_depthBuffer);
        _depthBuffer = 0;
        Bind();

        colorTexture = GL.GenTexture();

        var textureFormat = PixelInternalFormat.Rgba8;
        
        // had to use OpenTK enums and cast to them for OpenTK calls:
        // "PixelInternalFormat", "RenderbufferTarget", "TextureTarget", "TextureTargetMultisample", "RenderbufferStorage"
        // -- torf
        GL.BindTexture(TextureTarget.Texture2D, colorTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, (PixelInternalFormat)textureFormat, newSize.X, newSize.Y, 0, PixelFormat.Rgba, PixelType.Byte, IntPtr.Zero);
        // GL.TexImage2D(GL_TEXTURE_2D, 0, textureFormat, size.Width, size.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, IntPtr.Zero);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, colorTexture, 0);
        GL.TextureParameter(colorTexture, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TextureParameter(colorTexture, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        
        _depthBuffer = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthBuffer);
        // Note this is another OpenTK GL call.
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent, newSize.X, newSize.Y);
        // GL.RenderbufferStorage(GL_RENDERBUFFER, depthFormat, size.Width, size.Height);
        
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depthBuffer);

        // FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        IsValid = (status == FramebufferErrorCode.FramebufferComplete);
        if(!IsValid) {
            Console.WriteLine("Unable to configure OpenGL FBO (status {0})", status);
        }
        
        Size = newSize;
        Unbind();
    }

    public bool IsValid { get; private set; }

    public int Fbo => _fbo;

    public void Dispose() {
        if(_fbo != 0)
            GL.DeleteFramebuffer(_fbo);
        _fbo = 0;
        if (_depthBuffer != 0)
            GL.DeleteRenderbuffer(_depthBuffer);
        if(colorTexture != 0)
            GL.DeleteTexture(colorTexture);
    }
}
