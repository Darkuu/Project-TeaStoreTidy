using UnityEngine;
using TMPro;

/// <summary>Displays how many storage spots have been completely and correctly filled.</summary>
public sealed class PlacementProgressTracker : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.25f;
    [Header("Progress Display")]
    [SerializeField] private TextMeshProUGUI progressText;

    private PlacementSpot[] placementSpots;
    private int completedSpots;
    private float nextRefreshTime;

    private void Start()
    {
        placementSpots = FindObjectsByType<PlacementSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        RefreshProgress();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        RefreshProgress();
        nextRefreshTime = Time.unscaledTime + refreshInterval;
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
