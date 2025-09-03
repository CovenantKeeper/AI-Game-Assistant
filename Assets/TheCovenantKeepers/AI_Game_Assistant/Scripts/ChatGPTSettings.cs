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
    public string MasterDataPath = "Assets/AI_RPG_Assistant/Data/";

    [Tooltip("The folder where the tool will save generated ScriptableObjects.")]
    public string ScriptableObjectPath = "Assets/AI_RPG_Assistant/Data/ScriptableObjects/";

    [Tooltip("The folder where the tool will search for character models.")]
    public string ModelSearchPath = "Assets/AI_RPG_Assistant/Art/Models/Characters/";

    [Tooltip("The folder where the tool will save generated Animator Controllers.")]
    public string AnimatorControllerPath = "Assets/AI_RPG_Assistant/AnimatorControllers/";

    [Tooltip("The folder where the final character prefabs will be saved.")]
    public string PrefabSavePath = "Assets/AI_RPG_Assistant/Prefabs/";

    [Tooltip("The folder where generated C# scripts will be saved.")]
    public string ScriptGenerationPath = "Assets/AI_RPG_Assistant/Scripts/Generated/";

    [Tooltip("The folder containing your high-quality blueprint scripts.")]
    public string BlueprintScriptsPath = "Assets/AI_RPG_Assistant/Scripts/Blueprints/";

    //[Tooltip("The folder path to your main animation pack (e.g., from the Asset Store).")]
    //public string AnimationPackPath = "Assets/AI_RPG_Assistant/Art/Animations/";

    [Tooltip("Path to the CSV file that maps animator state names to animation clip search keywords.")]
    public string AnimationMapPath = "Assets/AI_RPG_Assistant/Data/AnimationMap.csv";

    private static ChatGPTSettings _instance;
    public static ChatGPTSettings Get()
    {
        if (_instance != null) return _instance;

        string[] guids = AssetDatabase.FindAssets("t:ChatGPTSettings");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _instance = AssetDatabase.LoadAssetAtPath<ChatGPTSettings>(path);
        }

        if (_instance == null)
        {
            Debug.LogWarning("ChatGPTSettings asset not found. Creating a new one in Assets/AI_RPG_Assistant/Editor/");
            _instance = CreateInstance<ChatGPTSettings>();

            string path = "Assets/AI_RPG_Assistant/Editor/";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            AssetDatabase.CreateAsset(_instance, path + "ChatGPTSettings.asset");
            AssetDatabase.SaveAssets();
        }
        return _instance;
    }

  }
}
