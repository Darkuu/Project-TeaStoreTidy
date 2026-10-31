using UnityEngine;
using UnityEngine.UI;

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
    [Tooltip("The fill Slider  of your stamina bar.")]
    [SerializeField] private Slider staminaBar;

    [Tooltip("The bar's parent object, containing its background and fill.")]
    [SerializeField] private GameObject staminaBarRoot;

    [Tooltip("If enabled, only show the bar while Sprint is held.")]
    [SerializeField] private bool showOnlyWhileSprintHeld = true;

    private float currentStamina;
    private bool initialized;
    private bool paused;
    private bool sprintHeld;

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
        UpdateStaminaBar();
    }

    private void OnDisable()
    {
        IsSprinting = false;
        sprintHeld = false;
        SetBarVisible(false);
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        currentStamina = MaximumStamina;
        initialized = true;
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
            SetBarVisible(false);
            return 0f;
        }

        // Also handles reducing maximum stamina in the Inspector.
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
            // Matches the original behavior:
            // release Sprint before stamina can recharge.
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
    /// Sets total upgrade bonuses, rather than adding to them.
    /// Increasing maximum stamina also grants the added capacity.
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
        if (staminaBar != null)
        {
            staminaBar.minValue = 0f;
            staminaBar.maxValue = MaximumStamina;
            staminaBar.value = currentStamina;
        }

        bool visible =
            isActiveAndEnabled &&
            !paused &&
            (!showOnlyWhileSprintHeld || sprintHeld);

        SetBarVisible(visible);
    }

    private void SetBarVisible(bool visible)
    {
        if (staminaBarRoot != null)
        {
            staminaBarRoot.SetActive(visible);
        }
        else if (staminaBar != null)
        {
            staminaBar.gameObject.SetActive(visible);
        }
    }
}