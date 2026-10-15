using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates the editable menu and rebind-row prefabs, then places the menu in Main Menu.unity.</summary>
public static class MainMenuPrefabSetup
{
    private const string MenuScenePath = "Assets/Scenes/Main Menu.unity";
    private const string MenuPrefabPath = "Assets/Prefabs/Main Menu Canvas.prefab";
    private const string RebindPrefabPath = "Assets/Prefabs/Key Binding Row.prefab";
    private static readonly Color Ink = new Color(0.16f, 0.19f, 0.16f);
    private static readonly Color Leaf = new Color(0.35f, 0.48f, 0.31f);
    private static readonly Color Paper = new Color(0.91f, 0.87f, 0.75f);

    [MenuItem("Tea Store Tidy/Build Main Menu Prefabs and Scene")]
    public static void BuildMainMenu()
    {
        EnsureFolder("Assets/Prefabs");
        RebindRow rowPrefab = BuildRebindRowPrefab();
        GameObject canvasPrefab = BuildCanvasPrefab(rowPrefab);
        AssetDatabase.SaveAssets();

        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        foreach (MainMenuController old in Object.FindObjectsByType<MainMenuController>(FindObjectsSortMode.None))
            Object.DestroyImmediate(old.gameObject);
        GameObject menu = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
        menu.name = "Main Menu Canvas";
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ((InputSystemUIInputModule)events.GetComponent<InputSystemUIInputModule>()).AssignDefaultActions();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/MainGame.unity", true)
        };
        AssetDatabase.SaveAssets();
        Debug.Log("Main Menu prefab and scene are ready. Add the generated canvas prefab to further customize its visuals.");
    }

    private static RebindRow BuildRebindRowPrefab()
    {
        GameObject root = new GameObject("Key Binding Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        Image image = root.GetComponent<Image>(); image.color = new Color(Paper.r, Paper.g, Paper.b, 0.75f);
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 7, 7); layout.spacing = 16; layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
        RectTransform rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(600, 52);
        root.AddComponent<LayoutElement>().preferredHeight = 52;
        TMP_Text name = CreateText("Action", root.transform, "Action", 22, TextAlignmentOptions.MidlineLeft, Ink);
        name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        TMP_Text binding = CreateText("Binding", root.transform, "Key", 20, TextAlignmentOptions.Center, Ink);
        binding.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;
        Button button = CreateButton("Change", root.transform, "Change", new Vector2(132, 42));
        button.gameObject.AddComponent<LayoutElement>().preferredWidth = 132;
        RebindRow row = root.AddComponent<RebindRow>();
        SetObject(row, "actionLabel", name); SetObject(row, "currentBindingLabel", binding); SetObject(row, "rebindButton", button);
        string path = RebindPrefabPath;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<RebindRow>(path);
    }

    private static GameObject BuildCanvasPrefab(RebindRow rowPrefab)
    {
        GameObject canvas = new GameObject("Main Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas c = canvas.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;

        GameObject background = CreatePanel("Backdrop", canvas.transform, new Color(0.12f, 0.19f, 0.15f, 1)); Stretch(background.GetComponent<RectTransform>());
        GameObject card = CreatePanel("Menu Card", background.transform, new Color(Paper.r, Paper.g, Paper.b, 0.96f));
        RectTransform cardRect = card.GetComponent<RectTransform>(); Center(cardRect, new Vector2(660, 820));
        TMP_Text title = CreateText("Title", card.transform, "TEA STORE TIDY", 48, TextAlignmentOptions.Center, Ink);
        SetRect(title.rectTransform, new Vector2(0, 330), new Vector2(560, 90));
        TMP_Text subtitle = CreateText("Subtitle", card.transform, "A little order, one shelf at a time.", 22, TextAlignmentOptions.Center, Ink);
        SetRect(subtitle.rectTransform, new Vector2(0, 278), new Vector2(560, 45));

        GameObject main = CreatePanel("Main Panel", card.transform, Color.clear); Center(main.GetComponent<RectTransform>(), new Vector2(540, 560));
        Button newGame = AddMenuButton(main.transform, "New Game", 205);
        Button load = AddMenuButton(main.transform, "Load Game", 125);
        Button settings = AddMenuButton(main.transform, "Settings", 45);
        Button quit = AddMenuButton(main.transform, "Quit", -35);

        GameObject settingsPanel = CreatePanel("Settings Panel", card.transform, Color.clear); Center(settingsPanel.GetComponent<RectTransform>(), new Vector2(570, 650));
        CreateText("Settings Title", settingsPanel.transform, "SETTINGS", 34, TextAlignmentOptions.Center, Ink).rectTransform.anchoredPosition = new Vector2(0, 270);
        Slider volume = AddSlider(settingsPanel.transform, "Master Volume", 205, 1f);
        TMP_Dropdown quality = AddDropdown(settingsPanel.transform, "Visual Quality", 115, new[] { "Quality" });
        TMP_Dropdown resolution = AddDropdown(settingsPanel.transform, "Resolution", 25, new[] { "Resolution" });
        Toggle fullscreen = AddToggle(settingsPanel.transform, "Fullscreen", -65);
        Button bindings = AddMenuButton(settingsPanel.transform, "Key Bindings", -155);
        Button settingsBack = AddMenuButton(settingsPanel.transform, "Back", -235);

        GameObject bindingPanel = CreatePanel("Key Bindings Panel", card.transform, Color.clear); Center(bindingPanel.GetComponent<RectTransform>(), new Vector2(620, 710));
        CreateText("Bindings Title", bindingPanel.transform, "KEY BINDINGS", 34, TextAlignmentOptions.Center, Ink).rectTransform.anchoredPosition = new Vector2(0, 315);
        GameObject scroll = new GameObject("Bindings Scroll View", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
        scroll.transform.SetParent(bindingPanel.transform, false);
        RectTransform scrollRect = scroll.GetComponent<RectTransform>(); Center(scrollRect, new Vector2(600, 560)); scrollRect.anchoredPosition = new Vector2(0, -10);
        Image scrollImage = scroll.GetComponent<Image>(); scrollImage.color = new Color(1, 1, 1, 0.15f);
        scroll.GetComponent<Mask>().showMaskGraphic = false;
        GameObject content = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); content.transform.SetParent(scroll.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>(); contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = new Vector2(1, 1); contentRect.pivot = new Vector2(0.5f, 1); contentRect.sizeDelta = new Vector2(-10, 0);
        VerticalLayoutGroup rowsLayout = content.GetComponent<VerticalLayoutGroup>(); rowsLayout.spacing = 8; rowsLayout.padding = new RectOffset(5, 5, 5, 5); rowsLayout.childForceExpandHeight = false; rowsLayout.childForceExpandWidth = true;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scrollComponent = scroll.GetComponent<ScrollRect>(); scrollComponent.content = contentRect; scrollComponent.viewport = scrollRect; scrollComponent.horizontal = false;
        Button bindingBack = AddMenuButton(bindingPanel.transform, "Back", -320);

        TMP_Text status = CreateText("Status", card.transform, "", 16, TextAlignmentOptions.Center, Ink);
        SetRect(status.rectTransform, new Vector2(0, -370), new Vector2(580, 35));
        settingsPanel.SetActive(false); bindingPanel.SetActive(false);

        MainMenuController controller = canvas.AddComponent<MainMenuController>();
        SetObject(controller, "mainPanel", main); SetObject(controller, "settingsPanel", settingsPanel); SetObject(controller, "keyBindingsPanel", bindingPanel);
        SetObject(controller, "newGameButton", newGame); SetObject(controller, "loadGameButton", load); SetObject(controller, "settingsButton", settings);
        SetObject(controller, "keyBindingsButton", bindings); SetObject(controller, "backFromSettingsButton", settingsBack); SetObject(controller, "backFromBindingsButton", bindingBack); SetObject(controller, "quitButton", quit);
        SetObject(controller, "masterVolumeSlider", volume); SetObject(controller, "qualityDropdown", quality); SetObject(controller, "resolutionDropdown", resolution); SetObject(controller, "fullscreenToggle", fullscreen);
        SetObject(controller, "inputActions", FindInputActions()); SetObject(controller, "bindingRowsParent", content.transform); SetObject(controller, "rebindRowPrefab", rowPrefab); SetObject(controller, "statusText", status);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvas, MenuPrefabPath);
        Object.DestroyImmediate(canvas);
        return prefab;
    }

    private static InputActionAsset FindInputActions()
    {
        string[] ids = AssetDatabase.FindAssets("InputSystem_Actions t:InputActionAsset");
        return ids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(ids[0]));
    }

    private static Button AddMenuButton(Transform parent, string label, float y)
    {
        Button button = CreateButton(label, parent, label, new Vector2(420, 62));
        SetRect(button.GetComponent<RectTransform>(), new Vector2(0, y), new Vector2(420, 62));
        return button;
    }

    private static Button CreateButton(string objectName, Transform parent, string label, Vector2 size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>(); image.color = Leaf;
        Button button = go.GetComponent<Button>(); button.targetGraphic = image;
        ColorBlock colors = button.colors; colors.normalColor = Leaf; colors.highlightedColor = new Color(0.47f, 0.6f, 0.41f); colors.pressedColor = new Color(0.24f, 0.36f, 0.23f); button.colors = colors;
        RectTransform rect = go.GetComponent<RectTransform>(); rect.sizeDelta = size;
        TMP_Text text = CreateText("Label", go.transform, label, 24, TextAlignmentOptions.Center, Color.white); Stretch(text.rectTransform);
        return button;
    }

    private static Slider AddSlider(Transform parent, string label, float y, float value)
    {
        CreateText(label + " Label", parent, label, 20, TextAlignmentOptions.MidlineLeft, Ink).rectTransform.anchoredPosition = new Vector2(-190, y);
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Slider)); go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(220, 24); rect.anchoredPosition = new Vector2(145, y);
        GameObject track = CreatePanel("Track", go.transform, new Color(0.3f, 0.3f, 0.3f)); Stretch(track.GetComponent<RectTransform>());
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform)); fillArea.transform.SetParent(go.transform, false); Stretch(fillArea.GetComponent<RectTransform>()); fillArea.GetComponent<RectTransform>().offsetMin = new Vector2(0, 4); fillArea.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -4);
        GameObject fill = CreatePanel("Fill", fillArea.transform, Leaf); Stretch(fill.GetComponent<RectTransform>());
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)); handleArea.transform.SetParent(go.transform, false); Stretch(handleArea.GetComponent<RectTransform>()); handleArea.GetComponent<RectTransform>().offsetMin = new Vector2(5, 0); handleArea.GetComponent<RectTransform>().offsetMax = new Vector2(-5, 0);
        GameObject handle = CreatePanel("Handle", handleArea.transform, Paper); RectTransform handleRect = handle.GetComponent<RectTransform>(); handleRect.sizeDelta = new Vector2(20, 30);
        Slider slider = go.GetComponent<Slider>(); slider.fillRect = fill.GetComponent<RectTransform>(); slider.handleRect = handleRect; slider.targetGraphic = handle.GetComponent<Image>(); slider.direction = Slider.Direction.LeftToRight; slider.value = value;
        return slider;
    }

    private static TMP_Dropdown AddDropdown(Transform parent, string label, float y, string[] options)
    {
        CreateText(label + " Label", parent, label, 20, TextAlignmentOptions.MidlineLeft, Ink).rectTransform.anchoredPosition = new Vector2(-190, y);
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown)); go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(220, 42); rect.anchoredPosition = new Vector2(145, y);
        Image image = go.GetComponent<Image>(); image.color = new Color(1, 1, 1, 0.85f);
        TMP_Text caption = CreateText("Caption", go.transform, options[0], 17, TextAlignmentOptions.Center, Ink); Stretch(caption.rectTransform);
        TMP_Dropdown dropdown = go.GetComponent<TMP_Dropdown>(); dropdown.captionText = caption; dropdown.targetGraphic = image;
        dropdown.options = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData(options[0]) };
        GameObject template = CreatePanel("Template", go.transform, Color.white);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0, 0); templateRect.anchorMax = new Vector2(1, 0); templateRect.pivot = new Vector2(0.5f, 1);
        templateRect.anchoredPosition = new Vector2(0, -2); templateRect.sizeDelta = new Vector2(0, 190);
        ScrollRect scroll = template.AddComponent<ScrollRect>();
        Mask mask = template.AddComponent<Mask>(); mask.showMaskGraphic = true;
        GameObject viewport = CreatePanel("Viewport", template.transform, Color.white);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>(); Stretch(viewportRect);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>(); contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = new Vector2(1, 1); contentRect.pivot = new Vector2(0.5f, 1); contentRect.sizeDelta = new Vector2(0, 32);
        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>(); contentLayout.childForceExpandHeight = false; contentLayout.childForceExpandWidth = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(Image)); item.transform.SetParent(content.transform, false);
        item.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
        item.AddComponent<LayoutElement>().preferredHeight = 36;
        TMP_Text itemLabel = CreateText("Item Label", item.transform, "Option", 17, TextAlignmentOptions.MidlineLeft, Ink);
        RectTransform itemLabelRect = itemLabel.rectTransform; Stretch(itemLabelRect); itemLabelRect.offsetMin = new Vector2(12, 0); itemLabelRect.offsetMax = new Vector2(-8, 0);
        Toggle itemToggle = item.GetComponent<Toggle>(); itemToggle.targetGraphic = item.GetComponent<Image>();
        scroll.content = contentRect; scroll.viewport = viewportRect; scroll.horizontal = false;
        dropdown.template = templateRect; dropdown.itemText = itemLabel; dropdown.captionText = caption;
        template.SetActive(false);
        return dropdown;
    }

    private static Toggle AddToggle(Transform parent, string label, float y)
    {
        CreateText(label + " Label", parent, label, 20, TextAlignmentOptions.MidlineLeft, Ink).rectTransform.anchoredPosition = new Vector2(-190, y);
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Toggle)); go.transform.SetParent(parent, false); go.GetComponent<RectTransform>().anchoredPosition = new Vector2(250, y);
        GameObject box = CreatePanel("Box", go.transform, Color.white); box.GetComponent<RectTransform>().sizeDelta = new Vector2(30, 30);
        GameObject check = CreatePanel("Checkmark", box.transform, Leaf); Stretch(check.GetComponent<RectTransform>()); check.GetComponent<RectTransform>().offsetMin = new Vector2(5, 5); check.GetComponent<RectTransform>().offsetMax = new Vector2(-5, -5);
        Toggle toggle = go.GetComponent<Toggle>(); toggle.targetGraphic = box.GetComponent<Image>(); toggle.graphic = check.GetComponent<Image>();
        return toggle;
    }

    private static TMP_Text CreateText(string name, Transform parent, string content, float size, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>(); text.text = content; text.fontSize = size; text.alignment = alignment; text.color = color; text.enableAutoSizing = false;
        if (SharedFont == null)
        {
            string[] fonts = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            if (fonts.Length > 0) SharedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fonts[0]));
        }
        if (SharedFont != null) text.font = SharedFont;
        return text;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false); go.GetComponent<Image>().color = color; return go;
    }

    private static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = size;
    }
    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }
    private static void SetObject(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target); serialized.FindProperty(propertyName).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder("Assets", "Prefabs");
    }

    private static TMP_FontAsset SharedFont;
}
