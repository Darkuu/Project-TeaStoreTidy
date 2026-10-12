using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reusable view for one power upgrade row/button.</summary>
public sealed class PowerUpgradeButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    private PowerUpgradeDefinition definition;
    private PlacementProgressTracker tracker;
    private bool listenerAdded;
    public PowerUpgradeDefinition Definition => definition;

    private void Awake()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);
        if (button != null && !listenerAdded)
        {
            button.onClick.AddListener(Purchase);
            listenerAdded = true;
        }
    }

    public void Configure(PowerUpgradeDefinition upgrade, PlacementProgressTracker progressTracker,
        int points, int level, int cost)
    {
        EnsureReferences();
        definition = upgrade;
        tracker = progressTracker;

        bool alreadyUnlocked = upgrade.oneTimeUnlock && level > 0;
        if (label != null)
            label.text = alreadyUnlocked
                ? $"{upgrade.displayName}\nUNLOCKED"
                : $"{upgrade.displayName}\nLevel {level} - {cost} point{(cost == 1 ? "" : "s")}";
        if (button != null)
            button.interactable = !alreadyUnlocked && points >= cost;
    }

    private void Purchase()
    {
        if (tracker != null && definition != null)
            tracker.PurchasePower(definition);
    }
}
