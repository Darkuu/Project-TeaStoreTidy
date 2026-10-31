using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SprintController : MonoBehaviour
{
    [Header("Base Sprint Settings")]
    [SerializeField, Min(0.1f)] private float sprintStamina = 5f;
    [SerializeField, Min(0f)] private float sprintDrainRate = 1f;
    [SerializeField, Min(0f)] private float sprintRechargeRate = 1.5f;
    [SerializeField, Min(0f)] private float sprintSpeedBonus = 2f;

    [Header("Upgrade Bonuses")]
    [SerializeField, Min(0f)] private float staminaBonus;
    [SerializeField, Min(0f)] private float rechargeBonus;
    [SerializeField, Min(0f)] private float speedBonus;

    [Header("UI — Optional")]
    [Tooltip("The Slider displaying current stamina.")]
    [SerializeField] private Slider staminaBar;

    [Tooltip(
        "The UI object to fade, containing the whole stamina bar. " +
        "Leave empty to use the Slider's GameObject."
    )]
    [SerializeField] private GameObject staminaBarRoot;

    // Retains your existing Inspector setting.
    [Tooltip(
        "When enabled, show while Sprint is held or stamina is recovering. " +
        "When disabled, always show unless paused."
    )]
    [SerializeField] private bool showOnlyWhileSprintHeld = true;

    [Tooltip("Time in seconds to fade from fully hidden to fully visible.")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.15f;

    [Tooltip("Time in seconds to fade from fully visible to fully hidden.")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;

    private float currentStamina;
    private bool initialized;
    private bool paused;
    private bool sprintHeld;

    private CanvasGroup staminaCanvasGroup;
    private float targetAlpha;

    public bool IsSprinting { get; private set; }

    public float MaximumStamina =>
        Mathf.Max(0.1f, sprintStamina + staminaBonus);

    public float CurrentStamina
    {
        get
        {
            InitializeIfNeeded();
            return currentStamina;
        }
    }

    public float RechargeRate =>
        Mathf.Max(0f, sprintRechargeRate + rechargeBonus);

    public float SprintSpeedBonus =>
        Mathf.Max(0f, sprintSpeedBonus + speedBonus);

    private void Awake()
    {
        InitializeIfNeeded();
        SetupUI();
        UpdateStaminaBar();

        // Start at the correct visibility without an initial flash.
        if (staminaCanvasGroup != null)
            staminaCanvasGroup.alpha = targetAlpha;
    }

    private void OnEnable()
    {
        UpdateStaminaBar();
    }

    private void Update()
    {
        // Fade independently of movement updates so hiding still works
        // when the powers menu stops FirstPersonController.Move().
        if (staminaCanvasGroup == null)
            return;

        float duration = targetAlpha > staminaCanvasGroup.alpha
            ? fadeInDuration
            : fadeOutDuration;

        if (duration <= 0f)
        {
            staminaCanvasGroup.alpha = targetAlpha;
            return;
        }

        staminaCanvasGroup.alpha = Mathf.MoveTowards(
            staminaCanvasGroup.alpha,
            targetAlpha,
            Time.unscaledDeltaTime / duration
        );
    }

    private void OnDisable()
    {
        IsSprinting = false;
        sprintHeld = false;
        targetAlpha = 0f;

        // A disabled component cannot keep updating its fade.
        if (staminaCanvasGroup != null)
            staminaCanvasGroup.alpha = 0f;
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        currentStamina = MaximumStamina;
        initialized = true;
    }

    private void SetupUI()
    {
        if (staminaBar != null)
        {
            staminaBar.interactable = false;
            staminaBar.wholeNumbers = false;
        }

        GameObject root = staminaBarRoot;

        if (root == null && staminaBar != null)
            root = staminaBar.gameObject;

        if (root == null)
            return;

        // Fade only the UI, never the player or its parent objects.
        if (transform.IsChildOf(root.transform))
        {
            Debug.LogError(
                "Stamina Bar Root must be a separate UI object, " +
                "not the player or an ancestor of the player.",
                this
            );
            return;
        }

        staminaCanvasGroup = root.GetComponent<CanvasGroup>();

        if (staminaCanvasGroup == null)
            staminaCanvasGroup = root.AddComponent<CanvasGroup>();

        staminaCanvasGroup.interactable = false;
        staminaCanvasGroup.blocksRaycasts = false;

        // Keep the UI active; visibility is controlled through alpha.
        root.SetActive(true);
    }

    /// <summary>
    /// Called once per movement update.
    /// Updates stamina and returns the additional movement speed.
    /// </summary>
    public float Tick(
        bool isSprintHeld,
        bool hasMovementInput,
        float deltaTime)
    {
        InitializeIfNeeded();

        if (!isActiveAndEnabled || paused)
        {
            IsSprinting = false;
            sprintHeld = false;
            targetAlpha = 0f;
            return 0f;
        }

        deltaTime = Mathf.Max(0f, deltaTime);

        currentStamina = Mathf.Clamp(
            currentStamina,
            0f,
            MaximumStamina
        );

        sprintHeld = isSprintHeld;

        IsSprinting =
            sprintHeld &&
            hasMovementInput &&
            currentStamina > 0f;

        if (IsSprinting)
        {
            currentStamina = Mathf.Max(
                0f,
                currentStamina -
                Mathf.Max(0f, sprintDrainRate) * deltaTime
            );
        }
        else if (!sprintHeld)
        {
            // Release Sprint before stamina can recharge.
            currentStamina = Mathf.Min(
                MaximumStamina,
                currentStamina + RechargeRate * deltaTime
            );
        }

        UpdateStaminaBar();

        return IsSprinting ? SprintSpeedBonus : 0f;
    }

    public void SetPaused(bool isPaused)
    {
        paused = isPaused;

        if (paused)
        {
            IsSprinting = false;
            sprintHeld = false;
        }

        UpdateStaminaBar();
    }

    /// <summary>
    /// Sets total upgrade bonuses rather than adding to them.
    /// Increasing maximum stamina grants the added capacity.
    /// </summary>
    public void SetSprintUpgrades(
        float extraStamina,
        float extraRecharge,
        float extraSpeed)
    {
        InitializeIfNeeded();

        float oldMaximum = MaximumStamina;

        staminaBonus = Mathf.Max(0f, extraStamina);
        rechargeBonus = Mathf.Max(0f, extraRecharge);
        speedBonus = Mathf.Max(0f, extraSpeed);

        float addedCapacity =
            Mathf.Max(0f, MaximumStamina - oldMaximum);

        currentStamina = Mathf.Clamp(
            currentStamina + addedCapacity,
            0f,
            MaximumStamina
        );

        UpdateStaminaBar();
    }

    private void UpdateStaminaBar()
    {
        float maximum = MaximumStamina;

        if (staminaBar != null)
        {
            if (staminaBar.minValue != 0f)
                staminaBar.minValue = 0f;

            if (staminaBar.maxValue != maximum)
                staminaBar.maxValue = maximum;

            // Updating a display should not trigger gameplay callbacks.
            staminaBar.SetValueWithoutNotify(currentStamina);
        }

        bool staminaNotFull = currentStamina < maximum;

        bool visible =
            isActiveAndEnabled &&
            !paused &&
            (
                !showOnlyWhileSprintHeld ||
                sprintHeld ||
                staminaNotFull
            );

        targetAlpha = visible ? 1f : 0f;
    }
}