using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Shows a temporary path to nearby unplaced objects matching the held item's tag.</summary>
public sealed class ScentTrailPower : MonoBehaviour
{
    [Header("Ability Display")]
    [SerializeField] private GameObject abilityPanel;
    [SerializeField] private TMP_Text abilityKeyText;
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

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        playerController = GetComponent<FirstPersonController>();
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

    private void Update()
    {
        if (unlocked && Time.time < nextUseTime)
            UpdateAbilityDisplay();
    }

    public void SetUnlocked(bool value)
    {
        unlocked = value;
        if (abilityPanel != null)
            abilityPanel.SetActive(unlocked);

        if (unlocked)
            EnableAction();
        else
            DisableAction();

        UpdateAbilityDisplay();
    }

    private void ResolveAction()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();
        InputActionAsset actions = inventory != null ? inventory.InputActions : null;
        InputActionMap playerMap = actions != null ? actions.FindActionMap("Player", false) : null;
        scentAction = playerMap != null ? playerMap.FindAction(actionName, false) : null;
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
        int trailCount = 0;

        foreach (CarryableItem item in allItems)
        {
            if (item == null || item == heldItem || inventory.IsCarrying(item) || !item.CompareTag(heldItem.tag))
                continue;
            if (item.GetComponentInParent<PlacementSpot>() != null)
                continue;
            if (Vector3.Distance(startPosition, item.transform.position) > searchRadius)
                continue;

            Vector3 targetPosition = item.transform.position;
            StartCoroutine(AnimateFogTrail(startPosition, targetPosition));
            trailCount++;
        }

        if (trailCount == 0)
        {
            SetTemporaryMessage("No matching items nearby");
            return false;
        }

        nextUseTime = Time.time + cooldownDuration;
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
        yield return new WaitForSeconds(activeDuration);
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
        if (abilityKeyText == null || !unlocked)
            return;

        string keyName = scentAction != null ? scentAction.GetBindingDisplayString() : actionName;
        if (string.IsNullOrEmpty(keyName))
            keyName = actionName;

        int seconds = Mathf.CeilToInt(nextUseTime - Time.time);
        abilityKeyText.text = seconds > 0 ? $"{keyName} - {seconds}s" : keyName;
    }
}
