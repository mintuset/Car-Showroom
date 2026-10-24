using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple first person controller built on CharacterController.
/// Works on Terrain colliders and meshes. Supports both the legacy Input Manager
/// and the new Input System package (auto-detected via scripting defines).
///
/// Setup:
/// 1. Create an empty GameObject (this is the player root), add this script.
///    A CharacterController is added automatically.
/// 2. Make the camera a child of the player, position it around (0, 0.7, 0)
///    (eye height), and assign it to "Camera Transform".
/// 3. Make sure the player is above your terrain, then press Play.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField, Tooltip("How quickly horizontal velocity changes while grounded.")]
    private float groundAcceleration = 12f;
    [SerializeField, Tooltip("How quickly horizontal velocity changes in the air.")]
    private float airAcceleration = 3f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private bool lockCursorOnStart = true;
    [SerializeField, Tooltip("Keep the cursor free for on-screen UI and only look around while the right mouse button is held.")]
    private bool holdRightMouseToLook = false;

    [Header("Ground Stick")]
    [SerializeField, Tooltip("Small downward force while grounded so the controller hugs slopes.")]
    private float groundedStickForce = -2f;

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;
    private Vector3 horizontalVelocity;
    private bool walkingEnabled = true;

    /// <summary>False while parked at a fixed viewpoint (e.g. a car seat): look still works, walking and gravity do not.</summary>
    public bool WalkingEnabled => walkingEnabled;

    public Vector3 EyePosition => cameraTransform != null ? cameraTransform.position : transform.position;
    public float Yaw => transform.eulerAngles.y;
    public float Pitch => pitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
            else Debug.LogError("FirstPersonController: No camera assigned or found in children.", this);
        }
    }

    private void Start()
    {
        if (lockCursorOnStart && !holdRightMouseToLook) SetCursorLocked(true);
    }

    private void Update()
    {
        if (holdRightMouseToLook)
        {
            if (ReadLookHeld())
                HandleLook();
        }
        else
        {
            HandleCursorToggle();

            if (Cursor.lockState == CursorLockMode.Locked)
                HandleLook();
        }

        if (walkingEnabled)
            HandleMovement();
    }

    /// <summary>
    /// Moves the player so the camera ends up at <paramref name="eyePosition"/>, facing
    /// <paramref name="yaw"/> / <paramref name="pitch"/> (degrees). With <paramref name="allowWalking"/>
    /// false the CharacterController is disabled so the player can sit inside other geometry.
    /// </summary>
    public void Teleport(Vector3 eyePosition, float yaw, float pitch, bool allowWalking)
    {
        controller.enabled = false;

        Quaternion bodyRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 eyeOffset = cameraTransform != null ? bodyRotation * cameraTransform.localPosition : Vector3.zero;
        transform.SetPositionAndRotation(eyePosition - eyeOffset, bodyRotation);

        this.pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(this.pitch, 0f, 0f);

        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        walkingEnabled = allowWalking;
        controller.enabled = allowWalking;
    }

    private void HandleLook()
    {
        Vector2 look = ReadLookInput();

        // Yaw rotates the whole body, pitch only rotates the camera.
        transform.Rotate(Vector3.up, look.x * mouseSensitivity);

        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, minPitch, maxPitch);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector2 moveInput = ReadMoveInput();
        bool sprinting = ReadSprint();
        bool grounded = controller.isGrounded;

        float targetSpeed = sprinting ? sprintSpeed : walkSpeed;
        Vector3 desired = (transform.right * moveInput.x + transform.forward * moveInput.y);
        desired = Vector3.ClampMagnitude(desired, 1f) * targetSpeed;

        float accel = grounded ? groundAcceleration : airAcceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desired, accel * targetSpeed * Time.deltaTime);

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = groundedStickForce;

        if (grounded && ReadJumpPressed())
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleCursorToggle()
    {
        if (ReadEscapePressed())
            SetCursorLocked(false);
        else if (Cursor.lockState != CursorLockMode.Locked && ReadClickPressed())
            SetCursorLocked(true);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // ---------------------------------------------------------------------
    // Input abstraction (new Input System or legacy Input Manager)
    // ---------------------------------------------------------------------
#if ENABLE_INPUT_SYSTEM
    private Vector2 ReadMoveInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }

    private Vector2 ReadLookInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return Vector2.zero;

        // Mouse delta is in pixels, so scale it down to match legacy feel.
        return mouse.delta.ReadValue() * 0.05f;
    }

    private bool ReadSprint() => Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
    private bool ReadJumpPressed() => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    private bool ReadEscapePressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    private bool ReadClickPressed() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    private bool ReadLookHeld() => Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
    private Vector2 ReadMoveInput() => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    private Vector2 ReadLookInput() => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    private bool ReadSprint() => Input.GetKey(KeyCode.LeftShift);
    private bool ReadJumpPressed() => Input.GetButtonDown("Jump");
    private bool ReadEscapePressed() => Input.GetKeyDown(KeyCode.Escape);
    private bool ReadClickPressed() => Input.GetMouseButtonDown(0);
    private bool ReadLookHeld() => Input.GetMouseButton(1);
#endif

    private void Reset()
    {
        // Sensible CharacterController defaults for a human-sized player on terrain.
        CharacterController cc = GetComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.slopeLimit = 50f;
        cc.stepOffset = 0.4f;
        cc.skinWidth = 0.03f;
    }
}
