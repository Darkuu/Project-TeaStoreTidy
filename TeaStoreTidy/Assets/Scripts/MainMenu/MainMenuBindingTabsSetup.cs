using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Adds keyboard/controller tabs to the existing customized menu prefab without rebuilding its visuals.</summary>
public static class MainMenuBindingTabsSetup
{
    private const string MenuPrefabPath = "Assets/Prefabs/Main Menu Canvas.prefab";

    [MenuItem("Tea Store Tidy/Add Keyboard and Controller Tabs (Preserve Menu Visuals)")]
    public static void AddTabs()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MenuPrefabPath);
        try
        {
            MainMenuController controller = root.GetComponentInChildren<MainMenuController>(true);
            Transform panel = FindDescendant(root.transform, "Key Bindings Panel");
            Transform keyboardScroll = panel != null ? panel.Find("Bindings Scroll View") : null;
            Transform backButtonTransform = panel != null ? panel.Find("Back") : null;
            if (controller == null || panel == null || keyboardScroll == null || backButtonTransform == null)
            {
                Debug.LogError("Could not find the key bindings panel, its scroll view, Back button, or MainMenuController. The menu prefab was left unchanged.");
                return;
            }

            ScrollRect keyboardScrollRect = keyboardScroll.GetComponent<ScrollRect>();
            if (keyboardScrollRect == null || keyboardScrollRect.content == null)
            {
                Debug.LogError("The keyboard binding list needs a ScrollRect with its Content assigned. The menu prefab was left unchanged.");
                return;
            }

            Transform controllerScroll = panel.Find("Controller Bindings Scroll View");
            if (controllerScroll == null)
            {
                GameObject clone = Object.Instantiate(keyboardScroll.gameObject, panel, false);
                clone.name = "Controller Bindings Scroll View";
                controllerScroll = clone.transform;
            }

            SetListGeometry(keyboardScroll as RectTransform);
            SetListGeometry(controllerScroll as RectTransform);
            controllerScroll.gameObject.SetActive(false);

            Button sourceButton = backButtonTransform.GetComponent<Button>();
            Button keyboardTab = GetOrCreateTab(panel, sourceButton, "Keyboard Tab", "Keyboard", new Vector2(-117, 247));
            Button controllerTab = GetOrCreateTab(panel, sourceButton, "Controller Tab", "Controller", new Vector2(117, 247));

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("bindingRowsParent").objectReferenceValue = keyboardScrollRect.content;
            serialized.FindProperty("controllerBindingRowsParent").objectReferenceValue = controllerScroll.GetComponent<ScrollRect>().content;
            serialized.FindProperty("keyboardBindingsContainer").objectReferenceValue = keyboardScroll.gameObject;
            serialized.FindProperty("controllerBindingsContainer").objectReferenceValue = controllerScroll.gameObject;
            serialized.FindProperty("keyboardBindingsTabButton").objectReferenceValue = keyboardTab;
            serialized.FindProperty("controllerBindingsTabButton").objectReferenceValue = controllerTab;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            keyboardScroll.gameObject.SetActive(true);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, MenuPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Added separate keyboard and controller tabs while preserving the existing menu and dropdown visuals.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Button GetOrCreateTab(Transform panel, Button template, string objectName, string label, Vector2 position)
    {
        Transform existing = panel.Find(objectName);
        Button button;
        if (existing != null)
        {
            button = existing.GetComponent<Button>();
        }
        else
        {
            GameObject copy = Object.Instantiate(template.gameObject, panel, false);
            copy.name = objectName;
            button = copy.GetComponent<Button>();
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(220, 48);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
        button.onClick.RemoveAllListeners();
        EditorUtility.SetDirty(button.gameObject);
        return button;
    }

    private static void SetListGeometry(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, -18);
        rect.sizeDelta = new Vector2(600, 490);
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root.name == targetName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }
}
