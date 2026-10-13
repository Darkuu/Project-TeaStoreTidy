using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using System.Text;

[Serializable]
public sealed class ItemProgressDisplayName
{
    public string tag;
    public string displayName;
}

[Serializable]
public sealed class SavedPowerProgress
{
    public string id;
    public int level;
    public int nextCost;
}

/// <summary>Displays how many storage spots have been completely and correctly filled.</summary>
public sealed class PlacementProgressTracker : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.25f;
    [Header("Progress Display")]
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Sorting Tracker")]
    [SerializeField] private RectTransform sortingMenuPrefab;
    [SerializeField] private Transform sortingMenuParent;
    [SerializeField] private ItemProgressDisplayName[] itemDisplayNames;
    [SerializeField] private Vector2 sortingMenuOpenPosition = new Vector2(20f, 0f);
    [SerializeField, Min(0f)] private float sortingMenuSlideDistance = 400f;
    [SerializeField, Min(0.1f)] private float sortingMenuSlideSpeed = 12f;

    [Header("Powers Menu (shared button prefab and power definitions)")]
    [SerializeField] private GameObject powersMenu;
    [SerializeField] private TextMeshProUGUI powerPointsText;
    [SerializeField] private Transform powerButtonContainer;
    [SerializeField] private PowerUpgradeButton powerButtonPrefab;
    [SerializeField] private PowerUpgradeDefinition[] availablePowers;
    [SerializeField, Min(0)] private int powerPoints;

    [SerializeField, HideInInspector] private List<SavedPowerProgress> powerProgress = new List<SavedPowerProgress>();

    private PlayerInventory playerInventory;
    private FirstPersonController playerController;
    private RectTransform sortingMenu;
    private TextMeshProUGUI sortingProgressText;
    private readonly List<PowerUpgradeButton> spawnedPowerButtons = new List<PowerUpgradeButton>();
    private readonly Dictionary<string, ItemProgressCounts> itemProgress = new Dictionary<string, ItemProgressCounts>();
    private readonly List<string> sortedItemTags = new List<string>();
    private readonly StringBuilder sortingTextBuilder = new StringBuilder();

    private PlacementSpot[] placementSpots;
    private int completedSpots;
    private float nextRefreshTime;
    private bool sortingMenuOpen;

    private sealed class ItemProgressCounts
    {
        public int total;
        public int placed;
    }

    private void Start()
    {
        playerInventory = FindFirstObjectByType<PlayerInventory>();
        playerController = FindFirstObjectByType<FirstPersonController>();

        placementSpots = FindObjectsByType<PlacementSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (powersMenu != null)
            powersMenu.SetActive(false);
        SetupSortingMenu();
        if (playerController != null)
            playerController.SetPowersMenuOpen(false);
        BuildPowerButtons();
        ApplyUnlockedPowers();
        RefreshProgress();
        UpdatePowersUI();
    }

    private void SetupSortingMenu()
    {
        if (sortingMenuPrefab == null)
            return;

        sortingMenu = Instantiate(sortingMenuPrefab, sortingMenuParent);
        sortingMenu.gameObject.name = sortingMenuPrefab.gameObject.name;
        sortingProgressText = sortingMenu.GetComponentInChildren<TextMeshProUGUI>(true);
        if (sortingProgressText == null)
            Debug.LogError("The Sorting Menu prefab needs a TextMeshProUGUI child for the item counts.", sortingMenu);

        sortingMenu.gameObject.SetActive(true);
        sortingMenu.anchoredPosition = GetSortingMenuHiddenPosition();
    }

    private void OnEnable()
    {
        PlacementSpot.Completed += OnShelfCompleted;
    }

    private void OnDisable()
    {
        PlacementSpot.Completed -= OnShelfCompleted;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            TogglePowersMenu();

        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            ToggleSortingMenu();

        AnimateSortingMenu();

        if (Time.unscaledTime < nextRefreshTime)
            return;

        RefreshProgress();
        RefreshSortingProgress();
        nextRefreshTime = Time.unscaledTime + refreshInterval;
    }

    public void TogglePowersMenu()
    {
        if (powersMenu == null)
            return;

        bool isOpen = !powersMenu.activeSelf;
        powersMenu.SetActive(isOpen);
        if (isOpen)
            sortingMenuOpen = false;
        if (playerController != null)
            playerController.SetPowersMenuOpen(isOpen);
    }

    public void ToggleSortingMenu()
    {
        if (sortingMenu == null)
            return;

        sortingMenuOpen = !sortingMenuOpen;
        if (sortingMenuOpen && powersMenu != null)
            powersMenu.SetActive(false);

        if (playerController != null)
            playerController.SetPowersMenuOpen(powersMenu != null && powersMenu.activeSelf);
    }

    private void AnimateSortingMenu()
    {
        if (sortingMenu == null)
            return;

        Vector2 target = sortingMenuOpen ? sortingMenuOpenPosition : GetSortingMenuHiddenPosition();
        float blend = 1f - Mathf.Exp(-sortingMenuSlideSpeed * Time.unscaledDeltaTime);
        sortingMenu.anchoredPosition = Vector2.Lerp(sortingMenu.anchoredPosition, target, blend);

        if ((sortingMenu.anchoredPosition - target).sqrMagnitude < 0.01f)
            sortingMenu.anchoredPosition = target;
    }

    private Vector2 GetSortingMenuHiddenPosition()
    {
        return sortingMenuOpenPosition + Vector2.left * sortingMenuSlideDistance;
    }

    public void PurchasePower(PowerUpgradeDefinition upgrade)
    {
        if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.id))
            return;

        SavedPowerProgress state = GetPowerProgress(upgrade);
        if (powerPoints < state.nextCost || (upgrade.oneTimeUnlock && state.level > 0))
            return;

        if (upgrade.effect == PowerUpgradeEffect.IncreaseInventoryCapacity)
        {
            if (playerInventory == null)
                playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (playerInventory == null)
                return;

            playerInventory.IncreaseInventoryCapacity(upgrade.amountPerPurchase);
        }
        else if (upgrade.effect == PowerUpgradeEffect.ScentTrail)
        {
            ScentTrailPower scentPower = FindFirstObjectByType<ScentTrailPower>();
            if (scentPower == null)
            {
                Debug.LogError("Add ScentTrailPower to the player before unlocking this power.", this);
                return;
            }
            scentPower.SetUnlocked(true);
        }

        powerPoints -= state.nextCost;
        state.level++;
        state.nextCost = Mathf.CeilToInt(state.nextCost * Mathf.Max(1f, upgrade.costMultiplier));
        UpdatePowersUI();
    }

    // Keeps existing scene buttons wired to the original inventory upgrade working.
    public void PurchaseInventoryUpgrade()
    {
        if (availablePowers == null)
            return;
        foreach (PowerUpgradeDefinition upgrade in availablePowers)
        {
            if (upgrade != null && upgrade.effect == PowerUpgradeEffect.IncreaseInventoryCapacity)
            {
                PurchasePower(upgrade);
                return;
            }
        }
    }

    public int PowerPoints => powerPoints;

    public List<SavedPowerProgress> GetSavedPowerProgress()
    {
        var copy = new List<SavedPowerProgress>(powerProgress.Count);
        foreach (SavedPowerProgress state in powerProgress)
            copy.Add(new SavedPowerProgress { id = state.id, level = state.level, nextCost = state.nextCost });
        return copy;
    }

    public void RestorePowerProgress(int savedPoints, List<SavedPowerProgress> savedProgress, int legacyInventoryCost = 1)
    {
        powerPoints = Mathf.Max(0, savedPoints);
        powerProgress.Clear();
        if (savedProgress != null)
        {
            foreach (SavedPowerProgress state in savedProgress)
            {
                if (state != null && !string.IsNullOrWhiteSpace(state.id))
                    powerProgress.Add(new SavedPowerProgress { id = state.id, level = Mathf.Max(0, state.level), nextCost = Mathf.Max(1, state.nextCost) });
            }
        }
        if (powerProgress.Count == 0 && availablePowers != null)
        {
            foreach (PowerUpgradeDefinition upgrade in availablePowers)
            {
                if (upgrade != null && upgrade.effect == PowerUpgradeEffect.IncreaseInventoryCapacity)
                {
                    powerProgress.Add(new SavedPowerProgress
                    {
                        id = upgrade.id,
                        level = 0,
                        nextCost = Mathf.Max(1, legacyInventoryCost)
                    });
                    break;
                }
            }
        }
        BuildPowerButtons();
        ApplyUnlockedPowers();
        UpdatePowersUI();
    }

    private void ApplyUnlockedPowers()
    {
        ScentTrailPower scentPower = FindFirstObjectByType<ScentTrailPower>();
        if (scentPower == null || availablePowers == null)
            return;

        foreach (PowerUpgradeDefinition upgrade in availablePowers)
        {
            if (upgrade == null || upgrade.effect != PowerUpgradeEffect.ScentTrail)
                continue;

            SavedPowerProgress state = GetPowerProgress(upgrade);
            scentPower.SetUnlocked(state.level > 0);
        }
    }

    private SavedPowerProgress GetPowerProgress(PowerUpgradeDefinition upgrade)
    {
        foreach (SavedPowerProgress state in powerProgress)
            if (state.id == upgrade.id)
                return state;

        var newState = new SavedPowerProgress
        {
            id = upgrade.id,
            level = 0,
            nextCost = Mathf.Max(1, upgrade.startingCost)
        };
        powerProgress.Add(newState);
        return newState;
    }

    private void BuildPowerButtons()
    {
        foreach (PowerUpgradeButton button in spawnedPowerButtons)
            if (button != null)
                Destroy(button.gameObject);
        spawnedPowerButtons.Clear();

        if (powerButtonContainer == null || powerButtonPrefab == null || availablePowers == null)
            return;

        foreach (PowerUpgradeDefinition upgrade in availablePowers)
        {
            if (upgrade == null)
                continue;

            PowerUpgradeButton button = Instantiate(powerButtonPrefab, powerButtonContainer);
            spawnedPowerButtons.Add(button);
            SavedPowerProgress state = GetPowerProgress(upgrade);
            button.Configure(upgrade, this, powerPoints, state.level, state.nextCost);
        }
    }

    private void OnShelfCompleted(PlacementSpot spot)
    {
        powerPoints++;
        UpdatePowersUI();
    }

    private void UpdatePowersUI()
    {
        if (powerPointsText != null)
            powerPointsText.text = $"Power points: {powerPoints}";
        foreach (PowerUpgradeButton button in spawnedPowerButtons)
        {
            if (button == null || button.Definition == null)
                continue;

            SavedPowerProgress state = GetPowerProgress(button.Definition);
            button.Configure(button.Definition, this, powerPoints, state.level, state.nextCost);
        }
    }

    private void RefreshProgress()
    {
        if (placementSpots == null)
            return;

        completedSpots = 0;
        foreach (PlacementSpot spot in placementSpots)
        {
            if (spot != null && spot.IsComplete)
                completedSpots++;
        }

        UpdateHudText();
    }

    private void RefreshSortingProgress()
    {
        if (sortingProgressText == null)
            return;

        itemProgress.Clear();
        sortedItemTags.Clear();

        CarryableItem[] items = FindObjectsByType<CarryableItem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (CarryableItem item in items)
        {
            if (item == null)
                continue;

            string itemTag = item.tag;
            if (!itemProgress.TryGetValue(itemTag, out ItemProgressCounts counts))
            {
                counts = new ItemProgressCounts();
                itemProgress.Add(itemTag, counts);
                sortedItemTags.Add(itemTag);
            }

            counts.total++;
            PlacementSpot spot = item.GetComponentInParent<PlacementSpot>();
            if (spot != null && item.transform.parent == spot.transform && spot.Accepts(item))
                counts.placed++;
        }

        sortedItemTags.Sort(CompareItemDisplayNames);
        sortingTextBuilder.Clear();
        sortingTextBuilder.AppendLine("Items to sort");

        if (sortedItemTags.Count == 0)
            sortingTextBuilder.Append("No items found");
        else
        {
            foreach (string itemTag in sortedItemTags)
            {
                ItemProgressCounts counts = itemProgress[itemTag];
                sortingTextBuilder.Append(counts.placed)
                    .Append('/')
                    .Append(counts.total)
                    .Append(' ')
                    .Append(GetItemDisplayName(itemTag));

                if (itemTag != sortedItemTags[sortedItemTags.Count - 1])
                    sortingTextBuilder.AppendLine();
            }
        }

        sortingProgressText.text = sortingTextBuilder.ToString();
    }

    private int CompareItemDisplayNames(string leftTag, string rightTag)
    {
        return string.Compare(GetItemDisplayName(leftTag), GetItemDisplayName(rightTag),
            StringComparison.OrdinalIgnoreCase);
    }

    private string GetItemDisplayName(string itemTag)
    {
        if (itemDisplayNames != null)
        {
            foreach (ItemProgressDisplayName entry in itemDisplayNames)
                if (entry != null && entry.tag == itemTag && !string.IsNullOrWhiteSpace(entry.displayName))
                    return entry.displayName;
        }

        return itemTag.Replace('_', ' ');
    }

    private void UpdateHudText()
    {
        if (progressText == null)
            return;

        int total = placementSpots != null ? placementSpots.Length : 0;
        progressText.text = $"Completion: {completedSpots} / {total}";

        if (total > 0 && completedSpots == total)
            progressText.text += "\nAll items sorted!";
    }
}
