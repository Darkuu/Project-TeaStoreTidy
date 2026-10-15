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
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
    [SerializeField, Range(1f, 89f)] private float maxLookAngle = 89f;

    private CharacterController controller;
    private PauseMenuController pauseMenu;
    private InputAction moveAction;
    private InputAction lookAction;
    private float verticalVelocity;
    private float cameraPitch;
    private bool powersMenuOpen;
    public bool PowersMenuOpen => powersMenuOpen;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        pauseMenu = GetComponent<PauseMenuController>();

        if (cameraTransform == null || inputActions == null)
        {
            Debug.LogError("Assign the camera transform and input actions asset.", this);
            enabled = false;
            return;
        }

        InputBindingPersistence.Apply(inputActions);

        InputActionMap playerMap = inputActions.FindActionMap("Player", false);
        if (playerMap == null)
        {
            Debug.LogError("Input actions asset must contain a 'Player' map.", this);
            enabled = false;
            return;
        }

        moveAction = playerMap.FindAction("Move", false);
        lookAction = playerMap.FindAction("Look", false);
        if (moveAction == null || lookAction == null)
        {
            Debug.LogError("Player map must contain 'Move' and 'Look' actions.", this);
            enabled = false;
            return;
        }

        cameraPitch = NormalizeAngle(cameraTransform.localEulerAngles.x);
        cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);
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
        if (pauseMenu == null)
            pauseMenu = GetComponent<PauseMenuController>();

        if (pauseMenu == null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (!powersMenuOpen && (pauseMenu == null || !pauseMenu.IsOpen) &&
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
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
        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isOpen;
    }

    private void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 lookInput = lookAction.ReadValue<Vector2>() * mouseSensitivity;

        // Yaw belongs to the player; pitch belongs to the child camera.
        transform.Rotate(Vector3.up, lookInput.x, Space.Self);

        cameraPitch = Mathf.Clamp(cameraPitch - lookInput.y, -maxLookAngle, maxLookAngle);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    public void RestoreCameraRotation(Quaternion localRotation)
    {
        cameraPitch = Mathf.Clamp(
            NormalizeAngle(localRotation.eulerAngles.x),
            -maxLookAngle,
            maxLookAngle
        );
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void Move()
    {
        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        Vector3 direction = transform.right * input.x + transform.forward * input.y;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = direction * moveSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
