
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

    [Header("Sprint")]
    [SerializeField, Min(0.1f)] private float sprintStamina = 5f;
    [SerializeField, Min(0f)] private float sprintDrainRate = 1f;
    [SerializeField, Min(0f)] private float sprintRechargeRate = 1.5f;
    [SerializeField, Min(0f)] private float sprintSpeedBonus = 2f;

    [Header("Look")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
    [SerializeField, Range(1f, 89f)] private float maxLookAngle = 85f;

    private CharacterController controller;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;

    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float cameraPitch;
    private bool powersMenuOpen;
    private float currentSprintStamina;
    private float staminaBonus;
    private float rechargeBonus;
    private float speedBonus;
    private Image staminaBar;

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

        cameraPitch = Mathf.Clamp(
            NormalizeAngle(cameraTransform.localEulerAngles.x),
            -maxLookAngle,
            maxLookAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
        currentSprintStamina = sprintStamina;
    }

    private void Start() => CreateStaminaBar();

    private void OnEnable()
    {
        if (moveAction == null || lookAction == null)
            return;

        moveAction.Enable();
        lookAction.Enable();
        sprintAction?.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        lookAction?.Disable();
        sprintAction?.Disable();
    }

    private void Update()
    {
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
        UpdateStaminaBar();
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
        UpdateStaminaBar();
    }

    public void SetSprintUpgrades(float extraStamina, float extraRecharge, float extraSpeed)
    {
        float oldMaximum = MaximumSprintStamina;
        staminaBonus = Mathf.Max(0f, extraStamina);
        rechargeBonus = Mathf.Max(0f, extraRecharge);
        speedBonus = Mathf.Max(0f, extraSpeed); currentSprintStamina = Mathf.Min(MaximumSprintStamina,
            currentSprintStamina + Mathf.Max(0f, MaximumSprintStamina - oldMaximum));
        UpdateStaminaBar();
    }

    private float MaximumSprintStamina => sprintStamina + staminaBonus;

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

        bool hasInput = input.sqrMagnitude > 0.01f;
        bool sprintHeld = sprintAction != null && sprintAction.IsPressed();
        bool isSprinting = sprintHeld && hasInput && currentSprintStamina > 0f;
        if (isSprinting)
            currentSprintStamina = Mathf.Max(0f, currentSprintStamina - sprintDrainRate * deltaTime);
        else if (!sprintHeld)
            currentSprintStamina = Mathf.Min(MaximumSprintStamina,
                currentSprintStamina + (sprintRechargeRate + rechargeBonus) * deltaTime);

        float currentMoveSpeed = moveSpeed + (isSprinting ? sprintSpeedBonus + speedBonus : 0f);
        Vector3 targetVelocity = direction * currentMoveSpeed;

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

    private void CreateStaminaBar()
    {
        var canvasObject = new GameObject("Sprint Stamina Canvas", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        GameObject background = CreateBarImage("Stamina Bar Background", canvasObject.transform,
            new Color(0f, 0f, 0f, 0.75f));
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0f);
        backgroundRect.pivot = new Vector2(0.5f, 0f);
        backgroundRect.anchoredPosition = new Vector2(0f, 32f);
        backgroundRect.sizeDelta = new Vector2(280f, 18f);

        GameObject fill = CreateBarImage("Stamina Bar Fill", background.transform,
            new Color(0.35f, 0.85f, 0.35f, 1f));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        staminaBar = fill.GetComponent<Image>();
        staminaBar.type = Image.Type.Filled;
        staminaBar.fillMethod = Image.FillMethod.Horizontal;
        staminaBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        background.SetActive(false);
    }

    private static GameObject CreateBarImage(string name, Transform parent, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        imageObject.GetComponent<Image>().color = color;
        return imageObject;
    }

    private void UpdateStaminaBar()
    {
        if (staminaBar == null)
            return;
        bool show = !powersMenuOpen && sprintAction != null && sprintAction.IsPressed();
        staminaBar.transform.parent.gameObject.SetActive(show);
        staminaBar.fillAmount = MaximumSprintStamina > 0f
            ? Mathf.Clamp01(currentSprintStamina / MaximumSprintStamina)
            : 0f;
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
