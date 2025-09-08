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
                case AIProvider.ChatGPT:
                    return await ChatGPTClient.GenerateScriptAsync(fullPrompt, settings.apiKey, settings.apiUrl, settings.model);
                case AIProvider.Gemini:
                    return await GeminiClient.GenerateScriptAsync(settings.geminiApiKey, fullPrompt);
                default:
                    Debug.LogError($"AI Provider '{provider}' is not supported.");
                    return null;
            }
        }

        // Common character CSV header used by Character/Beast/Spirit
        private const string CharacterHeader =
            "Name,Type,Role,Affiliation,Class,Faction,Element,Gender,Health,Mana,Attack,Defense,Magic,Speed,UltimateAbility,LoreBackground,ModelPath," +
            "SubClass,ResourceType,Strength,Agility,Intelligence,Armor,MagicResist,AttackSpeed,MoveSpeed,Range,CritChance,CritDamageMultiplier,ArmorPenetration,MagicPenetration,LifeSteal,SpellVamp,CooldownReduction,Tenacity," +
            "PassiveName,PassiveDescription,Ability1Name,Ability1Description,Ability2Name,Ability2Description,Ability3Name,Ability3Description,UltimateDescription," +
            "Ability1Cost,Ability1Cooldown,Ability1Range,Ability1Target,Ability2Cost,Ability2Cooldown,Ability2Range,Ability2Target,Ability3Cost,Ability3Cooldown,Ability3Range,Ability3Target";

        // ----------------------------------------------------
        // Public generators
        // ----------------------------------------------------
        public static async Task GenerateCharacterMasterlistFromPrompt(string prompt, string savePath, AIProvider provider)
        {
            // Base header + extended MOBA/MMORPG stats
            const string header = CharacterHeader;

            string fullPrompt =
                "You are a game data generator. Output MUST be raw CSV rows only (no markdown).\n" +
                "Each character has one Passive (always-on effect), three active abilities (A1,A2,A3), and an Ultimate. Provide names and concise descriptions.\n" +
                "For A1/A2/A3 also provide Cost (resource), Cooldown (seconds), Range (meters) and Target (Self/Enemy/Ally/Area).\n" +
                "Columns (exact order): " + header + "\n\n" + prompt;

            string csv = await GenerateWithSelectedProvider(fullPrompt, provider);
            if (string.IsNullOrWhiteSpace(csv)) { Debug.LogError("❌ No response from provider."); return; }

            csv = EnsureCsvHasHeader(SanitizeToCsv(csv), header);
            csv = TryRepairCsvRows(csv, header); // attempt to auto-fix common LLM omissions
            if (!TryValidateCsv(csv, header, out var validationError))
            {
                ReportCsvError(validationError, csv);
                return;
            }

            AssistantPaths.EnsureDirectoryForFile(savePath);
            System.IO.File.WriteAllText(savePath, csv);
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(savePath);
#endif
            CharacterDatabase.LoadCharacters(savePath);
            Debug.Log($"✅ Character masterlist saved to: {savePath}");
        }

        public static async Task GenerateBeastMasterlistFromPrompt(string prompt, string savePath, AIProvider provider)
        {
            // Reuse character header
            const string header = CharacterHeader;

            string fullPrompt =
                "Generate Beasts (Type=Beast) as companions/pets using only content/themes from the Bible and related spiritual literature (e.g., Deuterocanon, 1 Enoch).\n" +
                "Rows only, no markdown. Keep names unique and descriptions concise.\n" +
                "Each entry has one Passive and three actives (A1–A3) plus an Ultimate.\n" +
                "For A1/A2/A3 provide Cost (uses ResourceType), Cooldown (sec), Range (m), Target from {Self,Ally,Enemy,Area,Cone,Line,Ground,Projectile}.\n" +
                "Columns (exact order): " + header + "\n\n" + prompt;

            string csv = await GenerateWithSelectedProvider(fullPrompt, provider);
            if (string.IsNullOrWhiteSpace(csv)) { Debug.LogError("❌ No response from provider."); return; }

            csv = EnsureCsvHasHeader(SanitizeToCsv(csv), header);
            csv = TryRepairCsvRows(csv, header); // attempt to auto-fix common LLM omissions
            if (!TryValidateCsv(csv, header, out var validationError))
            {
                ReportCsvError(validationError, csv);
                return;
            }

            AssistantPaths.EnsureDirectoryForFile(savePath);
            System.IO.File.WriteAllText(savePath, csv);
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(savePath);
#endif
            CharacterDatabase.LoadCharacters(savePath);
            Debug.Log($"✅ Beast masterlist saved to: {savePath}");
        }

        public static async Task GenerateSpiritMasterlistFromPrompt(string prompt, string savePath, AIProvider provider)
        {
            // Reuse character header
            const string header = CharacterHeader;

            string fullPrompt =
                "Generate Spirits (Type=Spirit) aligned with biblical angelology/guardian motifs strictly from scripture and related spiritual literature.\n" +
                "Rows only, no markdown. Keep names unique and descriptions concise.\n" +
                "Each entry has one Passive and three actives (A1–A3) plus an Ultimate.\n" +
                "For A1/A2/A3 provide Cost (uses ResourceType), Cooldown (sec), Range (m), Target from {Self,Ally,Enemy,Area,Cone,Line,Ground,Projectile}.\n" +
                "Columns (exact order): " + header + "\n\n" + prompt;

            string csv = await GenerateWithSelectedProvider(fullPrompt, provider);
            if (string.IsNullOrWhiteSpace(csv)) { Debug.LogError("❌ No response from provider."); return; }

            csv = EnsureCsvHasHeader(SanitizeToCsv(csv), header);
            csv = TryRepairCsvRows(csv, header); // attempt to auto-fix common LLM omissions
            if (!TryValidateCsv(csv, header, out var validationError))
            {
                ReportCsvError(validationError, csv);
                return;
            }

            AssistantPaths.EnsureDirectoryForFile(savePath);
            System.IO.File.WriteAllText(savePath, csv);
#if UNITY_EDITOR
            AssetDatabase.ImportAsset(savePath);
#endif
            CharacterDatabase.LoadCharacters(savePath);
            Debug.Log($"✅ Spirit masterlist saved to: {savePath}");
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
            if (!TryValidateCsv(csv, header, out var validationError))
            {
                ReportCsvError(validationError, csv);
                return;
            }

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
            if (!TryValidateCsv(csv, header, out var validationError))
            {
                ReportCsvError(validationError, csv);
                return;
            }

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
            if (!TryValidateCsv(csv, header, out var validationError))
            {
                ReportCsvError(validationError, csv);
                return;
            }

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
            if (!TryValidateCsv(csv, header, out var validationError))
            {
                ReportCsvError(validationError, csv);
                return;
            }

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

        // Attempt to repair common off-by-one column issues in character-like CSVs
        private static string TryRepairCsvRows(string csvWithHeader, string header)
        {
            // Only special-case repair for Character-like headers
            bool isCharacterLike = header == CharacterHeader;
            if (!isCharacterLike) return csvWithHeader;

            int expectedCols = header.Split(',').Length;
            var lines = csvWithHeader.Split('\n');
            if (lines.Length <= 1) return csvWithHeader;

            var repaired = new System.Text.StringBuilder();
            repaired.AppendLine(lines[0].TrimEnd('\r'));

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var colsList = new System.Collections.Generic.List<string>();
                foreach (var c in CSVUtility.SplitCsvLine(line)) colsList.Add(c?.Trim());

                // Helper: normalize common non-numeric cost tokens to 0
                void NormalizeCosts()
                {
                    int[] costIdx = { 44, 48, 52 };
                    foreach (var idx in costIdx)
                    {
                        if (idx < colsList.Count)
                        {
                            var v = (colsList[idx] ?? string.Empty).Trim();
                            if (string.Equals(v, "none", System.StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(v, "n/a", System.StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(v, "na", System.StringComparison.OrdinalIgnoreCase) ||
                                v == "-")
                            {
                                colsList[idx] = "0";
                            }
                        }
                    }
                }

                // Exact match -> still normalize costs then pass through
                if (colsList.Count == expectedCols)
                {
                    NormalizeCosts();
                    repaired.AppendLine(JoinCsv(colsList));
                    continue;
                }

                // 55 columns: commonly missing UltimateDescription at index 43
                if (colsList.Count == expectedCols - 1)
                {
                    int tenacityIndex = 34;
                    if (colsList.Count > tenacityIndex)
                    {
                        if (!TryParseFloat(colsList[tenacityIndex], out _))
                        {
                            colsList.Insert(tenacityIndex, "0");
                        }
                    }

                    // If still short, try fixing CritDamageMultiplier (index 28)
                    if (colsList.Count == expectedCols - 1)
                    {
                        int critDmgIndex = 28;
                        if (colsList.Count > critDmgIndex && !TryParseFloat(colsList[critDmgIndex], out _))
                        {
                            colsList.Insert(critDmgIndex, "2");
                        }
                    }

                    // If still short, assume missing UltimateDescription (index 43)
                    if (colsList.Count == expectedCols - 1)
                    {
                        int ultimateDescIndex = 43;
                        // Insert empty UltimateDescription to realign tail (cost/ability quartets)
                        if (ultimateDescIndex <= colsList.Count)
                            colsList.Insert(ultimateDescIndex, "");
                    }

                    // As a last resort, pad zeros at the end until count matches
                    while (colsList.Count < expectedCols) colsList.Add("0");

                    NormalizeCosts();
                    repaired.AppendLine(JoinCsv(colsList));
                    continue;
                }

                // 54 columns: often missing A3Range and A3Target at the end
                if (colsList.Count == expectedCols - 2)
                {
                    // Heuristic: append defaults to complete the last ability tuple
                    colsList.Add("0");      // A3Range default
                    colsList.Add("Self");   // A3Target default

                    // If still short, pad with zeros
                    while (colsList.Count < expectedCols) colsList.Add("0");

                    NormalizeCosts();
                    repaired.AppendLine(JoinCsv(colsList));
                    continue;
                }

                // If greater than expected or much shorter, leave as-is; validation will handle
                repaired.AppendLine(line.TrimEnd('\r'));
            }

            return repaired.ToString();
        }

        private static bool TryParseFloat(string s, out float value)
        {
            // Treat empty or null as failure to parse
            if (string.IsNullOrEmpty(s)) { value = 0f; return false; }
            return float.TryParse(s, out value);
        }

        private static string JoinCsv(System.Collections.Generic.IList<string> fields)
        {
            var parts = new System.Collections.Generic.List<string>(fields.Count);
            foreach (var f in fields)
            {
                parts.Add(CSVUtility.EscapeCsvField(f ?? string.Empty));
            }
            return string.Join(",", parts);
        }

        // Validate CSV structure before saving
        private static bool TryValidateCsv(string csvWithHeader, string header, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(csvWithHeader)) { error = "Empty CSV"; return false; }

            // Common API error bodies (JSON) detection
            var trimmed = csvWithHeader.TrimStart();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("[") || trimmed.ToLowerInvariant().Contains("\"error\""))
            {
                error = "Provider returned JSON error instead of CSV (check API key and model).";
                return false;
            }

            int expectedCols = header.Split(',').Length;
            var lines = csvWithHeader.Split('\n');
            bool hasDataRow = false;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (i == 0) continue; // header

                var cols = CSVUtility.SplitCsvLine(line);
                int count = 0; foreach (var _ in cols) count++;
                if (count < expectedCols)
                {
                    error = $"Row {i + 1} has {count}/{expectedCols} columns: '{line}'";
                    return false;
                }
                hasDataRow = true;
            }

            if (!hasDataRow)
            {
                error = "No data rows generated (only header). Try a stronger prompt or check API settings.";
                return false;
            }

            return true;
        }

        private static void ReportCsvError(string message, string raw)
        {
            Debug.LogError($"CSV validation failed: {message}\nRaw (truncated): {Truncate(raw, 1000)}");
#if UNITY_EDITOR
            EditorUtility.DisplayDialog("CSV Validation Failed", message + "\n\nCheck API settings and prompt.", "OK");
#endif
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
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
