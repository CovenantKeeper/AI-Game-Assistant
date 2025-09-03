using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TheCovenantKeepers.AI_Game_Assistant
{
    public static class PromptProcessor
    {
        // ----------------------------------------------------
        // Core provider call
        // ----------------------------------------------------
        private static async Task<string> GenerateWithSelectedProvider(string fullPrompt, AIProvider provider)
        {
            var settings = ChatGPTSettings.Get();
            if (settings == null)
            {
                Debug.LogError("ChatGPTSettings asset not found.");
                return null;
            }

            switch (provider)
            {
                case AIProvider.OpenAI:
                    return await ChatGPTClient.GenerateScriptAsync(fullPrompt, settings.apiKey, settings.apiUrl, settings.model);
                case AIProvider.Gemini:
                    return await GeminiClient.GenerateScriptAsync(settings.geminiApiKey, fullPrompt);
                default:
                    Debug.LogError($"AI Provider '{provider}' is not supported.");
                    return null;
            }
        }

        // ----------------------------------------------------
        // Public generators
        // ----------------------------------------------------
        public static async Task GenerateCharacterMasterlistFromPrompt(string prompt, string savePath, AIProvider provider)
        {
            const string header =
                "Name,Type,Role,Affiliation,Class,Faction,Element,Gender,Health,Mana,Attack,Defense,Magic,Speed,UltimateAbility,LoreBackground,ModelPath";

            string fullPrompt =
                "You are a game data generator. Output MUST be raw CSV rows only (no markdown).\n" +
                "Columns (exact order): " + header + "\n\n" + prompt;

            string csv = await GenerateWithSelectedProvider(fullPrompt, provider);
            if (string.IsNullOrWhiteSpace(csv)) { Debug.LogError("❌ No response from provider."); return; }

            csv = EnsureCsvHasHeader(SanitizeToCsv(csv), header);

            AssistantPaths.EnsureDirectoryForFile(savePath);
            System.IO.File.WriteAllText(savePath, csv);
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(savePath);
#endif
            CharacterDatabase.LoadCharacterMasterlist(savePath);
            Debug.Log($"✅ Character masterlist saved to: {savePath}");
        }

        public static async Task GenerateItemMasterlistFromPrompt(string prompt, string savePath, AIProvider provider)
        {
            const string header =
                "ItemID,ItemName,ItemType,SubType,Description,ValueBuy,ValueSell,Weight,IsUsable,IsEquippable,EquipmentSlot,StatModifier1_Type,StatModifier1_Value,StatModifier2_Type,StatModifier2_Value,UseEffect,RequiredLevel,CraftingMaterials,Notes,PrefabPath";

            string fullPrompt =
                "Create RPG items as raw CSV rows only (no markdown).\n" +
                "Columns (exact order): " + header + "\n\n" + prompt;

            string csv = await GenerateWithSelectedProvider(fullPrompt, provider);
            if (string.IsNullOrWhiteSpace(csv)) { Debug.LogError("❌ No response from provider."); return; }

            csv = EnsureCsvHasHeader(SanitizeToCsv(csv), header);

            AssistantPaths.EnsureDirectoryForFile(savePath);
            System.IO.File.WriteAllText(savePath, csv);
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(savePath);
#endif
            ItemDatabase.LoadItemMasterlist(savePath);
            Debug.Log($"✅ Item masterlist saved to: {savePath}");
        }

        public static async Task GenerateAbilityMasterlistFromPrompt(string prompt, string savePath, AIProvider provider)
        {
            const string header =
                "AbilityID,AbilityName,Description,AbilityType,TargetType,Range,ManaCost,CooldownSeconds,CastTimeSeconds,DamageAmount,DamageType,HealingAmount,BuffDebuffEffect,AreaOfEffectRadius,ProjectilePrefabPath,VFX_CastPath,VFX_HitPath,SFX_CastPath,SFX_HitPath,AnimationTriggerCast,AnimationTriggerImpact,RequiredLevel,PrerequisiteAbilityID,Notes";

            string fullPrompt =
                "Create a structured CSV for RPG abilities. Raw rows only, no markdown or commentary.\n" +
                "Columns (exact order): " + header + "\n\n" + prompt;

            string csv = await GenerateWithSelectedProvider(fullPrompt, provider);
            if (string.IsNullOrWhiteSpace(csv)) { Debug.LogError("❌ No response from provider."); return; }

            csv = EnsureCsvHasHeader(SanitizeToCsv(csv), header);

            AssistantPaths.EnsureDirectoryForFile(savePath);
            System.IO.File.WriteAllText(savePath, csv);
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(savePath);
#endif
            AbilityDatabase.LoadAbilityMasterlist(savePath);
            Debug.Log($"✅ Ability masterlist saved to: {savePath}");
        }

        public static async Task GenerateQuestMasterlistFromPrompt(string prompt, string savePath, AIProvider provider)
        {
            const string header = "Title,Objective,Type,Reward,Region,LoreHint,PrefabPath";

            string fullPrompt =
                "Create a structured CSV for RPG quests. Raw rows only, no markdown or code.\n" +
                "Columns (exact order): " + header + "\n\n" + prompt;

            string csv = await GenerateWithSelectedProvider(fullPrompt, provider);
            if (string.IsNullOrWhiteSpace(csv)) { Debug.LogError("❌ No response from provider."); return; }

            csv = EnsureCsvHasHeader(SanitizeToCsv(csv), header);

            AssistantPaths.EnsureDirectoryForFile(savePath);
            System.IO.File.WriteAllText(savePath, csv);
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(savePath);
#endif
            QuestDatabase.LoadQuestMasterlist(savePath);
            Debug.Log($"✅ Quest masterlist saved to: {savePath}");
        }

        public static async Task GenerateLocationMasterlistFromPrompt(string prompt, string savePath, AIProvider provider)
        {
            const string header = "Name,Region,Type,FactionControl,DangerLevel,Lore,PrefabPath";

            string fullPrompt =
                "Create a structured CSV for RPG locations. Raw rows only, no markdown or code.\n" +
                "Columns (exact order): " + header + "\n\n" + prompt;

            string csv = await GenerateWithSelectedProvider(fullPrompt, provider);
            if (string.IsNullOrWhiteSpace(csv)) { Debug.LogError("❌ No response from provider."); return; }

            csv = EnsureCsvHasHeader(SanitizeToCsv(csv), header);

            AssistantPaths.EnsureDirectoryForFile(savePath);
            System.IO.File.WriteAllText(savePath, csv);
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(savePath);
#endif
            LocationDatabase.LoadLocationMasterlist(savePath);
            Debug.Log($"✅ Location masterlist saved to: {savePath}");
        }

        // ----------------------------------------------------
        // Helpers
        // ----------------------------------------------------
        private static string StripCodeFences(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = Regex.Replace(s, @"^\s*```.*\n", string.Empty, RegexOptions.Multiline);
            s = Regex.Replace(s, @"\n```(\s*)$", string.Empty, RegexOptions.Multiline);
            return s.Trim();
        }

        private static string SanitizeToCsv(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = StripCodeFences(s).Trim();

            // Heuristic: remove obvious code lines
            var lines = s.Split('\n');
            var keep = new System.Collections.Generic.List<string>(lines.Length);
            foreach (var raw in lines)
            {
                var t = raw.Trim();
                if (string.IsNullOrEmpty(t)) continue;
                if (Regex.IsMatch(t, @"^(using|namespace|public|private|class|struct|enum|//|/\*|\{|\})")) continue;
                keep.Add(t);
            }
            return string.Join("\n", keep);
        }

        private static string EnsureCsvHasHeader(string csvBody, string header)
        {
            if (string.IsNullOrWhiteSpace(csvBody)) return header + "\n";
            string trimmed = csvBody.Trim();
            string firstLine = trimmed.Split('\n')[0].Trim('\r', ' ');
            string Norm(string x) => x.Replace(" ", "").ToLowerInvariant();
            if (Norm(firstLine).StartsWith(Norm(header))) return trimmed; // already has header
            return header + "\n" + trimmed;
        }

        // Existing template helper
        public static string ProcessScriptTemplate(string templateContent, string scriptName)
        {
            if (string.IsNullOrEmpty(templateContent)) return "// Error: Template content is empty.";
            string sanitizedClassName = Regex.Replace(scriptName, @"\s+", "");
            return templateContent.Replace("{{ClassName}}", sanitizedClassName);
        }
    }
}
