namespace TheCovenantKeepers.AI_Game_Assistant
{
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

public static class GenericPrefabCreator
{
    //public static void CreatePrefabFromRecipe(CharacterData characterData, PrefabRecipe recipe)
    //{
    //    if (characterData == null || recipe == null)
    //    {
    //        Debug.LogError("Cannot create prefab, CharacterData or PrefabRecipe is null.");
    //        return;
    //    }

    //    ChatGPTSettings settings = ChatGPTSettings.Get();

    //    GameObject rootGO = new GameObject(characterData.Name);

    //    // Find and instantiate the character's 3D model
    //    string modelPath = GetModelPathForCharacter(characterData, settings.ModelSearchPath);
    //    if (!string.IsNullOrEmpty(modelPath))
    //    {
    //        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
    //        if (modelAsset != null)
    //        {
    //            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, rootGO.transform);
    //            modelInstance.name = modelAsset.name;
    //        }
    //    }
    //    else
    //    {
    //        Debug.LogWarning($"No model found for character {characterData.Name} with class {characterData.Class}. Prefab created without a model.");
    //    }

    //    // Add base components
    //    foreach (string componentName in recipe.BaseComponents)
    //    {
    //        Type componentType = Type.GetType($"UnityEngine.{componentName}, UnityEngine.CoreModule");
    //        if (componentType != null)
    //        {
    //            rootGO.AddComponent(componentType);
    //        }
    //        else
    //        {
    //            Debug.LogWarning($"Could not find Unity component type: {componentName}");
    //        }
    //    }

    //    // Add custom scripts
    //    foreach (string scriptName in recipe.CustomScripts)
    //    {
    //        Type scriptType = GetTypeByName(scriptName);
    //        if (scriptType != null)
    //        {
    //            rootGO.AddComponent(scriptType);
    //        }
    //        else
    //        {
    //            Debug.LogWarning($"Could not find custom script type: {scriptName}");
    //        }
    //    }

    //    // Special handling for CharacterStats initialization
    //    //CharacterStats statsComponent = rootGO.GetComponent<CharacterStats>();
    //    //if (statsComponent != null)
    //    //{
    //    //    statsComponent.InitializeStats(characterData);
    //    //}

    //    // Set Tag and Layer
    //    if (!string.IsNullOrEmpty(recipe.InitialTag)) rootGO.tag = recipe.InitialTag;
    //    if (!string.IsNullOrEmpty(recipe.InitialLayer)) rootGO.layer = LayerMask.NameToLayer(recipe.InitialLayer);

    //    // Save the final prefab
    //    string savePath = Path.Combine(settings.PrefabSavePath, recipe.InitialTag, characterData.Name + ".prefab");
    //    Directory.CreateDirectory(Path.GetDirectoryName(savePath));
    //    GameObject finalPrefab = PrefabUtility.SaveAsPrefabAsset(rootGO, savePath);
    //    GameObject.DestroyImmediate(rootGO);

    //    Debug.Log($"? Successfully created prefab for '{characterData.Name}' at: {savePath}", finalPrefab);
    //    EditorGUIUtility.PingObject(finalPrefab);
    //}

    // UPDATED: This method now gets the path from settings instead of a parameter.
    public static Dictionary<string, PrefabRecipe> LoadAllRecipes()
    {
        var recipes = new Dictionary<string, PrefabRecipe>();
        ChatGPTSettings settings = ChatGPTSettings.Get();
        string recipeFilePath = Path.Combine(settings.MasterDataPath, "PrefabRecipeMasterlist.txt");

        // --- NEW LOGIC: Create the file if it doesn't exist ---
        if (!File.Exists(recipeFilePath))
        {
            Debug.LogWarning($"Prefab Recipe file not found. Creating a new template at: {recipeFilePath}");
            string defaultContent =
                "Player_Warrior;A basic warrior player character;Rigidbody|CapsuleCollider;PlayerController|CharacterStats|MeleeCombat;Player;Default\n";

            File.WriteAllText(recipeFilePath, defaultContent);
            AssetDatabase.Refresh();
        }
        // --- END NEW LOGIC ---

        string[] lines = File.ReadAllLines(recipeFilePath);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#")) continue;

            // This single line now creates the recipe and does all the parsing,
            // because the logic is inside the PrefabRecipe constructor.
            var recipe = new PrefabRecipe(line);

            // We just need to check if it's valid and add it to the dictionary.
            if (recipe.IsValid)
            {
                recipes[recipe.RecipeID] = recipe;
            }
        }
        Debug.Log($"Loaded {recipes.Count} valid prefab recipes.");
        return recipes;
    }
    //private static string GetModelPathForCharacter(CharacterData charData, string modelRootFolder)
    //{
    //    string sanitizedClass = charData.Class.Replace("/", "_").Replace(" ", "");
    //    string expectedPath = Path.Combine(modelRootFolder, sanitizedClass, charData.Gender + ".prefab");
    //    if (File.Exists(expectedPath))
    //    {
    //        return expectedPath;
    //    }
    //    return null;
    //}

    public static Type GetTypeByName(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(name);
            if (type != null)
            {
                return type;
            }
        }
        return null;
    }
}
}
