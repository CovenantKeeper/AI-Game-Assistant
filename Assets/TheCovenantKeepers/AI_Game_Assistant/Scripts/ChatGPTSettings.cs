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
        return _instance;
    }

    private static string ToSystemPath(string assetPath)
    {
        var projectAssets = Application.dataPath.Replace("\\", "/");
        if (!assetPath.StartsWith("Assets/")) return assetPath.Replace("\\", "/");
        return projectAssets.Substring(0, projectAssets.Length - "Assets".Length) + assetPath;
    }
  }
}
