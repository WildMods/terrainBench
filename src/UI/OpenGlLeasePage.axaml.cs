// Adapted from Avalonia samples:
// https://github.com/AvaloniaUI/Avalonia/blob/main/samples/ControlCatalog/Pages/OpenGl/OpenGlLeasePage.xaml.cs
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using OpenTK.Graphics.OpenGL4;
using SkiaSharp;
using terrainBench.UI.ViewModels;
using static Avalonia.OpenGL.GlConsts;

namespace terrainBench.UI;

public partial class OpenGlLeasePage : ContentPage {
    private CompositionCustomVisual? _visual;
    private bool _isDragging = false;

    // Persistent input state, since Avalonia only gives events
    protected InputState input = new();

    public class DisposeMessage;
    class GlVisual : CompositionCustomVisualHandler {
        private OpenGlContent _content;
        private bool _contentInitialized;
        private OpenGlFbo? _fbo;
        private IGlContext? _gl;
        private EditorState _editorState;
        private InputState _input;

        public GlVisual(OpenGlContent content, EditorState editorState, InputState input) {
            _content = content;
            _editorState = editorState;
            _input = input;
        }

        public override void OnAnimationFrameUpdate() {
            Invalidate();
            base.OnAnimationFrameUpdate();
        }

        public override void OnRender(ImmediateDrawingContext drawingContext) {
            Profiler.EmitFrameMark();
            var z = Profiler.BeginZone("OpenGlLeasePage.OnRender");
            RegisterForNextAnimationFrameUpdate();
            var bounds = GetRenderBounds();
            var size = PixelSize.FromSize(bounds.Size, 1);
            if (size.Width < 1 || size.Height < 1)
                return;
            
            if (drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>(out var skiaFeature)) {
                using var skiaLease = skiaFeature.Lease();
                var grContext = skiaLease.GrContext;
                if (grContext == null)
                    return;
                // Borrow the graphics API context
                SKImage? snapshot;
                using (var platformApiLease = skiaLease.TryLeasePlatformGraphicsApi()) {
                    if (platformApiLease?.Context is not IGlContext glContext)
                        return;

                    var gl = glContext.GlInterface;
                    if (_gl != glContext) {
                        // The old context is lost
                        _fbo = null;
                        _contentInitialized = false;
                        _gl = glContext;
                        // Hook up OpenTK to our context
                        AvaloniaTkContext avaCtx = new(gl);
                        GL.LoadBindings(avaCtx);
                    }

                    // Render to a new framebuffer
                    gl.GetIntegerv(GL_FRAMEBUFFER_BINDING, out var oldFb);

                    _fbo ??= new OpenGlFbo(glContext, grContext);
                    if (_fbo.Size != size)
                        _fbo.Resize(size);

                    gl.BindFramebuffer(GL_FRAMEBUFFER, _fbo.Fbo);

                    
                    if (!_contentInitialized) {
                        _content.Init(gl, glContext.Version, _editorState);
                        _contentInitialized = true;
                    }

                    z.Dispose();
                    _content.OnOpenGlRender(gl, _fbo.Fbo, size, _editorState, _input);
                    _input.ResetKeyStates();

                    // Have the rendered frame copied to a presentable texture
                    snapshot = _fbo.Snapshot();
                    gl.BindFramebuffer(GL_FRAMEBUFFER, oldFb);
                }

                var presentZone = Profiler.BeginZone("OnRender Present");
                // Present the image normally
                using(snapshot)
                    if (snapshot != null)
                        skiaLease.SkCanvas.DrawImage(snapshot, new SKRect(0, 0,
                            (float)bounds.Width, (float)bounds.Height));
                presentZone.Dispose();
            }
        }

        public override void OnMessage(object message) {
            if (message is DisposeMessage) {
                if (_gl != null) {
                    try {
                        if (_fbo != null || _contentInitialized) {
                            using (_gl.MakeCurrent()) {
                                if (_contentInitialized)
                                    _content.Deinit(_gl.GlInterface, _editorState);
                                _contentInitialized = false;
                                _fbo?.Dispose();
                                _fbo = null;
                            }
                        }
                    } catch (Exception e) {
                        Console.WriteLine(e.ToString());
                    }

                    _gl = null;
                }
            } else {
                RegisterForNextAnimationFrameUpdate();
                
            }

            base.OnMessage(message);
        }
    }
    
    public OpenGlLeasePage() {
        AvaloniaXamlLoader.Load(this);
        InitializeComponent();
    }

    private void ViewportAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) {
        var visual = ElementComposition.GetElementVisual(Viewport);
        if(visual == null)
            return;
        
        Debug.Assert(DataContext is EditorState);
        var vm = (EditorState)DataContext;
        _visual = visual.Compositor.CreateCustomVisual(new GlVisual(new OpenGlContent(), vm, input));
        ElementComposition.SetElementChildVisual(Viewport, _visual);
        UpdateSize(Bounds.Size);
    }

    private void UpdateSize(Size size) {
        if (_visual != null)
            _visual.Size = new Vector(size.Width, size.Height);
    }

    protected override Size ArrangeOverride(Size finalSize) {
        var size = base.ArrangeOverride(finalSize);
        UpdateSize(size);
        return size;
    }

    private void ViewportDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e) {
        _visual?.SendHandlerMessage(new DisposeMessage());
        _visual = null;
        ElementComposition.SetElementChildVisual(Viewport, null);
        base.OnDetachedFromVisualTree(e);
    }
    
    protected override void OnKeyDown(KeyEventArgs e) {
        if (!IsEffectivelyVisible)
            return;
        
        input.SetKey(e.Key, true);
    }
    
    protected override void OnKeyUp(KeyEventArgs e) {
        if (!IsEffectivelyVisible)
            return;
        
        input.SetKey(e.Key, false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        input.SetMouseButton(e.GetCurrentPoint(this).Properties.PointerUpdateKind.GetMouseButton(), true);

        _isDragging = true;
        e.Pointer.Capture(this);
        input.SetMousePosition(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        input.SetMousePosition(e.GetPosition(this));
        input.SetMouseButton(e.GetCurrentPoint(this).Properties.PointerUpdateKind.GetMouseButton(), false);

        _isDragging = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e) {
        input.SetMousePosition(e.GetPosition(this));

        if (!_isDragging)
            return;

        input.CalcMouseDelta();
    }
}