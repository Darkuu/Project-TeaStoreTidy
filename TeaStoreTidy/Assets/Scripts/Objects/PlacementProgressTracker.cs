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

public enum SkillPerkEffect
{
    None,
    IncreaseInventoryCapacity,
    UnlockScentTrail,
    ScentEffectDuration,
    ScentRange,
    ScentRecoveryRate,
    ScentExtraTrail,
    SprintStamina,
    SprintRechargeRate,
    SprintSpeed
}

[Serializable]
public sealed class SkillPerk
{
    public string name = "New Perk";
    [TextArea] public string description;
    [Min(1)] public int pointsRequired = 1;
    public SkillPerkEffect effect;
    [Min(1)] public int effectAmount = 1;
    [Tooltip("Value per unlock: seconds (Scent duration/recovery, Sprint stamina), world units (Scent range/Sprint speed), targets (Extra Trail), or stamina per second (Sprint recharge).")]
    [Min(0f)] public float effectValue = 1f;
}

[Serializable]
public sealed class SkillTreeDefinition
{
    public string name = "General";
    [Tooltip("Perk 1 unlocks once. Perks 2 onward repeat in order forever.")]
    public SkillPerk[] perks;
    [Tooltip("Assign a row instance placed in the scene. The tracker updates it without creating copies.")]
    public SkillTreeProgressView progressView;
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

    [Header("Skill Progress Menu")]
    [SerializeField] private GameObject powersMenu;
    [SerializeField] private TextMeshProUGUI legacyProgressText;
    [SerializeField] private SkillTreeDefinition[] skillTrees;

    private FirstPersonController playerController;
    private RectTransform sortingMenu;
    private TextMeshProUGUI sortingProgressText;
    private readonly Dictionary<string, ItemProgressCounts> itemProgress = new Dictionary<string, ItemProgressCounts>();
    private readonly Dictionary<string, SkillProgressCounts> skillProgress =
        new Dictionary<string, SkillProgressCounts>(StringComparer.OrdinalIgnoreCase);
    private readonly List<SkillTreeProgressView> skillViews = new List<SkillTreeProgressView>();
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
        playerController = FindAnyObjectByType<FirstPersonController>();

        placementSpots = FindObjectsByType<PlacementSpot>(FindObjectsInactive.Exclude);
        if (powersMenu != null)
            powersMenu.SetActive(false);
        SetupSortingMenu();
        if (playerController != null)
            playerController.SetPowersMenuOpen(false);
        RefreshProgress();
        BuildSkillTreeViews();
        RefreshSkillProgress();
        if (legacyProgressText != null)
            legacyProgressText.gameObject.SetActive(false);
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
        RefreshSkillProgress();
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

    private void OnShelfCompleted(PlacementSpot spot)
    {
        RefreshSkillProgress();
    }

    private sealed class SkillProgressCounts
    {
        public int completed;
    }

    private void RefreshSkillProgress()
    {
        skillProgress.Clear();
        if (placementSpots != null)
        {
            foreach (PlacementSpot spot in placementSpots)
            {
                if (spot == null)
                    continue;
                string tree = spot.SkillTree;
                if (!skillProgress.ContainsKey(tree))
                {
                    skillProgress.Add(tree, new SkillProgressCounts());
                }
                if (spot.IsComplete)
                    skillProgress[tree].completed += spot.SkillPointsOnComplete;
            }
        }

        ApplyPerkEffects();
        foreach (SkillTreeProgressView view in skillViews)
        {
            if (view == null)
                continue;
            SkillTreeDefinition tree = FindTree(view.TreeName);
            if (tree == null)
                continue;
            skillProgress.TryGetValue(tree.name.Trim(), out SkillProgressCounts progress);
            GetPerkProgress(tree, progress != null ? progress.completed : 0,
                out int firstUnlocks, out int repeatUnlocks, out _);
            view.SetProgress(firstUnlocks > 0, repeatUnlocks);
        }
    }

    private void BuildSkillTreeViews()
    {
        skillViews.Clear();
        if (skillTrees == null)
            return;
        foreach (SkillTreeDefinition tree in skillTrees)
        {
            if (tree == null || tree.progressView == null)
                continue;
            SkillTreeProgressView view = tree.progressView;
            view.Configure(tree.name, tree.perks != null ? tree.perks.Length : 0);
            skillViews.Add(view);
        }
    }

    private void ApplyPerkEffects()
    {
        int inventoryBonus = 0;
        bool scentUnlocked = false;
        float scentDurationBonus = 0f;
        float scentRangeBonus = 0f;
        float scentCooldownReduction = 0f;
        int scentExtraTrails = 0;
        float sprintStaminaBonus = 0f;
        float sprintRechargeBonus = 0f;
        float sprintSpeedBonus = 0f;
        if (skillTrees != null)
        {
            foreach (SkillTreeDefinition tree in skillTrees)
            {
                if (tree == null || string.IsNullOrWhiteSpace(tree.name) || tree.perks == null || tree.perks.Length == 0)
                    continue;
                skillProgress.TryGetValue(tree.name.Trim(), out SkillProgressCounts progress);
                int points = progress != null ? progress.completed : 0;
                GetPerkProgress(tree, points, out int first, out int repeats, out _);
                for (int i = 0; i < tree.perks.Length; i++)
                {
                    SkillPerk perk = tree.perks[i];
                    if (perk == null)
                        continue;
                    int unlocked = i == 0 ? first : CountPerkUnlocks(tree, repeats, i);
                    if (perk.effect == SkillPerkEffect.IncreaseInventoryCapacity)
                        inventoryBonus += unlocked * Mathf.Max(1, perk.effectAmount);
                    else if (unlocked > 0)
                    {
                        float value = Mathf.Max(0f, perk.effectValue) * unlocked;
                        switch (perk.effect)
                        {
                            case SkillPerkEffect.UnlockScentTrail: scentUnlocked = true; break;
                            case SkillPerkEffect.ScentEffectDuration: scentDurationBonus += value; break;
                            case SkillPerkEffect.ScentRange: scentRangeBonus += value; break;
                            case SkillPerkEffect.ScentRecoveryRate: scentCooldownReduction += value; break;
                            case SkillPerkEffect.ScentExtraTrail: scentExtraTrails += Mathf.RoundToInt(value); break;
                            case SkillPerkEffect.SprintStamina: sprintStaminaBonus += value; break;
                            case SkillPerkEffect.SprintRechargeRate: sprintRechargeBonus += value; break;
                            case SkillPerkEffect.SprintSpeed: sprintSpeedBonus += value; break;
                        }
                    }
                }
            }
        }

        PlayerInventory inventory = FindAnyObjectByType<PlayerInventory>();
        if (inventory != null)
            inventory.SetSkillCapacityBonus(inventoryBonus);
        ScentTrailPower scentTrail = FindAnyObjectByType<ScentTrailPower>();
        if (scentTrail != null)
        {
            scentTrail.SetUnlocked(scentUnlocked);
            scentTrail.SetUpgrades(scentDurationBonus, scentRangeBonus, scentCooldownReduction, scentExtraTrails);
        }
        if (playerController != null)
            playerController.SetSprintUpgrades(sprintStaminaBonus, sprintRechargeBonus, sprintSpeedBonus);
    }

    private static int CountPerkUnlocks(SkillTreeDefinition tree, int repeatUnlocks, int perkIndex)
    {
        int loopLength = tree.perks.Length - 1;
        int firstOccurrence = perkIndex - 1;
        return repeatUnlocks <= firstOccurrence
            ? 0
            : 1 + (repeatUnlocks - 1 - firstOccurrence) / loopLength;
    }

    private SkillTreeDefinition FindTree(string treeName)
    {
        if (skillTrees == null)
            return null;
        foreach (SkillTreeDefinition tree in skillTrees)
            if (tree != null && !string.IsNullOrWhiteSpace(tree.name) &&
                string.Equals(tree.name.Trim(), treeName, StringComparison.OrdinalIgnoreCase))
                return tree;
        return null;
    }

    private static void GetPerkProgress(SkillTreeDefinition tree, int points, out int firstUnlocks,
        out int repeatUnlocks, out int pointsToNext)
    {
        SkillPerk first = tree.perks[0];
        int firstCost = first != null ? Mathf.Max(1, first.pointsRequired) : 1;
        firstUnlocks = points >= firstCost ? 1 : 0;
        repeatUnlocks = 0;
        pointsToNext = firstUnlocks == 0 ? firstCost - points : 0;
        if (firstUnlocks == 0 || tree.perks.Length <= 1)
            return;

        int remaining = points - firstCost;
        int index = 1;
        while (tree.perks[index] != null && remaining >= Mathf.Max(1, tree.perks[index].pointsRequired))
        {
            remaining -= Mathf.Max(1, tree.perks[index].pointsRequired);
            repeatUnlocks++;
            index = index == tree.perks.Length - 1 ? 1 : index + 1;
        }
        pointsToNext = GetPerkCost(tree.perks[index]) - remaining;
    }

    private static int GetPerkCost(SkillPerk perk) => perk != null ? Mathf.Max(1, perk.pointsRequired) : 1;

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

    /// <summary>Rebuilds derived placement, sorting, and skill progress after a save is loaded.</summary>
    public void RefreshAfterLoad()
    {
        placementSpots = FindObjectsByType<PlacementSpot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        RefreshProgress();
        RefreshSortingProgress();
        RefreshSkillProgress();
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
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
