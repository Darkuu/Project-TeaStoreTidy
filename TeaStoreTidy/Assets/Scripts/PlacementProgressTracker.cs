using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays how many storage spots have been completely and correctly filled.</summary>
public sealed class PlacementProgressTracker : MonoBehaviour
{
    [SerializeField] private Vector2 screenPosition = new Vector2(16f, 16f);
    [SerializeField] private Vector2 panelSize = new Vector2(260f, 72f);
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.25f;

    private PlacementSpot[] placementSpots;
    private int completedSpots;
    private float nextRefreshTime;
    private Canvas progressCanvas;
    private Image progressPanel;
    private Text progressText;
    private bool ownsCanvas;

    public void ConfigureUI(Canvas canvas, Image panel, Text text)
    {
        progressCanvas = canvas;
        progressPanel = panel;
        progressText = text;
    }

    private void Start()
    {
        placementSpots = FindObjectsByType<PlacementSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        CreateHud();
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

    private void CreateHud()
    {
        if (progressCanvas == null && progressPanel != null)
            progressCanvas = progressPanel.GetComponentInParent<Canvas>();

        if (progressText != null)
        {
            if (progressCanvas == null)
                progressCanvas = progressText.GetComponentInParent<Canvas>();
            return;
        }

        if (progressCanvas == null)
        {
            GameObject canvasObject = new GameObject("Placement Progress HUD");
            ownsCanvas = true;
            progressCanvas = canvasObject.AddComponent<Canvas>();
            progressCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            progressCanvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (progressPanel == null)
        {
            GameObject panelObject = new GameObject("Progress Panel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(progressCanvas.transform, false);
            progressPanel = panelObject.GetComponent<Image>();
            RectTransform panelTransform = panelObject.GetComponent<RectTransform>();
            panelTransform.anchorMin = new Vector2(0f, 1f);
            panelTransform.anchorMax = new Vector2(0f, 1f);
            panelTransform.pivot = new Vector2(0f, 1f);
            panelTransform.anchoredPosition = new Vector2(screenPosition.x, -screenPosition.y);
            panelTransform.sizeDelta = panelSize;
            progressPanel.color = new Color(0f, 0f, 0f, 0.72f);
        }

        GameObject textObject = new GameObject("Progress Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(progressPanel.transform, false);
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = new Vector2(12f, 8f);
        textTransform.offsetMax = new Vector2(-12f, -8f);

        progressText = textObject.GetComponent<Text>();
        progressText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (progressText.font == null)
            progressText.font = Font.CreateDynamicFontFromOSFont("Arial", 22);
        progressText.fontSize = 22;
        progressText.color = Color.white;
        progressText.alignment = TextAnchor.MiddleLeft;
        progressText.horizontalOverflow = HorizontalWrapMode.Wrap;
        progressText.verticalOverflow = VerticalWrapMode.Overflow;
        progressText.raycastTarget = false;
    }

    private void UpdateHudText()
    {
        if (progressText == null)
            return;

        int total = placementSpots != null ? placementSpots.Length : 0;
        progressText.text = $"Storage areas: {completedSpots} / {total}";

        if (total > 0 && completedSpots == total)
            progressText.text += "\nAll storage areas complete!";
    }

    private void OnDestroy()
    {
        if (ownsCanvas && progressCanvas != null)
            Destroy(progressCanvas.gameObject);
    }
}
