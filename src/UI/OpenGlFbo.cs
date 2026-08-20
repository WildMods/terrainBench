// Taken from Avalonia samples:
// https://github.com/AvaloniaUI/Avalonia/blob/main/samples/ControlCatalog/Pages/OpenGl/OpenGlFbo.cs
using Avalonia;
using Avalonia.OpenGL;
using SkiaSharp;
using static Avalonia.OpenGL.GlConsts;
using OpenTK.Graphics.OpenGL4;
namespace terrainBench.UI;

internal class OpenGlFbo : IDisposable {
    private readonly GRContext _grContext;
    private int _fbo;
    private int _depthBuffer;
    private int _texture;
    private PixelSize _size;
    public PixelSize Size => _size;
    public GlInterface Gl => Context.GlInterface;
    public IGlContext Context { get; }

    public OpenGlFbo(IGlContext context, GRContext grContext) {
        _grContext = grContext;
        Context = context;
        _fbo = Gl.GenFramebuffer();
    }
    
    public void Resize(PixelSize size) {
        if(_size == size)
            return;

        if (_texture != 0)
            Gl.DeleteTexture(_texture);
        _texture = 0;
        if(_depthBuffer != 0)
            Gl.DeleteRenderbuffer(_depthBuffer);
        _depthBuffer = 0;
        Gl.BindFramebuffer(GL_FRAMEBUFFER, _fbo);

        _texture = Gl.GenTexture();
            
        var textureFormat = Context.Version.Type == GlProfileType.OpenGLES && Context.Version.Major == 2
            ? GL_RGBA
            : GL_RGBA8;
        
        // WARNING: This is a bit sketchy, I'm having to use the OpenTK OpenGL
        // bindings to access multisample functions that aren't exposed by
        // Avalonia's bindings. This is fine only because I've explicitly had
        // OpenTK load its bindings from Avalonia's GL context. OpenTK calls
        // have a "GL." prefix, Avalonia calls start with "Gl."
        // Some enum values are also missing from Avalonia's bindings, so I've
        // had to use OpenTK enums and cast to them for OpenTK calls:
        // "PixelInternalFormat", "RenderbufferTarget", "TextureTarget", "TextureTargetMultisample", "RenderbufferStorage"
        // -- torf
        Gl.BindTexture((int)TextureTarget.Texture2DMultisample, _texture);
        GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, 4, (PixelInternalFormat)textureFormat, size.Width, size.Height, true);
        // Gl.TexImage2D(GL_TEXTURE_2D, 0, textureFormat, size.Width, size.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, IntPtr.Zero);
        Gl.FramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, (int)TextureTarget.Texture2DMultisample, _texture, 0);
        
        _depthBuffer = Gl.GenRenderbuffer();
        var depthFormat = Context.Version.Type == GlProfileType.OpenGLES
            ? GL_DEPTH_COMPONENT16
            : GL_DEPTH_COMPONENT;
        Gl.BindRenderbuffer(GL_RENDERBUFFER, _depthBuffer);
        // Note this is another OpenTK GL call.
        GL.RenderbufferStorageMultisample((RenderbufferTarget)GL_RENDERBUFFER, 4, (RenderbufferStorage)depthFormat, size.Width, size.Height);
        // Gl.RenderbufferStorage(GL_RENDERBUFFER, depthFormat, size.Width, size.Height);
        
        Gl.FramebufferRenderbuffer(GL_FRAMEBUFFER, GL_DEPTH_ATTACHMENT, GL_RENDERBUFFER, _depthBuffer);

        // FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        var status = Gl.CheckFramebufferStatus(GL_FRAMEBUFFER);
        IsValid = (status == GL_FRAMEBUFFER_COMPLETE);
        if(!IsValid)
        {
            int code = Gl.GetError();
            Console.WriteLine("Unable to configure OpenGL FBO: {0} (status {1})", code, status);
        }
        
        _size = size;
    }

    public bool IsValid { get; private set; }

    public int Fbo => _fbo;

    public SKImage? Snapshot() {
#if false
        var z = Profiler.BeginZone("OpenGlFbo.Snapshot");
        Gl.Flush();
        _grContext.ResetContext();
        
        using var texture = new GRBackendTexture(_size.Width, _size.Height, false,
            new GRGlTextureInfo(GlConsts.GL_TEXTURE_2D, (uint)_texture, SKColorType.Rgba8888.ToGlSizedFormat()));
        
        var surf = SKSurface.Create(_grContext, texture, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        if (surf == null) {
            using var unformatted = new GRBackendTexture(_size.Width, _size.Height, false,
                new GRGlTextureInfo(GlConsts.GL_TEXTURE_2D, (uint)_texture));

            surf = SKSurface.Create(_grContext, unformatted, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        }

        SKImage? rv;
        using (surf)
            rv = surf?.Snapshot();
        _grContext.Flush();
        z.Dispose();
        return rv;
#else
        var target = new GRBackendRenderTarget(_size.Width, _size.Height, 4, 0,
            new GRGlFramebufferInfo((uint)_fbo, SKColorType.Rgba8888.ToGlSizedFormat()));
        SKImage rv;
        using (var surface = SKSurface.Create(_grContext, target,
                   GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888,
                   new SKSurfaceProperties(SKPixelGeometry.RgbHorizontal)))
            rv = surface.Snapshot();
        _grContext.Flush();
        return rv;
#endif
    }
    
    public void Dispose() {
        if(_fbo != 0)
            Gl.DeleteFramebuffer(_fbo);
        _fbo = 0;
        if (_depthBuffer != 0)
            Gl.DeleteRenderbuffer(_depthBuffer);
        if(_texture != 0)
            Gl.DeleteTexture(_texture);
    }
}
