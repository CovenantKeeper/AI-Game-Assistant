namespace TheCovenantKeepers.AI_Game_Assistant
{
// Path: Assets/ChatGPTUnityPlugin/Editor/RuntimeTestSpawner.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class RuntimeTestSpawner
{
    private const string PrefabPathKey = "RT_PrefabPath";
    private const string SpawnPosXKey = "RT_SpawnPosX";
    private const string SpawnPosYKey = "RT_SpawnPosY";
    private const string SpawnPosZKey = "RT_SpawnPosZ";
    private const string IsEnabledKey = "RT_IsEnabled";

    static RuntimeTestSpawner()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    // This method is called from your ChatGPTEditorWindow UI
    public static void SetTestData(GameObject prefab, Vector3 position, bool enabled)
    {
        if (enabled && prefab != null)
        {
            EditorPrefs.SetString(PrefabPathKey, AssetDatabase.GetAssetPath(prefab));
            EditorPrefs.SetFloat(SpawnPosXKey, position.x);
            EditorPrefs.SetFloat(SpawnPosYKey, position.y);
            EditorPrefs.SetFloat(SpawnPosZKey, position.z);
            EditorPrefs.SetBool(IsEnabledKey, true);
        }
        else
        {
            // If disabled, clear the saved settings
            EditorPrefs.SetBool(IsEnabledKey, false);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // We want to spawn the character right after entering play mode
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool shouldSpawn = EditorPrefs.GetBool(IsEnabledKey, false);

            if (!shouldSpawn)
            {
                Debug.Log("[RuntimeTestSpawner] Spawning is disabled.");
                return;
            }

            string prefabPath = EditorPrefs.GetString(PrefabPathKey);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError("[RuntimeTestSpawner] Spawning is enabled, but no prefab path was saved!");
                return;
            }

            GameObject prefabToSpawn = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabToSpawn == null)
            {
                Debug.LogError($"[RuntimeTestSpawner] Could not load prefab from path: {prefabPath}");
                return;
            }

            Vector3 spawnPosition = new Vector3(
                EditorPrefs.GetFloat(SpawnPosXKey, 0),
                EditorPrefs.GetFloat(SpawnPosYKey, 1),
                EditorPrefs.GetFloat(SpawnPosZKey, 0)
            );

            Debug.Log($"[RuntimeTestSpawner] Spawning prefab '{prefabToSpawn.name}' at position {spawnPosition}.");
            Object.Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

            // Disable after spawning to prevent respawning on next play without re-enabling in the UI
            EditorPrefs.SetBool(IsEnabledKey, false);
        }
    }
}
}
