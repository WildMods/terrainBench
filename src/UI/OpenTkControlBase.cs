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
    private AvaloniaTkContext? _avaloniaTkContext;

    //handles the camera
    protected readonly Camera cam = new();

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

        //Update last key states AFTER render, so input events are applied during frame
        //This way inputs like IsKeyJustPressed() work as expected.
        // InputManager.ResetKeyStates();

        //Schedule next UI update with avalonia
        //Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Background);
        DispatcherTimer.Run(() =>
        {
            RequestNextFrameRendering();
            // return false; // run once
            return true;
        }, TimeSpan.FromSeconds(1.0 / Fps));
    }

    protected sealed override void OnOpenGlInit(GlInterface gl)
    {
        // Bind Avalonia context to OpenTK
        _avaloniaTkContext = new(gl);
        GL.LoadBindings(_avaloniaTkContext);

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
        
        // InputManager.SetKey(e.Key, true);
    }
    
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (!IsEffectivelyVisible)
            return;
        
        // InputManager.SetKey(e.Key, false);
    }

    public bool HitTest(Point point) => Bounds.Contains(point);
}