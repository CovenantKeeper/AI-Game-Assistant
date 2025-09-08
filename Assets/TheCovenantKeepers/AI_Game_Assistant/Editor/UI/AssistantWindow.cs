using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using UnityEditor.UIElements;
using System.Reflection;
using UnityEditor.Animations;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor.UI
{
    public class AssistantWindow : EditorWindow
    {
        private EnumField masterListTypeField;
        private EnumField providerField;
        private VisualElement contentArea; // fallback only
        private VisualElement contentTop;
        private VisualElement contentBottom;
        private Label statusLabel;
        private Button openCsvButton;

        // Store loaded characters
        private List<CharacterData> characters;
        private string searchText = string.Empty;

        // Prompt buffer (shared by UI Toolkit control)
        private string promptBuffer = string.Empty;

        // EditorPrefs keys
        private const string PrefKeyType = "TCK_AI_MasterlistType";
        private const string PrefKeyProvider = "TCK_AI_Provider";
        private const string PrefKeyPrompt = "TCK_AI_LastPrompt";

        [MenuItem("The Covenant Keepers/AI Game Assistant/Assistant Window", priority = 0)]
        [MenuItem("Window/The Covenant Keepers/AI Game Assistant", priority = 2000)]
        public static void ShowWindow() => GetWindow<AssistantWindow>("AI Game Assistant");

        private void CreateGUI()
        {
#if UNITY_EDITOR
            AssistantPaths.EnsureAllFolders();
            AssistantPaths.MigrateLegacyCsvs();
            ChatGPTSettings.Get();
#endif
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssistantPaths.AssistantUxml);
            var root = rootVisualElement;

            Button generateButton = null;

            if (visualTree != null)
            {
                visualTree.CloneTree(root);

                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssistantPaths.AssistantUss);
                if (styleSheet != null) root.styleSheets.Add(styleSheet);

                masterListTypeField = root.Q<EnumField>("masterlist-type-field");
                providerField = root.Q<EnumField>("provider-field");
                // new content targets
                contentTop = root.Q<VisualElement>("content-top");
                contentBottom = root.Q<VisualElement>("content-bottom");
                contentArea = root.Q<VisualElement>("content-area"); // may be null in new layout

                // Build a single scrollable TextField in the host
                var promptHost = root.Q<VisualElement>("ai-prompt-host");
                if (promptHost != null)
                {
                    promptHost.Clear();
                    var sv = new ScrollView(ScrollViewMode.Vertical);
                    sv.style.height = 240;
                    sv.style.flexGrow = 0;
                    sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                    var tf = new TextField { multiline = true };
                    tf.name = "ai-prompt-field-runtime";
                    tf.style.whiteSpace = WhiteSpace.Normal;
                    tf.style.flexGrow = 1;
                    tf.style.minHeight = 220;
                    tf.value = EditorPrefs.GetString(PrefKeyPrompt, string.Empty);
                    promptBuffer = tf.value;
                    tf.RegisterValueChangedCallback(e =>
                    {
                        promptBuffer = e.newValue ?? string.Empty;
                        EditorPrefs.SetString(PrefKeyPrompt, promptBuffer);
                    });
                    sv.Add(tf);
                    promptHost.Add(sv);
                }

                generateButton = root.Q<Button>("generate-button");

                // optional status label bottom area
                statusLabel = new Label() { name = "status-label" };
                (contentBottom ?? root).Add(statusLabel);
            }

            // Fallbacks
            if (masterListTypeField == null)
            {
                masterListTypeField = new EnumField("Masterlist Type", MasterlistType.Character);
                root.Add(masterListTypeField);
            }
            if (providerField == null)
            {
                providerField = new EnumField("AI Provider", AIProvider.ChatGPT);
                root.Add(providerField);
            }
            if (contentTop == null && contentArea == null)
            {
                contentArea = new VisualElement { name = "content-area" };
                contentArea.style.flexGrow = 1;
                contentArea.style.marginTop = 6;
                root.Add(contentArea);
            }
            if (generateButton == null)
            {
                generateButton = new Button() { text = "Generate New List" };
                root.Add(generateButton);
            }
            if (statusLabel == null)
            {
                statusLabel = new Label() { name = "status-label" };
                statusLabel.style.marginTop = 4;
                (contentBottom ?? root).Add(statusLabel);
            }
            if (openCsvButton == null)
            {
                openCsvButton = new Button() { text = "Open CSV" };
                openCsvButton.clicked += () =>
                {
                    var type = (MasterlistType)(masterListTypeField?.value ?? MasterlistType.Character);
                    var path = GetSavePath(type);
                    if (File.Exists(path)) EditorUtility.RevealInFinder(path);
                    else EditorUtility.DisplayDialog("Not Found", $"No CSV at\n{path}", "OK");
                };
                (contentBottom ?? root).Add(openCsvButton);
            }

            // Add default prompt tools
            var promptToolsRow = new VisualElement { name = "prompt-tools-row" };
            promptToolsRow.style.flexDirection = FlexDirection.Row;
            promptToolsRow.style.marginTop = 4;

            var useDefaultPromptBtn = new Button() { text = "Use Default Prompt" };
            useDefaultPromptBtn.clicked += () =>
            {
                var t = (MasterlistType)(masterListTypeField?.value ?? MasterlistType.Character);
                promptBuffer = GetDefaultPromptForType(t);
                EditorPrefs.SetString(PrefKeyPrompt, promptBuffer);
                var runtimeTf = root.Q<TextField>("ai-prompt-field-runtime");
                if (runtimeTf != null) runtimeTf.value = promptBuffer;
            };
            promptToolsRow.Add(useDefaultPromptBtn);

            var clearPromptBtn = new Button() { text = "Clear" };
            clearPromptBtn.clicked += () =>
            {
                promptBuffer = string.Empty;
                EditorPrefs.SetString(PrefKeyPrompt, promptBuffer);
                var runtimeTf = root.Q<TextField>("ai-prompt-field-runtime");
                if (runtimeTf != null) runtimeTf.value = promptBuffer;
            };
            promptToolsRow.Add(clearPromptBtn);

            (contentBottom ?? root).Add(promptToolsRow);

            // Init dropdowns
            var savedType = (MasterlistType)EditorPrefs.GetInt(PrefKeyType, (int)MasterlistType.Character);
            masterListTypeField.Init(savedType);
            masterListTypeField.value = savedType;
            masterListTypeField.RegisterValueChangedCallback(evt =>
            {
                var t = (MasterlistType)evt.newValue;
                EditorPrefs.SetInt(PrefKeyType, (int)t);
                ShowSection(t);
            });

            var savedProvider = (AIProvider)EditorPrefs.GetInt(PrefKeyProvider, (int)AIProvider.ChatGPT);
            providerField.Init(savedProvider);
            providerField.value = savedProvider;
            providerField.RegisterValueChangedCallback(evt =>
            {
                var p = (AIProvider)evt.newValue;
                EditorPrefs.SetInt(PrefKeyProvider, (int)p);
            });

            generateButton.clicked += async () =>
            {
                EditorPrefs.SetString(PrefKeyPrompt, promptBuffer ?? string.Empty);

                generateButton.SetEnabled(false);
                statusLabel.text = "Generating...";
                try
                {
                    var type = (MasterlistType)(masterListTypeField?.value ?? MasterlistType.Character);
                    var providerEnum = providerField?.value is AIProvider p ? p : AIProvider.ChatGPT;
                    var prompt = promptBuffer ?? string.Empty;

                    string savePath = GetSavePath(type);
                    await GenerateCsvAsync(type, prompt, savePath, providerEnum);
#if UNITY_EDITOR
                    AssetDatabase.ImportAsset(savePath);
                    AssetDatabase.Refresh();
#endif
                    ShowSection(type);
                    statusLabel.text = $"Saved: {savePath}";
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"CSV generation failed: {ex.Message}");
                    statusLabel.text = "Error: see Console";
                }
                finally
                {
                    generateButton.SetEnabled(true);
                }
            };

            ShowSection(savedType);
        }

        private static string GetSavePath(MasterlistType type)
        {
            switch (type)
            {
                case MasterlistType.Character: return AssistantPaths.GeneratedCharacterCsv;
                case MasterlistType.Item: return AssistantPaths.GeneratedItemCsv;
                case MasterlistType.Ability: return AssistantPaths.GeneratedAbilityCsv;
                case MasterlistType.Quest: return AssistantPaths.GeneratedQuestCsv;
                case MasterlistType.Location: return AssistantPaths.GeneratedLocationCsv;
                case MasterlistType.Beast: return AssistantPaths.GeneratedBeastCsv;
                case MasterlistType.Spirit: return AssistantPaths.GeneratedSpiritCsv;
                default: return AssistantPaths.GeneratedCharacterCsv;
            }
        }

        private static Task GenerateCsvAsync(MasterlistType type, string prompt, string savePath, AIProvider provider)
        {
            switch (type)
            {
                case MasterlistType.Character: return PromptProcessor.GenerateCharacterMasterlistFromPrompt(prompt, savePath, provider);
                case MasterlistType.Item: return PromptProcessor.GenerateItemMasterlistFromPrompt(prompt, savePath, provider);
                case MasterlistType.Ability: return PromptProcessor.GenerateAbilityMasterlistFromPrompt(prompt, savePath, provider);
                case MasterlistType.Quest: return PromptProcessor.GenerateQuestMasterlistFromPrompt(prompt, savePath, provider);
                case MasterlistType.Location: return PromptProcessor.GenerateLocationMasterlistFromPrompt(prompt, savePath, provider);
                case MasterlistType.Beast: return PromptProcessor.GenerateBeastMasterlistFromPrompt(prompt, savePath, provider);
                case MasterlistType.Spirit: return PromptProcessor.GenerateSpiritMasterlistFromPrompt(prompt, savePath, provider);
            }
            return Task.CompletedTask;
        }

        // Provides a strong default prompt per feature/type
        private static string GetDefaultPromptForType(MasterlistType type)
        {
            switch (type)
            {
                case MasterlistType.Spirit:
                    return "Generate exactly 6 Spirits (Type=Spirit) aligned with biblical angelology/guardian motifs. Output rows only (no header, no code, no labels). Each row must have exactly 56 comma-separated values. Rules: Use ASCII quotes only; quote fields containing commas; no spaces around commas. Keep Type=Spirit. ResourceType from {Stamina,Focus,Spirit}. Targets from {Self,Ally,Enemy,Area,Cone,Line,Ground,Projectile}. Numbers: Health/Mana/Attack/Defense/Magic/Strength/Agility/Intelligence/Armor/MagicResist/AbilityXCost are integers. Speed/AttackSpeed/MoveSpeed/Range/CritChance/CritDamageMultiplier/ArmorPenetration/MagicPenetration/LifeSteal/SpellVamp/CooldownReduction/Tenacity/AbilityXCooldown/AbilityXRange are decimals (dot). CritChance/LifeSteal/SpellVamp/CDR/Tenacity in 0..1; CritDamageMultiplier >= 1. Include UltimateDescription after Ability3Description, then A1/A2/A3 fields in the quartet order: Cost,Cooldown,Range,Target. Use 0 for any Cost that doesn’t apply; never write None; use \"\" if a text field is unknown. ModelPath must be \"Assets/Models/Spirits/<Name>.prefab\". Return only 6 CSV lines.";
                case MasterlistType.Beast:
                    return "Generate exactly 6 Beasts (Type=Beast) as companions/pets inspired by biblical/spiritual literature. Output rows only; 56 columns. Same numeric/target rules as Spirits. ResourceType from {Stamina,Focus,Spirit}. ModelPath must be \"Assets/Models/Beasts/<Name>.prefab\". Use 0 for N/A costs and \"\" for unknown text. Return only 6 lines.";
                case MasterlistType.Character:
                    return "Generate exactly 8 Characters (Type=Character) spanning roles (Tank, Support, Assassin, Mage, Fighter). Output rows only; 56 columns. Use same numeric/target rules. ModelPath must be \"Assets/Models/Characters/<Name>.prefab\". Use 0 for N/A costs; \"\" for unknown text. Ensure concise descriptions.";
                case MasterlistType.Item:
                    return "Generate 20 RPG items across ItemType {Weapon,Armor,Consumable,Accessory,Material,QuestItem}. Keep descriptions concise and game-ready. PrefabPath like \"Assets/Items/<ItemName>.prefab\". Numbers must be numeric; booleans true/false; leave \"\" if unknown.";
                case MasterlistType.Ability:
                    return "Generate 20 RPG abilities mixing Damage, Heal, Buff, Debuff, Utility. Keep AbilityName unique and concise. Use decimal dot for numerics. Provide placeholder VFX/SFX paths like \"Assets/VFX/<AbilityName>_Cast.prefab\". Rows only; follow column types strictly.";
                case MasterlistType.Quest:
                    return "Generate 12 quests with clear objectives, rewards, and lore hints. Regions should vary. PrefabPath like \"Assets/Quests/<Title>.prefab\". Rows only; concise text fields.";
                case MasterlistType.Location:
                    return "Generate 10 locations with varied Region/Type and short lore hooks. PrefabPath like \"Assets/Locations/<Name>.prefab\". Rows only; concise text fields.";
                default:
                    return string.Empty;
            }
        }

        private void ClearContent()
        {
            (contentTop ?? contentArea)?.Clear();
            (contentBottom)?.Clear();
        }

        private void ShowSection(MasterlistType type)
        {
            ClearContent();

            switch (type)
            {
                case MasterlistType.Character:
                    DrawCharacterListForPath(AssistantPaths.GeneratedCharacterCsv, "🧙 Character Masterlist");
                    break;
                case MasterlistType.Beast:
                    DrawCharacterListForPath(AssistantPaths.GeneratedBeastCsv, "🐾 Beast Masterlist");
                    break;
                case MasterlistType.Spirit:
                    DrawCharacterListForPath(AssistantPaths.GeneratedSpiritCsv, "👼 Spirit Masterlist");
                    break;
                case MasterlistType.Item:
                    (contentTop ?? contentArea).Add(new Label("🗡️ Item Masterlist Editor (WIP)"));
                    break;
                case MasterlistType.Ability:
                    (contentTop ?? contentArea).Add(new Label("✨ Ability Masterlist Editor (WIP)"));
                    break;
                case MasterlistType.Quest:
                    (contentTop ?? contentArea).Add(new Label("📜 Quest Masterlist Editor (WIP)"));
                    break;
                case MasterlistType.Location:
                    (contentTop ?? contentArea).Add(new Label("🌍 Location Masterlist Editor (WIP)"));
                    break;
            }
        }

        private void DrawCharacterListForPath(string csvPath, string title)
        {
            var header = new Label(title);
            header.AddToClassList("section-header");
            (contentTop ?? contentArea).Add(header);

            if (!File.Exists(csvPath))
            {
                var openBtn = new Button(() => EditorUtility.RevealInFinder(csvPath)) { text = "Open CSV" };
                (contentTop ?? contentArea).Add(openBtn);
                (contentTop ?? contentArea).Add(new Label($"No CSV found at: {csvPath}"));
                (contentTop ?? contentArea).Add(new Label("Use the CSV Generation section to generate a list."));
                return;
            }

            characters = CharacterDatabase.LoadCharacters(csvPath);

            // Header toolbar
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            var openCsv = new Button(() => EditorUtility.RevealInFinder(csvPath)) { text = "Open CSV" };
            toolbar.Add(openCsv);
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; toolbar.Add(spacer);
            var search = new ToolbarSearchField { value = searchText }; search.style.minWidth = 200;
            search.RegisterValueChangedCallback(evt => { searchText = evt.newValue ?? string.Empty; RepaintList(); });
            toolbar.Add(search);
            var expandAll = new Button() { text = "Expand All" };
            var collapseAll = new Button() { text = "Collapse All" };
            toolbar.Add(expandAll); toolbar.Add(collapseAll);

            // Bulk build button for filtered list
            var buildAllBtn = new Button() { text = "Build Prefabs (Filtered)" };
            buildAllBtn.clicked += () => BuildPrefabsForFiltered();
            toolbar.Add(buildAllBtn);

            (contentTop ?? contentArea).Add(toolbar);

            var listContainer = new VisualElement();
            (contentTop ?? contentArea).Add(listContainer);

            // Build filtered list and foldouts in same order for mapping selections
            var filteredList = string.IsNullOrEmpty(searchText)
                ? new List<CharacterData>(characters)
                : characters.Where(c =>
                        (!string.IsNullOrEmpty(c.Name) && c.Name.ToLowerInvariant().Contains(searchText.ToLowerInvariant())) ||
                        (!string.IsNullOrEmpty(c.Class) && c.Class.ToLowerInvariant().Contains(searchText.ToLowerInvariant())) ||
                        (!string.IsNullOrEmpty(c.Faction) && c.Faction.ToLowerInvariant().Contains(searchText.ToLowerInvariant()))
                  ).ToList();

            var foldouts = new List<Foldout>();

            if (filteredList.Count == 0)
            {
                listContainer.Add(new Label("No characters found."));
            }
            else
            {
                foreach (var character in filteredList)
                {
                    var fold = new Foldout { text = string.IsNullOrEmpty(character.Name) ? "<unnamed>" : $"{character.Name} — {character.Class} / {character.Faction}", value = false };
                    var inner = new VisualElement { style = { flexDirection = FlexDirection.Column, marginLeft = 6, marginTop = 4, marginBottom = 6 } };

                    var nameField = new TextField("Name") { value = character.Name };
                    nameField.RegisterValueChangedCallback(evt => { character.Name = evt.newValue; fold.text = string.IsNullOrEmpty(character.Name) ? "<unnamed>" : $"{character.Name} — {character.Class} / {character.Faction}"; });
                    inner.Add(nameField);
                    var classField = new TextField("Class") { value = character.Class }; classField.RegisterValueChangedCallback(evt => { character.Class = evt.newValue; fold.text = string.IsNullOrEmpty(character.Name) ? "<unnamed>" : $"{character.Name} — {character.Class} / {character.Faction}"; }); inner.Add(classField);

                    var factionField = new TextField("Faction") { value = character.Faction }; factionField.RegisterValueChangedCallback(evt => { character.Faction = evt.newValue; fold.text = string.IsNullOrEmpty(character.Name) ? "<unnamed>" : $"{character.Name} — {character.Class} / {character.Faction}"; }); inner.Add(factionField);

                    // Model/Prefab selection
                    var modelObj = string.IsNullOrEmpty(character.ModelPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(character.ModelPath);
                    var modelField = new ObjectField("Model / Prefab") { objectType = typeof(GameObject), allowSceneObjects = false, value = modelObj };
                    modelField.RegisterValueChangedCallback(e =>
                    {
                        var go = e.newValue as GameObject;
                        character.ModelPath = go != null ? AssetDatabase.GetAssetPath(go) : string.Empty;
                    });
                    inner.Add(modelField);

                    // Animator controller (optional per-character)
                    var animCtrlField = new ObjectField("Animator Controller") { objectType = typeof(RuntimeAnimatorController), allowSceneObjects = false, value = character.AnimatorController };
                    animCtrlField.RegisterValueChangedCallback(e =>
                    {
                        character.AnimatorController = e.newValue as RuntimeAnimatorController;
                    });
                    inner.Add(animCtrlField);

                    // Optional animation clip assignments
                    var clipFold = new Foldout { text = "Animation Clips" };
                    var idleField = new ObjectField("Idle") { objectType = typeof(AnimationClip), allowSceneObjects = false, value = character.IdleClip };
                    idleField.RegisterValueChangedCallback(e => character.IdleClip = e.newValue as AnimationClip);
                    clipFold.Add(idleField);
                    var walkField = new ObjectField("Walk") { objectType = typeof(AnimationClip), allowSceneObjects = false, value = character.WalkClip };
                    walkField.RegisterValueChangedCallback(e => character.WalkClip = e.newValue as AnimationClip);
                    clipFold.Add(walkField);
                    var runField = new ObjectField("Run") { objectType = typeof(AnimationClip), allowSceneObjects = false, value = character.RunClip };
                    runField.RegisterValueChangedCallback(e => character.RunClip = e.newValue as AnimationClip);
                    clipFold.Add(runField);
                    var a1Clip = new ObjectField("Ability 1") { objectType = typeof(AnimationClip), allowSceneObjects = false, value = character.Ability1Clip };
                    a1Clip.RegisterValueChangedCallback(e => character.Ability1Clip = e.newValue as AnimationClip);
                    clipFold.Add(a1Clip);
                    var a2Clip = new ObjectField("Ability 2") { objectType = typeof(AnimationClip), allowSceneObjects = false, value = character.Ability2Clip };
                    a2Clip.RegisterValueChangedCallback(e => character.Ability2Clip = e.newValue as AnimationClip);
                    clipFold.Add(a2Clip);
                    var a3Clip = new ObjectField("Ability 3") { objectType = typeof(AnimationClip), allowSceneObjects = false, value = character.Ability3Clip };
                    a3Clip.RegisterValueChangedCallback(e => character.Ability3Clip = e.newValue as AnimationClip);
                    clipFold.Add(a3Clip);
                    var ultClip = new ObjectField("Ultimate") { objectType = typeof(AnimationClip), allowSceneObjects = false, value = character.UltimateClip };
                    ultClip.RegisterValueChangedCallback(e => character.UltimateClip = e.newValue as AnimationClip);
                    clipFold.Add(ultClip);
                    inner.Add(clipFold);

                    var modelTools = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
                    var pingBtn = new Button(() =>
                    {
                        if (!string.IsNullOrEmpty(character.ModelPath))
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<Object>(character.ModelPath);
                            if (obj) EditorGUIUtility.PingObject(obj);
                        }
                    }) { text = "Ping" };
                    var revealBtn = new Button(() =>
                    {
                        if (!string.IsNullOrEmpty(character.ModelPath))
                            EditorUtility.RevealInFinder(character.ModelPath);
                    }) { text = "Reveal" };
                    var clearBtn = new Button(() => { character.ModelPath = string.Empty; modelField.value = null; }) { text = "Clear" };
                    modelTools.Add(pingBtn); modelTools.Add(revealBtn); modelTools.Add(clearBtn);
                    inner.Add(modelTools);

                    // Build Prefab actions row (per-character)
                    var buildRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
                    var buildBtn = new Button(() => BuildPrefabForCharacterName(character.Name, kind: "Player")) { text = "Build Prefab" };
                    buildRow.Add(buildBtn);
                    var buildEnemyBtn = new Button(() => BuildPrefabForCharacterName(character.Name, kind: "Enemy")) { text = "Build Enemy Prefab" };
                    buildRow.Add(buildEnemyBtn);
                    inner.Add(buildRow);

                    // Extended identity
                    var subClassField = new TextField("Sub-Class") { value = character.SubClass }; subClassField.RegisterValueChangedCallback(evt => character.SubClass = evt.newValue); inner.Add(subClassField);
                    var resourceField = new TextField("Resource") { value = character.ResourceType }; resourceField.RegisterValueChangedCallback(evt => character.ResourceType = evt.newValue); inner.Add(resourceField);

                    var statsRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var healthField = new IntegerField("Health") { value = character.Health }; healthField.style.minWidth = 150; healthField.style.marginRight = 8; healthField.RegisterValueChangedCallback(evt => character.Health = evt.newValue); statsRow.Add(healthField);
                    var manaField = new IntegerField("Mana") { value = character.Mana }; manaField.style.minWidth = 150; manaField.RegisterValueChangedCallback(evt => character.Mana = evt.newValue); statsRow.Add(manaField);
                    inner.Add(statsRow);

                    // Core combat rows
                    var row2 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var atkField = new IntegerField("Attack") { value = character.Attack }; atkField.style.minWidth = 120; atkField.RegisterValueChangedCallback(evt => character.Attack = evt.newValue); row2.Add(atkField);
                    var defField = new IntegerField("Defense") { value = character.Defense }; defField.style.minWidth = 120; defField.RegisterValueChangedCallback(evt => character.Defense = evt.newValue); row2.Add(defField);
                    var magField = new IntegerField("Magic") { value = character.Magic }; magField.style.minWidth = 120; magField.RegisterValueChangedCallback(evt => character.Magic = evt.newValue); row2.Add(magField);
                    inner.Add(row2);

                    var row3 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var armField = new IntegerField("Armor") { value = character.Armor }; armField.style.minWidth = 120; armField.RegisterValueChangedCallback(evt => character.Armor = evt.newValue); row3.Add(armField);
                    var mresField = new IntegerField("M.Resist") { value = character.MagicResist }; mresField.style.minWidth = 120; mresField.RegisterValueChangedCallback(evt => character.MagicResist = evt.newValue); row3.Add(mresField);
                    var asField = new FloatField("Atk Speed") { value = character.AttackSpeed }; asField.style.minWidth = 120; asField.RegisterValueChangedCallback(evt => character.AttackSpeed = evt.newValue); row3.Add(asField);
                    inner.Add(row3);

                    var row4 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var msField = new FloatField("Move Spd") { value = character.MoveSpeed }; msField.style.minWidth = 120; msField.RegisterValueChangedCallback(evt => character.MoveSpeed = evt.newValue); row4.Add(msField);
                    var rangeField = new FloatField("Range") { value = character.Range }; rangeField.style.minWidth = 120; rangeField.RegisterValueChangedCallback(evt => character.Range = evt.newValue); row4.Add(rangeField);
                    var speedField = new FloatField("Speed") { value = character.Speed }; speedField.style.minWidth = 120; speedField.RegisterValueChangedCallback(evt => character.Speed = evt.newValue); row4.Add(speedField);
                    inner.Add(row4);

                    var row5 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var critCField = new FloatField("Crit %") { value = character.CritChance }; critCField.style.minWidth = 120; critCField.RegisterValueChangedCallback(evt => character.CritChance = evt.newValue); row5.Add(critCField);
                    var critDField = new FloatField("Crit DMG") { value = character.CritDamageMultiplier }; critDField.style.minWidth = 120; critDField.RegisterValueChangedCallback(evt => character.CritDamageMultiplier = evt.newValue); row5.Add(critDField);
                    var cdrField = new FloatField("CDR") { value = character.CooldownReduction }; cdrField.style.minWidth = 120; cdrField.RegisterValueChangedCallback(evt => character.CooldownReduction = evt.newValue); row5.Add(cdrField);
                    inner.Add(row5);

                    var row6 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var penPField = new FloatField("Armor Pen") { value = character.ArmorPenetration }; penPField.style.minWidth = 120; penPField.RegisterValueChangedCallback(evt => character.ArmorPenetration = evt.newValue); row6.Add(penPField);
                    var penMField = new FloatField("Magic Pen") { value = character.MagicPenetration }; penMField.style.minWidth = 120; penMField.RegisterValueChangedCallback(evt => character.MagicPenetration = evt.newValue); row6.Add(penMField);
                    var tenaField = new FloatField("Tenacity") { value = character.Tenacity }; tenaField.style.minWidth = 120; tenaField.RegisterValueChangedCallback(evt => character.Tenacity = evt.newValue); row6.Add(tenaField);
                    inner.Add(row6);

                    var row7 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var lsField = new FloatField("Lifesteal") { value = character.LifeSteal }; lsField.style.minWidth = 120; lsField.RegisterValueChangedCallback(evt => character.LifeSteal = evt.newValue); row7.Add(lsField);
                    var svField = new FloatField("Spell Vamp") { value = character.SpellVamp }; svField.style.minWidth = 120; svField.RegisterValueChangedCallback(evt => character.SpellVamp = evt.newValue); row7.Add(svField);
                    inner.Add(row7);

                    // Ability names (optional quick fields)
                    var abilRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var pName = new TextField("Passive Name") { value = character.PassiveName }; pName.style.minWidth = 180; pName.RegisterValueChangedCallback(evt => character.PassiveName = evt.newValue); abilRow.Add(pName);
                    var a1 = new TextField("A1 Name") { value = character.Ability1Name }; a1.style.minWidth = 140; a1.RegisterValueChangedCallback(evt => character.Ability1Name = evt.newValue); abilRow.Add(a1);
                    var a2 = new TextField("A2 Name") { value = character.Ability2Name }; a2.style.minWidth = 140; a2.RegisterValueChangedCallback(evt => character.Ability2Name = evt.newValue); abilRow.Add(a2);
                    var a3 = new TextField("A3 Name") { value = character.Ability3Name }; a3.style.minWidth = 140; a3.RegisterValueChangedCallback(evt => character.Ability3Name = evt.newValue); abilRow.Add(a3);
                    inner.Add(abilRow);

                    // Per-ability stats
                    var a1Row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    a1Row.Add(new Label("A1") { style = { minWidth = 40, unityTextAlign = TextAnchor.MiddleLeft } });
                    var a1Cost = new IntegerField("Cost") { value = character.Ability1Cost }; a1Cost.style.minWidth = 100; a1Cost.RegisterValueChangedCallback(e => character.Ability1Cost = e.newValue); a1Row.Add(a1Cost);
                    var a1Cd = new FloatField("CD") { value = character.Ability1Cooldown }; a1Cd.style.minWidth = 90; a1Cd.RegisterValueChangedCallback(e => character.Ability1Cooldown = e.newValue); a1Row.Add(a1Cd);
                    var a1Range = new FloatField("Range") { value = character.Ability1Range }; a1Range.style.minWidth = 100; a1Range.RegisterValueChangedCallback(e => character.Ability1Range = e.newValue); a1Row.Add(a1Range);
                    var a1Target = new TextField("Target") { value = character.Ability1Target }; a1Target.style.minWidth = 120; a1Target.RegisterValueChangedCallback(e => character.Ability1Target = e.newValue); a1Row.Add(a1Target);
                    inner.Add(a1Row);

                    var a2Row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    a2Row.Add(new Label("A2") { style = { minWidth = 40, unityTextAlign = TextAnchor.MiddleLeft } });
                    var a2Cost = new IntegerField("Cost") { value = character.Ability2Cost }; a2Cost.style.minWidth = 100; a2Cost.RegisterValueChangedCallback(e => character.Ability2Cost = e.newValue); a2Row.Add(a2Cost);
                    var a2Cd = new FloatField("CD") { value = character.Ability2Cooldown }; a2Cd.style.minWidth = 90; a2Cd.RegisterValueChangedCallback(e => character.Ability2Cooldown = e.newValue); a2Row.Add(a2Cd);
                    var a2Range = new FloatField("Range") { value = character.Ability2Range }; a2Range.style.minWidth = 100; a2Range.RegisterValueChangedCallback(e => character.Ability2Range = e.newValue); a2Row.Add(a2Range);
                    var a2Target = new TextField("Target") { value = character.Ability2Target }; a2Target.style.minWidth = 120; a2Target.RegisterValueChangedCallback(e => character.Ability2Target = e.newValue); a2Row.Add(a2Target);
                    inner.Add(a2Row);

                    var a3Row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    a3Row.Add(new Label("A3") { style = { minWidth = 40, unityTextAlign = TextAnchor.MiddleLeft } });
                    var a3Cost = new IntegerField("Cost") { value = character.Ability3Cost }; a3Cost.style.minWidth = 100; a3Cost.RegisterValueChangedCallback(e => character.Ability3Cost = e.newValue); a3Row.Add(a3Cost);
                    var a3Cd = new FloatField("CD") { value = character.Ability3Cooldown }; a3Cd.style.minWidth = 90; a3Cd.RegisterValueChangedCallback(e => character.Ability3Cooldown = e.newValue); a3Row.Add(a3Cd);
                    var a3Range = new FloatField("Range") { value = character.Ability3Range }; a3Range.style.minWidth = 100; a3Range.RegisterValueChangedCallback(e => character.Ability3Range = e.newValue); a3Row.Add(a3Range);
                    var a3Target = new TextField("Target") { value = character.Ability3Target }; a3Target.style.minWidth = 120; a3Target.RegisterValueChangedCallback(e => character.Ability3Target = e.newValue); a3Row.Add(a3Target);
                    inner.Add(a3Row);

                    fold.Add(inner);
                    listContainer.Add(fold);
                    foldouts.Add(fold);
                }

                expandAll.clicked += () => { foreach (var f in foldouts) f.value = true; };
                collapseAll.clicked += () => { foreach (var f in foldouts) f.value = false; };
            }

            // Bottom area: actions & status + description editors
            var actionsBar = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
            var saveButton = new Button(() => { CharacterDatabase.SaveCharacters(characters, csvPath); EditorUtility.DisplayDialog("Saved", "Character Masterlist saved!", "OK"); }) { text = "💾 Save Changes" };
            actionsBar.Add(saveButton);
            (contentBottom ?? contentArea).Add(actionsBar);

            var details = new VisualElement { style = { marginTop = 8 } };
            (contentBottom ?? contentArea).Add(details);

            void RebuildDetails()
            {
                details.Clear();
                int selIndex = foldouts.FindIndex(f => f.value);
                if (selIndex < 0 || selIndex >= filteredList.Count) return;
                var ch = filteredList[selIndex];

                details.Add(new Label($"Editing Descriptions: {ch.Name}"));

                var pDesc = new TextField("Passive Description") { multiline = true, value = ch.PassiveDescription };
                pDesc.style.height = 120;
                details.Add(pDesc);

                var a1Desc = new TextField("A1 Description") { multiline = true, value = ch.Ability1Description }; a1Desc.style.height = 90; details.Add(a1Desc);
                var a2Desc = new TextField("A2 Description") { multiline = true, value = ch.Ability2Description }; a2Desc.style.height = 90; details.Add(a2Desc);
                var a3Desc = new TextField("A3 Description") { multiline = true, value = ch.Ability3Description }; a3Desc.style.height = 90; details.Add(a3Desc);

                var ultDesc = new TextField("Ultimate Description") { multiline = true, value = ch.UltimateDescription }; ultDesc.style.height = 100; details.Add(ultDesc);

                var lore = new TextField("Lore Background") { multiline = true, value = ch.LoreBackground }; lore.style.height = 140; details.Add(lore);

                pDesc.RegisterValueChangedCallback(e => ch.PassiveDescription = e.newValue);
                a1Desc.RegisterValueChangedCallback(e => ch.Ability1Description = e.newValue);
                a2Desc.RegisterValueChangedCallback(e => ch.Ability2Description = e.newValue);
                a3Desc.RegisterValueChangedCallback(e => ch.Ability3Description = e.newValue);
                ultDesc.RegisterValueChangedCallback(e => ch.UltimateDescription = e.newValue);
                lore.RegisterValueChangedCallback(e => ch.LoreBackground = e.newValue);
            }

            foreach (var f in foldouts)
                f.RegisterValueChangedCallback(_ => RebuildDetails());

            void RepaintList()
            {
                ShowSection((MasterlistType)(masterListTypeField?.value ?? MasterlistType.Character));
            }

            RebuildDetails();
        }

        // -------- Prefab building helpers --------
        private static ScriptableObject FindCharacterSOByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var guids = AssetDatabase.FindAssets("t:CharacterData");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;
                var f = so.GetType().GetField("Name", BindingFlags.Public | BindingFlags.Instance);
                var value = f?.GetValue(so) as string;
                if (string.Equals((value ?? string.Empty).Trim(), name.Trim(), System.StringComparison.InvariantCultureIgnoreCase))
                    return so;
            }
            return null;
        }

        private static void BuildPrefabForCharacterName(string name, string kind)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                EditorUtility.DisplayDialog("Build Prefab", "Character name is empty.", "OK");
                return;
            }

            var so = FindCharacterSOByName(name);
            if (so == null)
            {
                if (EditorUtility.DisplayDialog("ScriptableObject Missing",
                    $"No CharacterData ScriptableObject found for '{name}'.\n\nRun 'Data/Sync Character ScriptableObjects' first?",
                    "Run Sync", "Cancel"))
                {
                    // Run sync using current generated CSV if available
                    var csv = AssistantPaths.GeneratedCharacterCsv;
                    if (File.Exists(csv))
                    {
                        var list = CharacterDatabase.LoadCharacters(csv);
                        TheCovenantKeepers.AI_Game_Assistant.Editor.ScriptableObjectSync.SyncCharacterScriptableObjects(list);
                        AssetDatabase.Refresh();
                        so = FindCharacterSOByName(name);
                    }
                }
            }

            if (so == null)
            {
                EditorUtility.DisplayDialog("Build Prefab", $"Could not find ScriptableObject for '{name}'.", "OK");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Build Prefab", $"Building '{name}'...", 0.6f);
                var path = TheCovenantKeepers.AI_Game_Assistant.Editor.CharacterPrefabBuilder.BuildCharacterPrefab(so, kind);
                EditorUtility.ClearProgressBar();
                if (!string.IsNullOrEmpty(path))
                {
                    EditorUtility.RevealInFinder(path);
                    Debug.Log($"✅ Built prefab: {path}");
                }
                else
                {
                    EditorUtility.DisplayDialog("Build Prefab", "Build failed. See Console for details.", "OK");
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"Build Prefab failed for '{name}': {ex.Message}\n{ex}");
            }
        }

        private void BuildPrefabsForFiltered()
        {
            if (characters == null || characters.Count == 0) return;

            var filtered = string.IsNullOrEmpty(searchText)
                ? new List<CharacterData>(characters)
                : characters.Where(c =>
                        (!string.IsNullOrEmpty(c.Name) && c.Name.ToLowerInvariant().Contains(searchText.ToLowerInvariant())) ||
                        (!string.IsNullOrEmpty(c.Class) && c.Class.ToLowerInvariant().Contains(searchText.ToLowerInvariant())) ||
                        (!string.IsNullOrEmpty(c.Faction) && c.Faction.ToLowerInvariant().Contains(searchText.ToLowerInvariant()))
                  ).ToList();

            int i = 0;
            foreach (var ch in filtered)
            {
                i++;
                try
                {
                    EditorUtility.DisplayProgressBar("Build Prefabs", $"{i}/{filtered.Count}: {ch.Name}", (float)i / filtered.Count);
                    BuildPrefabForCharacterName(ch.Name, "Player");
                }
                catch { }
                finally { EditorUtility.ClearProgressBar(); }
            }
        }
    }

    public enum MasterlistType { Character, Item, Ability, Quest, Location, Beast, Spirit }
}
