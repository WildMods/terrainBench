// Created Jul. 16 2026, copied from RenderTron 9000 C++ class
// @author Torphedo

using ImGuiNET;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using terrainBench.UI;
using static OpenTK.Mathematics.MathHelper;

namespace terrainBench;

public class Camera {
    public enum Mode {
        ORBIT, // 3rd-person dual-stick style
        MINECRAFT, // POV Minecraft-style
        FLY, // Flying (freecam style, like Source Engine spectator)
        MODE_ENUM_MAX,
    };

    static Vector3 camera_up = new Vector3(0, 1, 0);

    Vector3 pos = new Vector3(0, 200, 0); // Position of the viewer
    Quaternion orbit_angles = Quaternion.Identity;
    public float radius = 30.0f;
    public float move_speed = 500.0f;
    public float mouse_sens = 0.05f;
    private float zoom_sens = 10.0f;

    // Projection settings
    public float fov_angle = DegreesToRadians(70.0f);

    public float fov_degrees {
        get => RadiansToDegrees(fov_angle);
        set =>  fov_angle = DegreesToRadians(value);
    }
    public float aspect = 16f / 9f;
    public float near = 0.1f;
    public float far = 10000.0f;

    public bool invert_mouse_x = true;
    public bool invert_mouse_y = false;

    public Mode mode { get;
        set {
            if (value == mode) {
                return; // Nothing to do.
            }

            if (value == Mode.ORBIT) {
                mouse_sens *= (float)Math.PI;
            } else if (mode == Mode.ORBIT) {
                mouse_sens /= (float)Math.PI;
            }
        
            // If entering or leaving orbit mode, the target will be swapped with the
            // camera. We need to face the opposite direction to correct for the change
            bool needs_view_flip = (mode == Mode.ORBIT || value == Mode.ORBIT);

            var flip_rot = new Quaternion(new Vector3(DegreesToRadians(180.0f), DegreesToRadians(180.0f), 0));
            if (needs_view_flip) {
                orbit_angles *= flip_rot;
            }
        
            // Set mode
            field = value;
        }
    } = Mode.ORBIT;

    public bool ImGuiEdit() {
        bool dirty = false;

        Mode[] modeOptions = {Mode.ORBIT, Mode.MINECRAFT, Mode.FLY };
        if (ImGui.BeginCombo("Mode", mode.ToString())) {
            foreach (var m in modeOptions) {
                if (ImGui.Selectable(m.ToString())) {
                    mode = m;
                }
                if (mode == m) {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }
        
        
        dirty |= ImGui.SliderFloat("Move Speed", ref move_speed, 0f, 500f);
        var fov_temp = fov_degrees;
        if (ImGui.SliderFloat("FOV", ref fov_temp, 10f, 120f)) {
            fov_degrees = fov_temp;
            dirty = true;
        }
        dirty |= ImGui.SliderFloat("Mouse sensitivity", ref mouse_sens, 0f, 0.5f);
        dirty |= ImGui.SliderFloat("Zoom sensitivity", ref zoom_sens, 0.1f, 50f);
        dirty |= ImGui.Checkbox("Invert X", ref invert_mouse_x);
        dirty |= ImGui.Checkbox("Invert Y", ref invert_mouse_y);

        return dirty;
    }

    /// @brief Update the camera state (should be called each frame)
    /// @param The camera to modify
    /// @param delta_time Time elapsed since the last call
    public void update(KeyboardState input, MouseState mouse, double delta_time) {
        if (!input.WasKeyDown(Keys.M) && input.IsKeyDown(Keys.M)) {
            mode = (Mode)(((int)mode + 1) % (int)Mode.MODE_ENUM_MAX);
        }

        Vector2 cursor_delta = get_cursor_delta(mouse);
        // Vector2 scroll_delta = mouse.ScrollDelta;
        Vector2 scroll_delta = new();

        // Save state so we can find the delta next time we're called

        float multiplier = (float)delta_time * move_speed;

        float forward  = multiplier * ((input.IsKeyDown(Keys.W) ? 1f : 0f) - (input.IsKeyDown(Keys.S) ? 1f : 0f));
        float side     = multiplier * ((input.IsKeyDown(Keys.A) ? 1f : 0f) - (input.IsKeyDown(Keys.D) ? 1f : 0f));
        float vertical = multiplier * ((input.IsKeyDown(Keys.Space) ? 1f : 0f) - (input.IsKeyDown(Keys.LeftShift) ? 1f : 0f));

        Vector3 cam_dir = facing();

        // Horizontal plane of camera dir, so vertical angle doesn't affect horizontal movement
        Vector3 horizontal = new Vector3(cam_dir.X, 0, cam_dir.Z).Normalized();
        Vector3 cam_side = right();
        // Make forward/back move along camera vector in fly mode
        if (mode == Mode.FLY) {
            horizontal = cam_dir;
            vertical = 0; // Ignore the normal vertical movement keys
        }

        Vector3 pos_delta = (horizontal * forward) + (cam_side * side);
        pos_delta.Y += vertical;

        // Update target pos
        pos += pos_delta;

        // Rotate along side axis for pitch
        orbit_angles = Quaternion.FromAxisAngle(cam_side, cursor_delta.Y) * orbit_angles;
        orbit_angles = Quaternion.FromAxisAngle(camera_up, cursor_delta.X) * orbit_angles;

        radius -= scroll_delta.Y * zoom_sens;
        radius = Math.Clamp(radius, 0.05f, 8192.0f); // Don't allow <= 0 or really high zoom
    }

    /// @brief Gets the unit direction vector the camera is looking
    public Vector3 facing() {
        Vector3 vec = orbit_pos_by_angles().Normalized();
        float multiplier = (mode == Mode.ORBIT) ? -1 : 1;
        return vec * multiplier;
    }

    /// @brief Unit vector pointing to the right from the user's perspective
    public Vector3 right() {
        return Vector3.Cross(camera_up, facing()).Normalized();
    }

    /// @brief Get the position of the eye as seen by the user
    public Vector3 eye() {
        Vector3 position = pos;
        Vector3 view_target = pos + orbit_pos_by_angles();
        if (mode == Mode.ORBIT) {
            Swap(ref position, ref view_target);
        }

        return position;
    }

    // Get just the camera transform
    public Matrix4 view_matrix() {
        Vector3 position = pos;
        Vector3 view_target = pos + orbit_pos_by_angles();
        if (mode == Mode.ORBIT) {
            Swap(ref position, ref view_target);
        }

        return Matrix4.LookAt(position, view_target, camera_up);
    }

    // Get just the projection transform
    public Matrix4 proj_matrix() {
        var projection = Matrix4.CreatePerspectiveFieldOfView(fov_angle, aspect, near, far);
        return projection;
    }

    // Get combined projection & view matrix for the current camera position
    public Matrix4 proj_view() {
        return proj_matrix() * view_matrix();
    }

    /// @brief Get camera position relative to the orbit center point
    private Vector3 orbit_pos_by_angles() {
        // Apply rotation to unit vector scaled by radius
        return orbit_angles * (new Vector3(1, 0, 0) * radius);
    }

    /// @brief Screenspace cursor movement since last frame.
    ///
    /// This also applies mouse inversion if needed, and the gamepad's right stick.
    private Vector2 get_cursor_delta(MouseState mouse) {
        // Nullify movement unless click is held
        if (!mouse.IsButtonDown(MouseButton.Middle)) {
            return Vector2.Zero;
        }

        var delta = mouse.Delta;
        Vector2 cursor_delta = mouse_sens * new Vector2((float)delta.X, (float)delta.Y);

        // Invert sign as needed.
        if (invert_mouse_x) {
            cursor_delta.X = -cursor_delta.X;
        }
        if (invert_mouse_y) {
            cursor_delta.Y = -cursor_delta.Y;
        }

        return cursor_delta;
    }
};
