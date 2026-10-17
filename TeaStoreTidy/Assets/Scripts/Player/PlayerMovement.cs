
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 4.5f;
    [SerializeField, Min(0f)] private float moveAcceleration = 22f;
    [SerializeField, Min(0f)] private float moveDeceleration = 28f;
    [SerializeField, Range(0f, 1f)] private float airControl = 0.5f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
    [SerializeField, Range(1f, 89f)] private float maxLookAngle = 85f;

    private CharacterController controller;
    private InputAction moveAction;
    private InputAction lookAction;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float cameraPitch;
    private bool powersMenuOpen;

    public bool PowersMenuOpen => powersMenuOpen;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null || inputActions == null)
        {
            Debug.LogError(
                "Assign the camera transform and input actions asset.",
                this
            );

            enabled = false;
            return;
        }

        InputBindingPersistence.Apply(inputActions);

        InputActionMap playerMap =
            inputActions.FindActionMap("Player", false);

        if (playerMap == null)
        {
            Debug.LogError(
                "Input actions asset must contain a 'Player' map.",
                this
            );

            enabled = false;
            return;
        }

        moveAction = playerMap.FindAction("Move", false);
        lookAction = playerMap.FindAction("Look", false);

        if (moveAction == null || lookAction == null)
        {
            Debug.LogError(
                "Player map must contain 'Move' and 'Look' actions.",
                this
            );

            enabled = false;
            return;
        }

        cameraPitch = Mathf.Clamp(
            NormalizeAngle(cameraTransform.localEulerAngles.x),
            -maxLookAngle,
            maxLookAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void OnEnable()
    {
        if (moveAction == null || lookAction == null)
            return;

        moveAction.Enable();
        lookAction.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        lookAction?.Disable();
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (!powersMenuOpen &&
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (powersMenuOpen)
            return;

        Look();
        Move();
    }

    public void SetPowersMenuOpen(bool isOpen)
    {
        powersMenuOpen = isOpen;

        if (isOpen)
            horizontalVelocity = Vector3.zero;

        Cursor.lockState = isOpen
            ? CursorLockMode.None
            : CursorLockMode.Locked;

        Cursor.visible = isOpen;
    }

    private void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 lookInput =
            lookAction.ReadValue<Vector2>() * mouseSensitivity;

        // Rotate the player horizontally.
        transform.Rotate(
            Vector3.up,
            lookInput.x,
            Space.World
        );

        // Rotate the camera vertically.
        cameraPitch = Mathf.Clamp(
            cameraPitch - lookInput.y,
            -maxLookAngle,
            maxLookAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    public void RestoreCameraRotation(Quaternion localRotation)
    {
        cameraPitch = Mathf.Clamp(
            NormalizeAngle(localRotation.eulerAngles.x),
            -maxLookAngle,
            maxLookAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void Move()
    {
        float deltaTime = Time.deltaTime;

        Vector2 input = Vector2.ClampMagnitude(
            moveAction.ReadValue<Vector2>(),
            1f
        );

        // Convert movement input into world-space direction.
        Vector3 direction =
            transform.right * input.x +
            transform.forward * input.y;

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        Vector3 targetVelocity = direction * moveSpeed;

        // Accelerate while moving and decelerate when stopping.
        bool hasInput = input.sqrMagnitude > 0.01f;

        float acceleration = hasInput
            ? moveAcceleration
            : moveDeceleration;

        // Reduce acceleration control while airborne.
        if (!controller.isGrounded)
            acceleration *= airControl;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetVelocity,
            acceleration * deltaTime
        );

        // Keep the character grounded without building up
        // excessive downward velocity.
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * deltaTime;
        }

        Vector3 velocity =
            horizontalVelocity +
            Vector3.up * verticalVelocity;

        controller.Move(velocity * deltaTime);
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
