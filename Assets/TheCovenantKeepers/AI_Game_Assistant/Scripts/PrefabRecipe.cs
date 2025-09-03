namespace TheCovenantKeepers.AI_Game_Assistant
{
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PrefabRecipe
{
    public string RecipeID { get; private set; }
    public string Description { get; private set; }
    public List<string> BaseComponents { get; private set; } = new List<string>();
    public List<string> CustomScripts { get; private set; } = new List<string>();
    public string InitialTag { get; private set; }
    public string InitialLayer { get; private set; }

    // This new property will tell us if the recipe was parsed correctly.
    public bool IsValid { get; private set; }

    public PrefabRecipe(string recipeLine)
    {
        string[] parts = recipeLine.Split(';');
        if (parts.Length == 6)
        {
            RecipeID = parts[0].Trim();
            Description = parts[1].Trim();
            BaseComponents = parts[2].Split('|').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            CustomScripts = parts[3].Split('|').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            InitialTag = parts[4].Trim();
            InitialLayer = parts[5].Trim();

            // If we have a valid ID, the recipe is considered valid.
            IsValid = !string.IsNullOrEmpty(RecipeID);
        }
        else
        {
            Debug.LogWarning($"Skipping malformed recipe line: {recipeLine}");
            IsValid = false;
        }
    }
}
}
