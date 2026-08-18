// Adapted from:
// https://github.com/Marco2011T2/AvaloniaOpenTK/blob/main/AvaloniaOpenTK/OpenTK/OpenTkControlBase.cs
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using OpenTK.Graphics.OpenGL4;
using Avalonia.Threading;

namespace terrainBench.UI;

// ICustomHitTest is needed to receive input events
public abstract class OpenTkControlBase : OpenGlControlBase, ICustomHitTest
{
    // mouse => see if mouse is clicked and dragged
    private bool _isDragging;

    private AvaloniaTkContext? _avaloniaTkContext;

    protected bool GLinited = false;
    
    // Persistent input state, since Avalonia only gives events
    protected InputState input = new();

    protected double Fps = 165;

    protected abstract void Render();

    protected abstract void Init();

    protected abstract void Deinit();

    protected sealed override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (Bounds.Width != 0 && Bounds.Height != 0)
        {
            Render();
        }

        // Update last key states *after* render, so input events are applied
        // during frame This way inputs like IsKeyJustPressed() work as expected.
        input.ResetKeyStates();

        // Schedule next UI update with avalonia
        // Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Background);
        DispatcherTimer.Run(() => {
            RequestNextFrameRendering();
            return false; // run once
        }, TimeSpan.FromSeconds(1.0 / Fps));
    }

    protected sealed override void OnOpenGlInit(GlInterface gl)
    {
        // Bind Avalonia's GL context to OpenTK
        _avaloniaTkContext = new(gl);
        GL.LoadBindings(_avaloniaTkContext);
        GLinited = true;

        Init();
    }

    protected sealed override void OnOpenGlDeinit(GlInterface gl)
    {
        Deinit();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsEffectivelyVisible)
            return;
        
        input.SetKey(e.Key, true);
    }
    
    protected override void OnKeyUp(KeyEventArgs e)
    {
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


    public bool HitTest(Point point) => Bounds.Contains(point);
}
