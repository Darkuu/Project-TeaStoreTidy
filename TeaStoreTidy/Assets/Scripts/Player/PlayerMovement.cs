using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(SprintController))]
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
    private SprintController sprintController;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float cameraPitch;
    private bool powersMenuOpen;

    public bool PowersMenuOpen => powersMenuOpen;
    public InputActionAsset InputActions => inputActions;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        sprintController = GetComponent<SprintController>();

        if (cameraTransform == null || inputActions == null)
        {
            Debug.LogError(
                "Assign the camera transform and input actions asset.",
                this
            );

            enabled = false;
            return;
        }

        // Keep this if your project contains InputBindingPersistence.
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
        sprintAction = playerMap.FindAction("Sprint", false);

        if (moveAction == null || lookAction == null)
        {
            Debug.LogError(
                "Player map must contain 'Move' and 'Look' actions.",
                this
            );

            enabled = false;
            return;
        }

        if (sprintAction == null)
        {
            Debug.LogWarning(
                "No 'Sprint' action found. Sprinting will be unavailable.",
                this
            );
        }

        RestoreCameraRotation(cameraTransform.localRotation);
    }

    private void OnEnable()
    {
        if (moveAction == null || lookAction == null)
            return;

        moveAction.Enable();
        lookAction.Enable();
        sprintAction?.Enable();

        SetPowersMenuOpen(powersMenuOpen);
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        lookAction?.Disable();
        sprintAction?.Disable();

        if (sprintController != null)
            sprintController.SetPaused(true);
    }

    private void Update()
    {
        if (powersMenuOpen)
            return;

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Look();
        Move();
    }

    public void SetPowersMenuOpen(bool isOpen)
    {
        powersMenuOpen = isOpen;

        if (isOpen)
            horizontalVelocity = Vector3.zero;

        if (sprintController != null)
            sprintController.SetPaused(isOpen);

        Cursor.lockState = isOpen
            ? CursorLockMode.None
            : CursorLockMode.Locked;

        Cursor.visible = isOpen;
    }

    // Compatibility with existing upgrade code.
    // New code can call SprintController.SetSprintUpgrades directly.
    public void SetSprintUpgrades(
        float extraStamina,
        float extraRecharge,
        float extraSpeed)
    {
        if (sprintController == null)
            sprintController = GetComponent<SprintController>();

        sprintController.SetSprintUpgrades(
            extraStamina,
            extraRecharge,
            extraSpeed
        );
    }

    private void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 lookInput =
            lookAction.ReadValue<Vector2>() * mouseSensitivity;

        transform.Rotate(Vector3.up, lookInput.x, Space.World);

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

        Vector3 direction =
            transform.right * input.x +
            transform.forward * input.y;

        bool hasInput = input.sqrMagnitude > 0.01f;
        bool sprintHeld =
            sprintAction != null && sprintAction.IsPressed();

        float sprintBonus = sprintController.Tick(
            sprintHeld,
            hasInput,
            deltaTime
        );
        

        Vector3 targetVelocity =
            direction * (moveSpeed + sprintBonus);

        float acceleration = hasInput
            ? moveAcceleration
            : moveDeceleration;

        if (!controller.isGrounded)
            acceleration *= airControl;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetVelocity,
            acceleration * deltaTime
        );

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * deltaTime;

        Vector3 velocity =
            horizontalVelocity + Vector3.up * verticalVelocity;

        CollisionFlags collisions =
            controller.Move(velocity * deltaTime);

        if ((collisions & CollisionFlags.Above) != 0 &&
            verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.DeltaAngle(0f, angle);
    }
}
