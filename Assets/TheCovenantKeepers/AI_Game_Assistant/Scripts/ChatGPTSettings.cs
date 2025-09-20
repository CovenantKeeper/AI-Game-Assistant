namespace TheCovenantKeepers.AI_Game_Assistant
{
using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "ChatGPTSettings", menuName = "AI Assistant/Settings Asset", order = 1)]
public class ChatGPTSettings : ScriptableObject
{
    [Header("API Keys")]
    public string apiKey = "YOUR_OPENAI_KEY_HERE";
    public string apiUrl = "https://api.openai.com/v1/chat/completions";
    public string model = "gpt-4o";
    public string geminiApiKey = "YOUR_GEMINI_KEY_HERE";

    [Header("Project File Paths")]
    [Tooltip("The folder where your Character, Item, and Ability CSV files are located.")]
    public string MasterDataPath = AssistantPaths.DataRoot;

    [Tooltip("The folder where the tool will save generated ScriptableObjects.")]
    public string ScriptableObjectPath = AssistantPaths.PackageRoot + "/Data/ScriptableObjects/";

    [Tooltip("The folder where the tool will search for character models.")]
    public string ModelSearchPath = AssistantPaths.PackageRoot + "/Art/Models/Characters/";

    [Tooltip("The folder where the tool will save generated Animator Controllers.")]
    public string AnimatorControllerPath = AssistantPaths.PackageRoot + "/AnimatorControllers/";

    [Tooltip("The folder where the final character prefabs will be saved.")]
    public string PrefabSavePath = AssistantPaths.PackageRoot + "/Prefabs/";

    [Tooltip("The folder where generated C# scripts will be saved.")]
    public string ScriptGenerationPath = AssistantPaths.GeneratedScripts + "/";

    [Tooltip("The folder containing your high-quality blueprint scripts.")]
    public string BlueprintScriptsPath = AssistantPaths.ScriptsRoot + "/Blueprints/";

    [Tooltip("Path to the CSV file that maps animator state names to animation clip search keywords.")]
    public string AnimationMapPath = AssistantPaths.DataRoot + "/AnimationMap.csv";

    [Header("Animation Search")] 
    [Tooltip("Additional folders to search for AnimationClips when auto-building controllers. e.g. Assets/ExplosiveLLC")] 
    public string[] AdditionalAnimationSearchFolders = new[] { "Assets/ExplosiveLLC" };

    private static ChatGPTSettings _instance;
    public static ChatGPTSettings Get()
    {
        if (_instance != null) return _instance;

        // Try to find any existing settings asset
        string[] guids = AssetDatabase.FindAssets("t:ChatGPTSettings");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _instance = AssetDatabase.LoadAssetAtPath<ChatGPTSettings>(path);

            // Migrate legacy location if necessary
            if (path.StartsWith("Assets/AI_RPG_Assistant"))
            {
                string targetFolder = AssistantPaths.EditorRoot;
                AssistantPaths.EnsureFolder(targetFolder);
                string newPath = (targetFolder + "/ChatGPTSettings.asset").Replace('\\','/');
                string err = AssetDatabase.MoveAsset(path, newPath);
                if (!string.IsNullOrEmpty(err))
                {
                    // Fallback copy
                    try
                    {
                        var sysSrc = ToSystemPath(path);
                        var sysDst = ToSystemPath(newPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(sysDst));
                        File.Copy(sysSrc, sysDst, true);
                        AssetDatabase.ImportAsset(newPath);
                        AssetDatabase.DeleteAsset(path);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Failed to migrate ChatGPTSettings asset: {ex.Message}");
                    }
                }
                _instance = AssetDatabase.LoadAssetAtPath<ChatGPTSettings>(newPath);
            }
        }

        // Create a new one if still missing
        if (_instance == null)
        {
            string targetFolder = AssistantPaths.EditorRoot;
            Debug.LogWarning($"ChatGPTSettings asset not found. Creating a new one in {targetFolder}");
            _instance = CreateInstance<ChatGPTSettings>();
            AssistantPaths.EnsureFolder(targetFolder);
            string assetPath = (targetFolder + "/ChatGPTSettings.asset").Replace('\\','/');
            AssetDatabase.CreateAsset(_instance, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Ensure paths are using Game Assistant root instead of legacy AI_RPG_Assistant
        MigrateInternalPathsIfNeeded(_instance);
        return _instance;
    }

    private static void MigrateInternalPathsIfNeeded(ChatGPTSettings s)
    {
        bool changed = false;
        string legacyRoot = "Assets/AI_RPG_Assistant";
        string ReplaceLegacy(string input, string fallback)
        {
            if (string.IsNullOrEmpty(input)) { changed = true; return fallback; }
            if (input.StartsWith(legacyRoot)) { changed = true; return input.Replace(legacyRoot, AssistantPaths.PackageRoot); }
            return input;
        }

        s.MasterDataPath = ReplaceLegacy(s.MasterDataPath, AssistantPaths.DataRoot);
        s.ScriptableObjectPath = ReplaceLegacy(s.ScriptableObjectPath, AssistantPaths.PackageRoot + "/Data/ScriptableObjects/");
        s.ModelSearchPath = ReplaceLegacy(s.ModelSearchPath, AssistantPaths.PackageRoot + "/Art/Models/Characters/");
        s.AnimatorControllerPath = ReplaceLegacy(s.AnimatorControllerPath, AssistantPaths.PackageRoot + "/AnimatorControllers/");
        s.PrefabSavePath = ReplaceLegacy(s.PrefabSavePath, AssistantPaths.PackageRoot + "/Prefabs/");
        s.ScriptGenerationPath = ReplaceLegacy(s.ScriptGenerationPath, AssistantPaths.GeneratedScripts + "/");
        s.BlueprintScriptsPath = ReplaceLegacy(s.BlueprintScriptsPath, AssistantPaths.ScriptsRoot + "/Blueprints/");
        s.AnimationMapPath = ReplaceLegacy(s.AnimationMapPath, AssistantPaths.DataRoot + "/AnimationMap.csv");

        // Ensure animation search includes known pack folders mentioned by the user
        string[] desiredDefaults = new[]
        {
            "Assets/ExplosiveLLC",
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations",
            "Assets/PolygonFantasyHeroCharacters",
            "Assets/GabrielAguiarProductions",
            "Assets/Hovl Studio"
        };

        System.Collections.Generic.List<string> merged = new System.Collections.Generic.List<string>();
        if (s.AdditionalAnimationSearchFolders != null && s.AdditionalAnimationSearchFolders.Length > 0)
            merged.AddRange(s.AdditionalAnimationSearchFolders);

        foreach (var f in desiredDefaults)
        {
            var path = (f ?? string.Empty).Replace('\\','/');
            if (string.IsNullOrEmpty(path)) continue;
            if (!merged.Contains(path) && AssetDatabase.IsValidFolder(path))
            {
                merged.Add(path);
                changed = true;
            }
        }

        if (merged.Count == 0)
        {
            // fallback at least one default to avoid null
            merged.Add("Assets/ExplosiveLLC");
            changed = true;
        }

        if (s.AdditionalAnimationSearchFolders == null || merged.Count != s.AdditionalAnimationSearchFolders.Length)
            s.AdditionalAnimationSearchFolders = merged.ToArray();

        if (changed)
        {
            // Make sure folders exist so future saves go to the right place
            AssistantPaths.EnsureFolder(AssistantPaths.DataRoot);
            AssistantPaths.EnsureFolder(AssistantPaths.PackageRoot + "/Data/ScriptableObjects/");
            AssistantPaths.EnsureFolder(AssistantPaths.PackageRoot + "/AnimatorControllers/");
            AssistantPaths.EnsureFolder(AssistantPaths.PackageRoot + "/Prefabs/");
            AssistantPaths.EnsureFolder(AssistantPaths.GeneratedScripts + "/");
            AssistantPaths.EnsureFolder(AssistantPaths.ScriptsRoot + "/Blueprints/");

            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
        }
    }

    private static string ToSystemPath(string assetPath)
    {
        var projectAssets = Application.dataPath.Replace("\\", "/");
        if (!assetPath.StartsWith("Assets/")) return assetPath.Replace("\\", "/");
        return projectAssets.Substring(0, projectAssets.Length - "Assets".Length) + assetPath;
    }
  }
}
