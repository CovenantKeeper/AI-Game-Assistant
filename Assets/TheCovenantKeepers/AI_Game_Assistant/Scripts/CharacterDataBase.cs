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

            // Skip header
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var cols = CSVUtility.SplitCsvLine(lines[i]).ToArray();
                var data = new CharacterData
                {
                    Name = cols.Length > 0 ? cols[0] : string.Empty,
                    Type = cols.Length > 1 ? cols[1] : string.Empty,
                    Role = cols.Length > 2 ? cols[2] : string.Empty,
                    Affiliation = cols.Length > 3 ? cols[3] : string.Empty,
                    Class = cols.Length > 4 ? cols[4] : string.Empty,
                    Faction = cols.Length > 5 ? cols[5] : string.Empty,
                    Element = cols.Length > 6 ? cols[6] : string.Empty,
                    Gender = cols.Length > 7 ? cols[7] : string.Empty,
                    Health = cols.Length > 8 ? int.Parse(cols[8]) : 0,
                    Mana = cols.Length > 9 ? int.Parse(cols[9]) : 0,
                    Attack = cols.Length > 10 ? int.Parse(cols[10]) : 0,
                    Defense = cols.Length > 11 ? int.Parse(cols[11]) : 0,
                    Magic = cols.Length > 12 ? int.Parse(cols[12]) : 0,
                    Speed = cols.Length > 13 ? int.Parse(cols[13]) : 0,
                    UltimateAbility = cols.Length > 14 ? cols[14] : string.Empty,
                    LoreBackground = cols.Length > 15 ? cols[15] : string.Empty,
                    ModelPath = cols.Length > 16 ? cols[16] : string.Empty
                };

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

            // Header row
            rows.Add("Name,Type,Role,Affiliation,Class,Faction,Element,Gender,Health,Mana,Attack,Defense,Magic,Speed,UltimateAbility,LoreBackground,ModelPath");

            foreach (var c in characters)
            {
                rows.Add(ToCsvRow(c));
            }

            File.WriteAllLines(path, rows);
            Debug.Log($"[CharacterDatabase] Saved {characters.Count} characters to {path}");
        }

        /// <summary>
        /// Converts a character to a CSV row string.
        /// </summary>
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
                c.ModelPath
            });
        }
    }
}
