using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PauseMenuPrefabSetup
{
    private const string PrefabPath = "Assets/Resources/Pause Menu.prefab";

    [MenuItem("Tea Store Tidy/Create Editable Pause Menu Prefab")]
    public static void CreatePrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        var canvasObject = new GameObject("Pause Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        try
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject panel = new GameObject("Menu Root", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panel.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.1f, 0.88f);

            CreateLabel(panel.transform, "Paused", 155f, 52);
            Button resume = CreateButton(panel.transform, "Resume", 60f);
            Button save = CreateButton(panel.transform, "Save Game", -15f);
            Button menu = CreateButton(panel.transform, "Quit to Menu", -90f);
            Button desktop = CreateButton(panel.transform, "Quit to Desktop", -165f);

            PauseMenuController controller = canvasObject.AddComponent<PauseMenuController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("menuRoot").objectReferenceValue = panel;
            serialized.FindProperty("resumeButton").objectReferenceValue = resume;
            serialized.FindProperty("saveGameButton").objectReferenceValue = save;
            serialized.FindProperty("quitToMenuButton").objectReferenceValue = menu;
            serialized.FindProperty("quitToDesktopButton").objectReferenceValue = desktop;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created editable pause menu prefab at {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    private static Button CreateButton(Transform parent, string label, float y)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(380f, 58f);
        go.GetComponent<Image>().color = new Color(0.29f, 0.43f, 0.31f);
        CreateLabel(go.transform, label, 0f, 24);
        return go.GetComponent<Button>();
    }

    private static void CreateLabel(Transform parent, string text, float y, int fontSize)
    {
        var go = new GameObject(text + " Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(500f, 64f);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
    }

}
