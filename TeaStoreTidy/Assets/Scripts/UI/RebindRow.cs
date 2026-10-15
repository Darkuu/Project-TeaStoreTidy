using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>One editable binding row used by the key bindings panel prefab.</summary>
public sealed class RebindRow : MonoBehaviour
{
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private TMP_Text currentBindingLabel;
    [SerializeField] private Button rebindButton;

    private InputActionAsset asset;
    private InputAction action;
    private int bindingIndex;
    private Action<string> report;

    public void Initialize(InputActionAsset actionAsset, InputAction inputAction, int index, string label, Action<string> status)
    {
        asset = actionAsset;
        action = inputAction;
        bindingIndex = index;
        report = status;
        if (actionLabel != null) actionLabel.text = label;
        UpdateBindingLabel();
        if (rebindButton == null) return;
        rebindButton.onClick.RemoveAllListeners();
        rebindButton.onClick.AddListener(BeginRebind);
    }

    private void BeginRebind()
    {
        if (action == null || rebindButton == null || bindingIndex >= action.bindings.Count) return;
        rebindButton.interactable = false;
        if (currentBindingLabel != null) currentBindingLabel.text = "Press a key...";
        action.Disable();
        action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(operation =>
            {
                operation.Dispose();
                action.Enable();
                rebindButton.interactable = true;
                SaveOverrides();
                UpdateBindingLabel();
                report?.Invoke("Binding updated.");
            })
            .OnCancel(operation =>
            {
                operation.Dispose();
                action.Enable();
                rebindButton.interactable = true;
                UpdateBindingLabel();
                report?.Invoke("Rebinding cancelled.");
            })
            .Start();
    }

    private void SaveOverrides()
    {
        if (asset == null) return;
        PlayerPrefs.SetString("InputBindingOverrides", asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    private void UpdateBindingLabel()
    {
        if (action != null && currentBindingLabel != null && bindingIndex < action.bindings.Count)
            currentBindingLabel.text = action.GetBindingDisplayString(bindingIndex);
    }
}
