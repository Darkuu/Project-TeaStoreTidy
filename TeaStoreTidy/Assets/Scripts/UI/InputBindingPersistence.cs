using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Applies the player's saved Input System overrides whenever an action asset is used.</summary>
public static class InputBindingPersistence
{
    private const string PreferencesKey = "InputBindingOverrides";

    public static void Apply(InputActionAsset actions)
    {
        if (actions == null) return;
        string json = PlayerPrefs.GetString(PreferencesKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
            actions.LoadBindingOverridesFromJson(json);
    }
}
