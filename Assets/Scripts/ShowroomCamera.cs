using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Mouse-drag camera for the showroom. Outside the car, dragging with either mouse button
/// orbits around the car; inside, it pans the view around the seat without moving the eye.
/// Viewpoint changes go through <see cref="Place"/>; this component only handles the drag.
/// </summary>
public class ShowroomCamera : MonoBehaviour
{
    public enum DragMode { Orbit, Pan }

    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Orbit (outside)")]
    [SerializeField, Tooltip("Degrees per pixel of mouse drag.")]
    private float orbitSensitivity = 0.25f;
    [SerializeField, Tooltip("The orbit centre sits this far above the pivot, roughly mid-height on the body.")]
    private float pivotHeight = 0.7f;
    [SerializeField, Tooltip("Lowest and highest angle above the orbit centre the camera may reach.")]
    private float minElevation = 0f;
    [SerializeField] private float maxElevation = 70f;

    [Header("Pan (inside)")]
    [SerializeField, Tooltip("Degrees per pixel of mouse drag.")]
    private float panSensitivity = 0.15f;
    [SerializeField, Tooltip("How far the view may turn left or right of the seat's facing, like turning your head.")]
    private float maxYawFromSeat = 80f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private Vector3 eye;
    private float yaw;
    private float pitch;
    private float seatYaw;
    private bool dragging;

    public DragMode Mode { get; private set; } = DragMode.Orbit;

    /// <summary>The car's root; orbiting turns around a point <see cref="pivotHeight"/> above it.</summary>
    public Transform OrbitPivot { get; set; }

    /// <summary>Returns true when a drag starting at this screen position belongs to something else (e.g. UI).</summary>
    public Func<Vector2, bool> IsPointerBlocked { get; set; }

    /// <summary>While set, drags are ignored; used while a scripted transition owns the camera.</summary>
    public bool InputLocked { get; set; }

    public Vector3 EyePosition => eye;
    public float Yaw => yaw;
    public float Pitch => pitch;

    private void Awake()
    {
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
            else Debug.LogError("ShowroomCamera: No camera assigned or found in children.", this);
        }

        if (cameraTransform != null)
        {
            Vector3 euler = cameraTransform.eulerAngles;
            eye = cameraTransform.position;
            yaw = euler.y;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }
    }

    private void Update()
    {
        if (InputLocked)
        {
            SetDragging(false);
            return;
        }

        if (ReadDragPressed())
            SetDragging(IsPointerBlocked == null || !IsPointerBlocked(ReadPointer()));
        else if (!ReadDragHeld())
            SetDragging(false);

        if (!dragging) return;

        Vector2 delta = ReadDragDelta();
        if (delta == Vector2.zero) return;

        if (Mode == DragMode.Orbit) Orbit(delta);
        else Pan(delta);
        Apply();
    }

    /// <summary>Puts the camera at <paramref name="eyePosition"/> facing <paramref name="yaw"/> / <paramref name="pitch"/> (degrees).</summary>
    public void Place(Vector3 eyePosition, float yaw, float pitch, DragMode mode)
    {
        eye = eyePosition;
        this.yaw = yaw;
        this.pitch = pitch;
        seatYaw = yaw;
        Mode = mode;
        Apply();
    }

    private void OnDisable() => SetDragging(false);

    private void SetDragging(bool value)
    {
        // The cursor stays where it was grabbed and is hidden until the drag ends.
        if (dragging == value) return;
        dragging = value;
        Cursor.visible = !value;
    }

    private void Orbit(Vector2 delta)
    {
        Vector3 centre = OrbitCentre;
        Vector3 offset = eye - centre;

        // Horizontal drag turns the eye and its look direction together around the vertical axis.
        float turn = delta.x * orbitSensitivity;
        offset = Quaternion.AngleAxis(turn, Vector3.up) * offset;
        yaw += turn;

        // Vertical drag raises or lowers the eye around the centre, tilting the view to match.
        Vector3 flat = new Vector3(offset.x, 0f, offset.z);
        if (flat.sqrMagnitude > 1e-6f)
        {
            float elevation = Mathf.Atan2(offset.y, flat.magnitude) * Mathf.Rad2Deg;
            float target = Mathf.Clamp(elevation - delta.y * orbitSensitivity, minElevation, maxElevation);
            float tilt = target - elevation;
            Vector3 axis = Vector3.Cross(Vector3.up, flat).normalized;
            offset = Quaternion.AngleAxis(-tilt, axis) * offset;
            pitch = Mathf.Clamp(pitch + tilt, -89f, 89f);
        }

        eye = centre + offset;
    }

    private void Pan(Vector2 delta)
    {
        // Grab-style: the cabin follows the cursor, so the view turns the opposite way.
        float turn = Mathf.DeltaAngle(seatYaw, yaw) - delta.x * panSensitivity;
        yaw = seatYaw + Mathf.Clamp(turn, -maxYawFromSeat, maxYawFromSeat);
        pitch = Mathf.Clamp(pitch + delta.y * panSensitivity, minPitch, maxPitch);
    }

    private Vector3 OrbitCentre => (OrbitPivot != null ? OrbitPivot.position : Vector3.zero) + Vector3.up * pivotHeight;

    private void Apply()
    {
        if (cameraTransform != null)
            cameraTransform.SetPositionAndRotation(eye, Quaternion.Euler(pitch, yaw, 0f));
    }

    // ---------------------------------------------------------------------
    // Input abstraction (new Input System or legacy Input Manager)
    // ---------------------------------------------------------------------
#if ENABLE_INPUT_SYSTEM
    private static bool ReadDragPressed()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
    }

    private static bool ReadDragHeld()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
    }

    private static Vector2 ReadDragDelta() => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    private static Vector2 ReadPointer() => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
    private Vector2 lastPointer;

    private static bool ReadDragPressed() => Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
    private static bool ReadDragHeld() => Input.GetMouseButton(0) || Input.GetMouseButton(1);
    private static Vector2 ReadPointer() => Input.mousePosition;

    private Vector2 ReadDragDelta()
    {
        // Pixel delta to match the Input System path; reset on press so the first frame doesn't jump.
        Vector2 pointer = Input.mousePosition;
        Vector2 delta = ReadDragPressed() ? Vector2.zero : pointer - lastPointer;
        lastPointer = pointer;
        return delta;
    }
#endif
}
