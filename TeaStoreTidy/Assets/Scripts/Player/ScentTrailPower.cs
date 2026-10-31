using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Shows a temporary path to nearby unplaced objects matching the held item's tag.</summary>
public sealed class ScentTrailPower : MonoBehaviour
{
    [Header("Ability Display")]
    [SerializeField] private GameObject abilityPanel;
    [SerializeField] private TMP_Text abilityKeyText;
    [Tooltip("A dark Filled Image layered over the ability icon. It drains from top to bottom during cooldown.")]
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private string actionName = "ScentTrail";

    [Header("Scent Trail")]
    [SerializeField] private ParticleSystem scentFogPrefab;
    [Tooltip("Offset in camera-local space. A negative Y starts the scent below eye level.")]
    [SerializeField] private Vector3 trailStartOffset = new Vector3(0f, -0.25f, 0f);
    [SerializeField, Min(0.01f)] private float revealDuration = 0.8f;
    [SerializeField, Min(1f)] private float activeDuration = 10f;
    [SerializeField, Min(0f)] private float cooldownDuration = 30f;
    [SerializeField, Min(0.1f)] private float searchRadius = 35f;
    [SerializeField] private bool unlocked;

    private PlayerInventory inventory;
    private FirstPersonController playerController;
    private InputAction scentAction;
    private float nextUseTime;
    private float durationBonus;
    private float rangeBonus;
    private float cooldownReduction;
    private int extraTrails;
    private float EffectiveCooldown => Mathf.Max(0.1f, cooldownDuration - cooldownReduction);

    private void Awake()
    {
        CachePlayerComponents();
        if (abilityPanel != null)
            abilityPanel.SetActive(unlocked);
        ResolveAction();
        UpdateAbilityDisplay();
    }

    private void OnEnable()
    {
        if (unlocked)
            EnableAction();
    }

    private void OnDisable()
    {
        DisableAction();
    }

    private void Update() => UpdateCooldownOverlay();

    public void SetUnlocked(bool value)
    {
        if (unlocked == value)
            return;
        unlocked = value;
        if (abilityPanel != null)
            abilityPanel.SetActive(unlocked);

        if (unlocked)
            EnableAction();
        else
            DisableAction();

        UpdateAbilityDisplay();
    }

    public void SetUpgrades(float duration, float range, float cooldown, int trails)
    {
        durationBonus = Mathf.Max(0f, duration);
        rangeBonus = Mathf.Max(0f, range);
        cooldownReduction = Mathf.Max(0f, cooldown);
        extraTrails = Mathf.Max(0, trails);
        UpdateCooldownOverlay();
    }

    private void ResolveAction()
    {
        CachePlayerComponents();
        FirstPersonController controller = GetComponent<FirstPersonController>();
        if (controller == null)
            controller = playerController;
        InputActionAsset actions = controller != null ? controller.InputActions : null;
        if (actions == null && inventory != null)
            actions = inventory.InputActions;

        scentAction = null;
        if (actions == null)
            return;

        // Support renamed action display values while still requiring the
        // scent binding's stable ID when the configured name does not match.
        InputActionMap playerMap = actions.FindActionMap("Player", false);
        scentAction = playerMap != null ? playerMap.FindAction(actionName, false) : null;
        if (scentAction == null)
            scentAction = actions.FindAction("a57b676a-eaad-44d5-aa83-1c581c19a3d9", false);
        if (scentAction == null)
            scentAction = actions.FindAction("Player/ScentTrail", false);
    }

    private void CachePlayerComponents()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();
        if (playerController == null)
            playerController = GetComponentInParent<FirstPersonController>();
    }

    private void EnableAction()
    {
        ResolveAction();
        if (scentAction == null)
        {
            Debug.LogError($"Input Actions must contain Player/{actionName} for the scent ability.", this);
            return;
        }

        scentAction.performed -= OnScentAction;
        scentAction.performed += OnScentAction;
        scentAction.Enable();
    }

    private void DisableAction()
    {
        if (scentAction == null)
            return;
        scentAction.performed -= OnScentAction;
        scentAction.Disable();
    }

    private void OnScentAction(InputAction.CallbackContext context)
    {
        if (playerController != null && playerController.PowersMenuOpen)
            return;
        TryUsePower();
    }

    public bool TryUsePower()
    {
        if (!unlocked || Time.time < nextUseTime)
            return false;
        if (scentFogPrefab == null)
        {
            Debug.LogWarning("Assign a Particle System prefab to Scent Fog Prefab on ScentTrailPower.", this);
            return false;
        }

        CarryableItem heldItem = inventory != null ? inventory.SelectedItem : null;
        if (heldItem == null)
        {
            SetTemporaryMessage("Hold an item first");
            return false;
        }

        Transform cameraTransform = inventory.CameraTransform;
        Vector3 startPosition = cameraTransform != null
            ? cameraTransform.TransformPoint(trailStartOffset)
            : transform.position + Vector3.down * Mathf.Abs(trailStartOffset.y);
        CarryableItem[] allItems = FindObjectsByType<CarryableItem>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var candidates = new System.Collections.Generic.List<CarryableItem>();

        foreach (CarryableItem item in allItems)
        {
            if (item == null || item == heldItem || inventory.IsCarrying(item) || !item.CompareTag(heldItem.tag))
                continue;
            if (item.GetComponentInParent<PlacementSpot>() != null)
                continue;
            if (Vector3.Distance(startPosition, item.transform.position) > searchRadius + rangeBonus)
                continue;
            candidates.Add(item);
        }

        if (candidates.Count == 0)
        {
            SetTemporaryMessage("No matching items nearby");
            return false;
        }

        candidates.Sort((left, right) =>
            (left.transform.position - startPosition).sqrMagnitude.CompareTo(
                (right.transform.position - startPosition).sqrMagnitude));
        int trailCount = Mathf.Min(candidates.Count, 1 + extraTrails);
        for (int i = 0; i < trailCount; i++)
            StartCoroutine(AnimateFogTrail(startPosition, candidates[i].transform.position));

        nextUseTime = Time.time + EffectiveCooldown;
        UpdateAbilityDisplay();
        return true;
    }

    private System.Collections.IEnumerator AnimateFogTrail(Vector3 startPosition, Vector3 targetPosition)
    {
        ParticleSystem fog = Instantiate(scentFogPrefab, startPosition, Quaternion.identity);
        fog.Play();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, revealDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float smoothProgress = progress * progress * (3f - 2f * progress);
            fog.transform.position = Vector3.Lerp(startPosition, targetPosition, smoothProgress);
            yield return null;
        }

        // Leave the fog emitter at the destination for the rest of the reveal window.
        fog.transform.position = targetPosition;
        yield return new WaitForSeconds(activeDuration + durationBonus);
        if (fog != null)
            Destroy(fog.gameObject);
    }

    private void SetTemporaryMessage(string message)
    {
        if (abilityKeyText == null)
            return;
        abilityKeyText.text = message;
        CancelInvoke(nameof(UpdateAbilityDisplay));
        Invoke(nameof(UpdateAbilityDisplay), 2f);
    }

    private void UpdateAbilityDisplay()
    {
        string keyName = scentAction != null ? scentAction.GetBindingDisplayString() : actionName;
        if (string.IsNullOrEmpty(keyName))
            keyName = actionName;

        if (abilityKeyText != null && unlocked)
            abilityKeyText.text = keyName;

        UpdateCooldownOverlay();
    }

    private void UpdateCooldownOverlay()
    {
        if (cooldownOverlay == null)
            return;

        float remaining = Mathf.Max(0f, nextUseTime - Time.time);
        bool isCoolingDown = unlocked && remaining > 0f;
        float cooldownProgress = isCoolingDown && EffectiveCooldown > 0f
            ? 1f - Mathf.Clamp01(remaining / EffectiveCooldown)
            : 0f;
        cooldownOverlay.type = Image.Type.Filled;
        cooldownOverlay.fillMethod = Image.FillMethod.Vertical;
        cooldownOverlay.fillOrigin = (int)Image.OriginVertical.Top;
        cooldownOverlay.fillAmount = cooldownProgress;
        cooldownOverlay.enabled = isCoolingDown;
    }
}
