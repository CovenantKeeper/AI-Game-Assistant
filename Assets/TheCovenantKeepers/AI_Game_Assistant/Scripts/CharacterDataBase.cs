using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    public static class CharacterDatabase
    {
        /// <summary>
        /// Loads a character list from a CSV file.
        /// </summary>
        public static List<CharacterData> LoadCharacters(string path)
        {
            var characters = new List<CharacterData>();

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CharacterDatabase] No character CSV found at {path}");
                return characters;
            }

            string[] lines = File.ReadAllLines(path);
            if (lines.Length <= 1)
            {
                Debug.LogWarning($"[CharacterDatabase] Character CSV is empty or missing header at {path}");
                return characters;
            }

            // Determine header column count for optional fields
            var headerCols = CSVUtility.SplitCsvLine(lines[0]).ToArray();

            // Skip header
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var cols = CSVUtility.SplitCsvLine(lines[i]).ToArray();
                int ParseIntSafe(int idx)
                {
                    if (idx >= cols.Length) return 0;
                    var s = cols[idx]?.Trim();
                    return int.TryParse(s, out var v) ? v : 0;
                }
                float ParseFloatSafe(int idx)
                {
                    if (idx >= cols.Length) return 0f;
                    var s = cols[idx]?.Trim();
                    return float.TryParse(s, out var v) ? v : 0f;
                }
                string ParseStr(int idx) => idx < cols.Length ? cols[idx]?.Trim() : string.Empty;

                var data = ScriptableObject.CreateInstance<CharacterData>();
                data.Name = ParseStr(0);
                data.Type = ParseStr(1);
                data.Role = ParseStr(2);
                data.Affiliation = ParseStr(3);
                data.Class = ParseStr(4);
                data.Faction = ParseStr(5);
                data.Element = ParseStr(6);
                data.Gender = ParseStr(7);
                data.Health = ParseIntSafe(8);
                data.Mana = ParseIntSafe(9);
                data.Attack = ParseIntSafe(10);
                data.Defense = ParseIntSafe(11);
                data.Magic = ParseIntSafe(12);
                data.Speed = ParseFloatSafe(13);
                data.UltimateAbility = ParseStr(14);
                data.LoreBackground = ParseStr(15);
                data.ModelPath = ParseStr(16);

                // Optional extended columns beyond index 16 (keep future compatibility)
                // Example mapping (if present): SubClass, ResourceType, Strength, Agility, Intelligence, Armor, MagicResist, AttackSpeed, MoveSpeed, Range, CritChance, CritDamageMultiplier, ArmorPenetration, MagicPenetration, LifeSteal, SpellVamp, CooldownReduction, Tenacity
                int idx = 17;
                if (headerCols.Length > idx) data.SubClass = ParseStr(idx); idx++;
                if (headerCols.Length > idx) data.ResourceType = ParseStr(idx); idx++;
                if (headerCols.Length > idx) data.Strength = ParseIntSafe(idx); idx++;
                if (headerCols.Length > idx) data.Agility = ParseIntSafe(idx); idx++;
                if (headerCols.Length > idx) data.Intelligence = ParseIntSafe(idx); idx++;
                if (headerCols.Length > idx) data.Armor = ParseIntSafe(idx); idx++;
                if (headerCols.Length > idx) data.MagicResist = ParseIntSafe(idx); idx++;
                if (headerCols.Length > idx) data.AttackSpeed = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.MoveSpeed = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.Range = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.CritChance = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.CritDamageMultiplier = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.ArmorPenetration = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.MagicPenetration = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.LifeSteal = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.SpellVamp = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.CooldownReduction = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.Tenacity = ParseFloatSafe(idx); idx++;

                // Ability names & descriptions (optional)
                if (headerCols.Length > idx) data.PassiveName = ParseStr(idx); idx++;
                if (headerCols.Length > idx) data.PassiveDescription = ParseStr(idx); idx++;
                if (headerCols.Length > idx) data.Ability1Name = ParseStr(idx); idx++;
                if (headerCols.Length > idx) data.Ability1Description = ParseStr(idx); idx++;
                if (headerCols.Length > idx) data.Ability2Name = ParseStr(idx); idx++;
                if (headerCols.Length > idx) data.Ability2Description = ParseStr(idx); idx++;
                if (headerCols.Length > idx) data.Ability3Name = ParseStr(idx); idx++;
                if (headerCols.Length > idx) data.Ability3Description = ParseStr(idx); idx++;
                if (headerCols.Length > idx) data.UltimateDescription = ParseStr(idx); idx++;

                // Ability gameplay stats (optional)
                if (headerCols.Length > idx) data.Ability1Cost = ParseIntSafe(idx); idx++;
                if (headerCols.Length > idx) data.Ability1Cooldown = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.Ability1Range = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.Ability1Target = ParseStr(idx); idx++;

                if (headerCols.Length > idx) data.Ability2Cost = ParseIntSafe(idx); idx++;
                if (headerCols.Length > idx) data.Ability2Cooldown = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.Ability2Range = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.Ability2Target = ParseStr(idx); idx++;

                if (headerCols.Length > idx) data.Ability3Cost = ParseIntSafe(idx); idx++;
                if (headerCols.Length > idx) data.Ability3Cooldown = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.Ability3Range = ParseFloatSafe(idx); idx++;
                if (headerCols.Length > idx) data.Ability3Target = ParseStr(idx); idx++;

                characters.Add(data);
            }

            Debug.Log($"[CharacterDatabase] Loaded {characters.Count} characters from {path}");
            return characters;
        }

        /// <summary>
        /// Saves a list of characters to a CSV file.
        /// </summary>
        public static void SaveCharacters(List<CharacterData> characters, string path)
        {
            var rows = new List<string>();

            // Header row (base columns)
            string header = "Name,Type,Role,Affiliation,Class,Faction,Element,Gender,Health,Mana,Attack,Defense,Magic,Speed,UltimateAbility,LoreBackground,ModelPath" +
                            ",SubClass,ResourceType,Strength,Agility,Intelligence,Armor,MagicResist,AttackSpeed,MoveSpeed,Range,CritChance,CritDamageMultiplier,ArmorPenetration,MagicPenetration,LifeSteal,SpellVamp,CooldownReduction,Tenacity" +
                            ",PassiveName,PassiveDescription,Ability1Name,Ability1Description,Ability2Name,Ability2Description,Ability3Name,Ability3Description,UltimateDescription" +
                            ",Ability1Cost,Ability1Cooldown,Ability1Range,Ability1Target,Ability2Cost,Ability2Cooldown,Ability2Range,Ability2Target,Ability3Cost,Ability3Cooldown,Ability3Range,Ability3Target";
            rows.Add(header);

            foreach (var c in characters)
            {
                rows.Add(ToCsvRow(c));
            }

            File.WriteAllLines(path, rows);
            Debug.Log($"[CharacterDatabase] Saved {characters.Count} characters to {path}");
        }

        private static string ToCsvRow(CharacterData c)
        {
            return string.Join(",", new string[]
            {
                c.Name,
                c.Type,
                c.Role,
                c.Affiliation,
                c.Class,
                c.Faction,
                c.Element,
                c.Gender,
                c.Health.ToString(),
                c.Mana.ToString(),
                c.Attack.ToString(),
                c.Defense.ToString(),
                c.Magic.ToString(),
                c.Speed.ToString(),
                c.UltimateAbility,
                CSVUtility.EscapeCsvField(c.LoreBackground),
                c.ModelPath,
                c.SubClass,
                c.ResourceType,
                c.Strength.ToString(),
                c.Agility.ToString(),
                c.Intelligence.ToString(),
                c.Armor.ToString(),
                c.MagicResist.ToString(),
                c.AttackSpeed.ToString(),
                c.MoveSpeed.ToString(),
                c.Range.ToString(),
                c.CritChance.ToString(),
                c.CritDamageMultiplier.ToString(),
                c.ArmorPenetration.ToString(),
                c.MagicPenetration.ToString(),
                c.LifeSteal.ToString(),
                c.SpellVamp.ToString(),
                c.CooldownReduction.ToString(),
                c.Tenacity.ToString(),
                CSVUtility.EscapeCsvField(c.PassiveName),
                CSVUtility.EscapeCsvField(c.PassiveDescription),
                CSVUtility.EscapeCsvField(c.Ability1Name),
                CSVUtility.EscapeCsvField(c.Ability1Description),
                CSVUtility.EscapeCsvField(c.Ability2Name),
                CSVUtility.EscapeCsvField(c.Ability2Description),
                CSVUtility.EscapeCsvField(c.Ability3Name),
                CSVUtility.EscapeCsvField(c.Ability3Description),
                CSVUtility.EscapeCsvField(c.UltimateDescription),
                c.Ability1Cost.ToString(),
                c.Ability1Cooldown.ToString(),
                c.Ability1Range.ToString(),
                CSVUtility.EscapeCsvField(c.Ability1Target),
                c.Ability2Cost.ToString(),
                c.Ability2Cooldown.ToString(),
                c.Ability2Range.ToString(),
                CSVUtility.EscapeCsvField(c.Ability2Target),
                c.Ability3Cost.ToString(),
                c.Ability3Cooldown.ToString(),
                c.Ability3Range.ToString(),
                CSVUtility.EscapeCsvField(c.Ability3Target)
            });
        }
    }

    // --- New wrappers for Beast and Spirit using the same Character schema ---
    public static class BeastDatabase
    {
        public static List<CharacterData> LoadBeasts(string path = null)
        {
            path = string.IsNullOrEmpty(path) ? AssistantPaths.GeneratedBeastCsv : path;
            return CharacterDatabase.LoadCharacters(path);
        }

        public static void SaveBeasts(List<CharacterData> beasts, string path = null)
        {
            path = string.IsNullOrEmpty(path) ? AssistantPaths.GeneratedBeastCsv : path;
            CharacterDatabase.SaveCharacters(beasts, path);
        }
    }

    public static class SpiritDatabase
    {
        public static List<CharacterData> LoadSpirits(string path = null)
        {
            path = string.IsNullOrEmpty(path) ? AssistantPaths.GeneratedSpiritCsv : path;
            return CharacterDatabase.LoadCharacters(path);
        }

        public static void SaveSpirits(List<CharacterData> spirits, string path = null)
        {
            path = string.IsNullOrEmpty(path) ? AssistantPaths.GeneratedSpiritCsv : path;
            CharacterDatabase.SaveCharacters(spirits, path);
        }
    }
}
