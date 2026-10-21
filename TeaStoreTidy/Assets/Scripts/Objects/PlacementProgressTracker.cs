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
    [SerializeField] private TextMeshProUGUI skillProgressText;
    [SerializeField] private Transform legacyPurchaseButtons;
    [SerializeField] private SkillTreeDefinition[] skillTrees;

    private FirstPersonController playerController;
    private RectTransform sortingMenu;
    private TextMeshProUGUI sortingProgressText;
    private readonly Dictionary<string, ItemProgressCounts> itemProgress = new Dictionary<string, ItemProgressCounts>();
    private readonly Dictionary<string, SkillProgressCounts> skillProgress =
        new Dictionary<string, SkillProgressCounts>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> skillNames = new List<string>();
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
        playerController = FindFirstObjectByType<FirstPersonController>();

        placementSpots = FindObjectsByType<PlacementSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (powersMenu != null)
            powersMenu.SetActive(false);
        SetupSortingMenu();
        if (playerController != null)
            playerController.SetPowersMenuOpen(false);
        RefreshProgress();
        RefreshSkillProgress();
        if (legacyPurchaseButtons != null)
            legacyPurchaseButtons.gameObject.SetActive(false);
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
        public int total;
    }

    private void RefreshSkillProgress()
    {
        skillProgress.Clear();
        skillNames.Clear();
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
                    skillNames.Add(tree);
                }
                if (spot.IsComplete)
                    skillProgress[tree].completed += spot.SkillPointsOnComplete;
                skillProgress[tree].total++;
            }
        }

        ApplyPerkEffects();
        if (skillProgressText == null)
            return;

        skillNames.Sort(StringComparer.OrdinalIgnoreCase);
        sortingTextBuilder.Clear();
        sortingTextBuilder.AppendLine("Skill Progress");
        if (skillNames.Count == 0)
            sortingTextBuilder.Append("No skill trees assigned");
        else
            foreach (string tree in skillNames)
            {
                SkillProgressCounts progress = skillProgress[tree];
                sortingTextBuilder.Append(tree).Append(": ").Append(progress.completed).AppendLine(" points");
                AppendTreePerks(tree, progress.completed);
            }
        skillProgressText.text = sortingTextBuilder.ToString().TrimEnd();
    }

    private void AppendTreePerks(string treeName, int points)
    {
        SkillTreeDefinition tree = FindTree(treeName);
        if (tree == null || tree.perks == null || tree.perks.Length == 0)
        {
            sortingTextBuilder.AppendLine("  No perks configured");
            return;
        }

        GetPerkProgress(tree, points, out int firstUnlocks, out int repeatUnlocks, out int pointsToNext);
        if (firstUnlocks == 0)
        {
            sortingTextBuilder.Append("  Next: ").Append(tree.perks[0] != null ? tree.perks[0].name : "(empty perk)")
                .Append(" (").Append(pointsToNext).AppendLine(" points)");
            return;
        }

        sortingTextBuilder.Append("  ").Append(tree.perks[0] != null ? tree.perks[0].name : "(empty perk)")
            .AppendLine(" [unlocked]");
        if (tree.perks.Length == 1)
        {
            sortingTextBuilder.AppendLine("  First perk is the only perk; no repeat loop configured");
            return;
        }

        int nextIndex = 1 + (repeatUnlocks % (tree.perks.Length - 1));
        sortingTextBuilder.Append("  Loop: ");
        for (int i = 1; i < tree.perks.Length; i++)
        {
            SkillPerk perk = tree.perks[i];
            int unlocked = CountPerkUnlocks(tree, repeatUnlocks, i);
            sortingTextBuilder.Append(perk != null ? perk.name : "(empty perk)");
            if (i == nextIndex)
                sortingTextBuilder.Append(" [next: ").Append(pointsToNext).Append(" points]");
            else if (unlocked > 0)
                sortingTextBuilder.Append(" [unlocked x").Append(unlocked).Append(']');
            if (i < tree.perks.Length - 1)
                sortingTextBuilder.Append(" -> ");
        }
        sortingTextBuilder.AppendLine();
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

        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        if (inventory != null)
            inventory.SetSkillCapacityBonus(inventoryBonus);
        ScentTrailPower scentTrail = FindFirstObjectByType<ScentTrailPower>();
        if (scentTrail != null)
        {
            scentTrail.SetUnlocked(scentUnlocked);
            scentTrail.SetUpgrades(scentDurationBonus, scentRangeBonus, scentCooldownReduction, scentExtraTrails);
        }
        if (playerController != null)
            playerController.SetSprintUpgrades(sprintStaminaBonus, sprintRechargeBonus, sprintSpeedBonus);
    }

    private int CountPerkUnlocks(SkillTreeDefinition tree, int repeatUnlocks, int perkIndex)
    {
        if (tree.perks.Length <= 1 || perkIndex <= 0)
            return 0;
        int firstOccurrence = perkIndex - 1;
        return repeatUnlocks <= firstOccurrence
            ? 0
            : 1 + (repeatUnlocks - 1 - firstOccurrence) / (tree.perks.Length - 1);
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
