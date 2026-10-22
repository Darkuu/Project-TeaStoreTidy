using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays one authored skill tree row using inspector-assigned dot Images.</summary>
public sealed class SkillTreeProgressView : MonoBehaviour
{
    [SerializeField] private TMP_Text header;
    [SerializeField] private Image[] stageDots;
    [SerializeField] private Color unlockedColor = new Color(0.35f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color currentColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.25f, 0.3f, 0.4f, 1f);

    private string treeName;
    private int stageCount;

    public string TreeName => treeName;

    public void Configure(string name, int stages)
    {
        treeName = name;
        stageCount = Mathf.Max(0, stages);
        if (stageDots == null)
            stageDots = new Image[0];
        if (stageDots.Length != stageCount)
            Debug.LogWarning($"Skill tree '{treeName}' has {stageCount} perks but its progress prefab has {stageDots.Length} dots.", this);
        SetProgress(false, 0);
    }

    public void SetProgress(bool firstPerkUnlocked, int repeatUnlocks)
    {
        int visibleStages = Mathf.Min(stageCount, stageDots != null ? stageDots.Length : 0);
        int repeatStages = Mathf.Max(0, stageCount - 1);
        int loops = repeatStages > 0 ? repeatUnlocks / repeatStages : 0;
        int currentRepeatStage = repeatStages > 0 ? repeatUnlocks % repeatStages : 0;
        if (header != null)
            header.text = $"{treeName}     Loops: {loops}";

        for (int i = 0; i < stageDots.Length; i++)
        {
            Image dot = stageDots[i];
            if (dot == null)
                continue;
            dot.gameObject.SetActive(i < visibleStages);
            if (i >= visibleStages)
                continue;

            if (i == 0)
                dot.color = firstPerkUnlocked ? unlockedColor : currentColor;
            else if (firstPerkUnlocked && i - 1 < currentRepeatStage)
                dot.color = unlockedColor;
            else if (firstPerkUnlocked && i - 1 == currentRepeatStage)
                dot.color = currentColor;
            else
                dot.color = lockedColor;
        }
    }
}
