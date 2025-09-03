namespace TheCovenantKeepers.AI_Game_Assistant
{
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Alias Unity�s Debug so it won�t clash with System.Diagnostics
using Debug = UnityEngine.Debug;

public static class AnimatorPresetLibrary
{
    private static List<AnimatorPreset> _allPresets;

    /// <summary>
    /// Gets all AnimatorPreset assets in the project.
    /// </summary>
    public static IReadOnlyList<AnimatorPreset> AllPresets
    {
        get
        {
            if (_allPresets == null)
                LoadAllPresets();
            return _allPresets;
        }
    }

    private static void LoadAllPresets()
    {
        _allPresets = new List<AnimatorPreset>();
        // Find all assets of type AnimatorPreset
        string[] guids = AssetDatabase.FindAssets("t:AnimatorPreset");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var preset = AssetDatabase.LoadAssetAtPath<AnimatorPreset>(path);
            if (preset != null)
                _allPresets.Add(preset);
            else
                Debug.LogWarning($"[AnimatorPresetLibrary] Failed to load preset at {path}");
        }
    }

    /// <summary>
    /// Returns the first preset whose characterClassKey matches (case-insensitive).
    /// </summary>
    public static AnimatorPreset GetPresetForClass(string classKey)
    {
        foreach (var preset in AllPresets)
        {
            if (preset.characterClassKey.Equals(classKey, System.StringComparison.OrdinalIgnoreCase))
                return preset;
        }

        Debug.LogWarning($"[AnimatorPresetLibrary] No preset found for class '{classKey}'");
        return null;
    }
}
}
