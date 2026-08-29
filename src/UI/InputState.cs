// Adapted from:
// https://github.com/Marco2011T2/AvaloniaOpenTK/blob/main/AvaloniaOpenTK/OpenTK/InputManager.cs
using System.Collections;
using Avalonia;
using Avalonia.Input;

namespace terrainBench.UI;

// Gives persistent access to keyboard & mouse input state
public class InputState
{
    private readonly BitArray _keys;
    private readonly BitArray _keysPrevious;
    private readonly BitArray _mouseButtons;
    private readonly BitArray _mouseButtonsPrevious;

    private readonly object _sync = new();

    public Point MousePosition { get; private set; }
    public Point MousePositionPrevious { get; private set; }
    public Point MouseDelta { get; private set; }

    public float pressure = 1f;

    public void SetPressureFromPointer(PointerPoint p) {
        if (p.Pointer.Type == PointerType.Pen) {
            pressure = p.Properties.Pressure;
        } else {
            pressure = 1f;
        }
    }

    public void CalcMouseDelta()
    {
        var deltaX = MousePosition.X - MousePositionPrevious.X;
        var deltaY = MousePosition.Y - MousePositionPrevious.Y;
        MouseDelta = new Point(deltaX, deltaY);
    }

    public InputState()
    {
        var keyCount = (int) Key.DeadCharProcessed + 1;
        _keys = new BitArray(keyCount);
        _keysPrevious = new BitArray(keyCount);

        var mouseButtonCount = (int) MouseButton.XButton2 + 1;
        _mouseButtons = new BitArray(mouseButtonCount);
        _mouseButtonsPrevious = new BitArray(mouseButtonCount);
    }

    public void ResetKeyStates()
    {
        lock (_sync)
        {
            _keysPrevious.SetAll(false);
            _keysPrevious.Or(_keys);

            _mouseButtonsPrevious.SetAll(false);
            _mouseButtonsPrevious.Or(_mouseButtons);

            MousePositionPrevious = MousePosition;
            CalcMouseDelta();
        }
    }

    #region Keyboard

    public void SetKey(Key key, bool pressed)
    {
        lock (_sync)
        {
            _keys.Set((int) key, pressed);
        }
    }

    public bool IsKeyDown(Key key)
    {
        lock (_sync) { return _keys.Get((int) key); }
    }

    public bool WasKeyDownLastFrame(Key key)
    {
        lock (_sync) { return _keysPrevious.Get((int) key); }
    }

    public bool IsKeyJustPressed(Key key)
    {
        lock (_sync) { return _keys.Get((int) key) && !_keysPrevious.Get((int) key); }
    }

    public bool IsKeyJustReleased(Key key)
    {
        lock (_sync) { return !_keys.Get((int) key) && _keysPrevious.Get((int) key); }
    }

    public bool IsKeyHeld(Key key)
    {
        lock (_sync) { return _keys.Get((int) key) && _keysPrevious.Get((int) key); }
    }

    #endregion

    #region Mouse

    public void SetMouseButton(MouseButton button, bool pressed)
    {
        lock (_sync)
        {
            _mouseButtons.Set((int) button, pressed);
        }
    }

    public bool IsMouseButtonDown(MouseButton button)
    {
        lock (_sync) { return _mouseButtons.Get((int) button); }
    }

    public bool WasMouseButtonDownLastFrame(MouseButton button)
    {
        lock (_sync) { return _mouseButtonsPrevious.Get((int) button); }
    }

    public bool IsMouseButtonJustPressed(MouseButton button)
    {
        lock (_sync) { return _mouseButtons.Get((int) button) && !_mouseButtonsPrevious.Get((int) button); }
    }

    public bool IsMouseButtonJustReleased(MouseButton button)
    {
        lock (_sync) { return !_mouseButtons.Get((int) button) && _mouseButtonsPrevious.Get((int) button); }
    }

    public bool IsMouseButtonHeld(MouseButton button)
    {
        lock (_sync) { return _mouseButtons.Get((int) button) && _mouseButtonsPrevious.Get((int) button); }
    }

    public void SetMousePosition(Point position)
    {
        lock (_sync)
        {
            MousePositionPrevious = MousePosition;
            MousePosition = position;
        }
    }

    #endregion

    /// <summary>
    /// Clears all keys and mouse buttons. Call on LostFocus.
    /// </summary>
    public void ClearAll()
    {
        lock (_sync)
        {
            _keys.SetAll(false);
            _keysPrevious.SetAll(false);
            _mouseButtons.SetAll(false);
            _mouseButtonsPrevious.SetAll(false);
        }
    }
}
