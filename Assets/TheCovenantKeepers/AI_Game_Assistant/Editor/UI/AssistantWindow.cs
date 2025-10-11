using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UObject = UnityEngine.Object;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor.UI
{
    // Add this ABOVE the AssistantWindow class, but inside the namespace
    public enum MasterlistType
    {
        Character = 0,
        Item = 1,
        Ability = 2,
        Quest = 3,
        Location = 4,
        Beast = 5,
        Spirit = 6
    }


    public class AssistantWindow : EditorWindow
    {

        // Animation Lab state
        // Animation Lab: auto-sync option
        private Label _animLastSyncedLabel;
        private bool _animAutoSyncFromCharacterData = true;
        private ScriptableObject _animTemplatePreset;
        private string _animPresetFolder = "Assets/TheCovenantKeepers/AI_Game_Assistant/AnimatorPresets";
        private string _animControllerFolder = "Assets/TheCovenantKeepers/AI_Game_Assistant/AnimatorControllers";
        private bool _animSkipExisting = true, _animClearBeforeFill = true, _animAutofillExisting = true, _animOverwriteExisting = false;

        // UI refs (optional)
        private ObjectField _animTemplateField, _animPreviewClipField, _animCharacterSOField;
        private TextField _animClassesCsv, _animName, _animClassKey, _animModelPath;
        private ObjectField _animModelPrefabField;

        // Tabs & Panels
        private VisualElement _tabBar;
        // Tabs & Panels (safe placeholders)
        private UnityEngine.UIElements.VisualElement _panelMasterlists;
        private UnityEngine.UIElements.VisualElement _panelAnimationLab;
        private UnityEngine.UIElements.VisualElement _panelEffects;
        private UnityEngine.UIElements.VisualElement _panelScripts;

        // Preview
        private UnityEditor.Editor _previewEditor;
        private GameObject _previewInstance;
        private UnityEditor.PreviewRenderUtility _preview;
        private UnityEngine.Vector2 _previewDrag;
        private bool _clearClipsBeforeAutofill = true; // default ON
        private EnumField masterListTypeField;
        private EnumField providerField;
        private VisualElement contentArea; // fallback only
        private VisualElement contentTop;
        private VisualElement contentBottom;
        private Label statusLabel;
        private Button openCsvButton;
        // --- Animation Lab Preview state ---
        private PreviewRenderUtility _previewUtil;
        private GameObject _previewGO;
        private Light _previewLight;
        private Image _previewImage;
        private RenderTexture _previewRT;

        private AnimationClip _currentClip;
        private double _clipStartTime;
        private bool _isPlayingClip;


        // Typed-list options
        private bool _skipExistingPresets = true; // default ON

        // Store loaded characters
        private List<CharacterData> characters;
        private string searchText = string.Empty;

        // Prompt buffer (shared by UI Toolkit control)
        private string promptBuffer = string.Empty;

        // EditorPrefs keys
        private const string PrefKeyType = "TCK_AI_MasterlistType";
        private const string PrefKeyProvider = "TCK_AI_Provider";
        private const string PrefKeyPrompt = "TCK_AI_LastPrompt";



        //[MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Root + "/Assistant Window", priority = 0)]
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
            root.Clear();

            if (visualTree != null)
            {
                visualTree.CloneTree(root);
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssistantPaths.AssistantUss);
                if (styleSheet != null) root.styleSheets.Add(styleSheet);
            }

            // In tabbed layout we don't use legacy hosts; ensure they are null
            masterListTypeField = null;
            providerField = null;
            contentArea = null;
            contentTop = null;
            contentBottom = null;
            statusLabel = null;
            openCsvButton = null;

            // Hosts from new UXML (fallback to root if not present)
            var tabHost = root.Q<VisualElement>("tabbar-host");
            var panelsRoot = root.Q<VisualElement>("panels-root") ?? root;

            // === Panels (one per tab) ===
            _panelMasterlists = new ScrollView() { style = { flexGrow = 1 } };
            _panelAnimationLab = new ScrollView() { style = { flexGrow = 1 } };
            _panelEffects     = new ScrollView() { style = { flexGrow = 1 } };
            _panelScripts     = new ScrollView() { style = { flexGrow = 1 } };

            // Mount panels
            panelsRoot.Add(_panelMasterlists);
            panelsRoot.Add(_panelAnimationLab);
            panelsRoot.Add(_panelEffects);
            panelsRoot.Add(_panelScripts);

            // Fill panels (content is created INSIDE each panel only)
            _panelMasterlists.Add(BuildMasterlistControlsGroup()); // Contains prompt/type/provider/generate/etc
            _panelAnimationLab.Add(BuildAnimationLabPanel());      // Animation-only UI
            _panelEffects.Add(BuildEffectsSidebarGroup());         // Effects-only UI
            _panelScripts.Add(BuildScriptPanel());                 // Scripts-only UI

            // Build tab bar and mount it
            _tabBar = BuildTabBar();
            if (tabHost != null) tabHost.Add(_tabBar);
            else root.Insert(0, _tabBar);

            // Show default tab
            ShowPanel(_panelMasterlists);
        }

        // Builds a titled group with the Effects/Spawner tools
        private VisualElement BuildEffectsSidebarGroup()
        {
            var box = new VisualElement { name = "effects-sidebar-group" };
            box.style.marginTop = 6;
            box.style.paddingTop = 6; box.style.paddingBottom = 6; box.style.paddingLeft = 6; box.style.paddingRight = 6;
            box.style.borderTopWidth = 1; box.style.borderBottomWidth = 1; box.style.borderLeftWidth = 1; box.style.borderRightWidth = 1;
            box.style.borderTopColor = new Color(0,0,0,0.3f); box.style.borderBottomColor = new Color(0,0,0,0.3f); box.style.borderLeftColor = new Color(0,0,0,0.3f); box.style.borderRightColor = new Color(0,0,0,0.3f);
            var title = new Label("Effects Tools");
            title.AddToClassList("section-header");
            box.Add(title);

            void AddButton(string text, System.Action onClick, string tooltip = null)
            {
                var btn = new Button(onClick) { text = text };
                if (!string.IsNullOrEmpty(tooltip)) btn.tooltip = tooltip;
                btn.style.marginTop = 2; btn.style.marginBottom = 2;
                box.Add(btn);
            }

            AddButton("Create Effect SO", () => TheCovenantKeepers.AI_Game_Assistant.Editor.AbilityEffectTemplates.CreateEffectAsset(), "Create an AbilityEffect ScriptableObject.");
            AddButton("Spark VFX Prefab", () => TheCovenantKeepers.AI_Game_Assistant.Editor.AbilityEffectTemplates.CreateSparkPrefab());
            AddButton("Hit Puff VFX Prefab", () => TheCovenantKeepers.AI_Game_Assistant.Editor.AbilityEffectTemplates.CreateHitPuffPrefab());
            AddButton("Effect + Sample Pair", () => TheCovenantKeepers.AI_Game_Assistant.Editor.AbilityEffectTemplates.CreateEffectWithVfxPair(), "Create a simple cast+hit VFX pair and an AbilityEffect that references them.");
            AddButton("Add Spawner To Selected", AddAbilitySpawnerToSelection, "Adds an AbilityEffectSpawner to the selected GameObject and auto-assigns a spawn transform.");
            AddButton("Open Effects Folder", () => { var fxFolder = "Assets/TheCovenantKeepers/AI_Game_Assistant/Blueprints/Effects"; AssistantPaths.EnsureFolder(fxFolder); EditorUtility.RevealInFinder(fxFolder); });

            // New: Scene camera rig setup
            AddButton("Setup Scene Camera Rig", () => TheCovenantKeepers.AI_Game_Assistant.Editor.CameraRigBuilder.SetupSceneCameraRig(), "Create/ensure Main Camera with TckFollowCamera that follows the Player.");

            return box;
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

        // Show only the active panel
        private void ShowPanel(VisualElement active)
        {
            if (_panelMasterlists != null) _panelMasterlists.style.display = DisplayStyle.None;
            if (_panelAnimationLab != null) _panelAnimationLab.style.display = DisplayStyle.None;
            if (_panelEffects != null) _panelEffects.style.display = DisplayStyle.None;
            if (_panelScripts != null) _panelScripts.style.display = DisplayStyle.None;

            if (active != null) active.style.display = DisplayStyle.Flex;
        }

        // Simple tab bar
        private VisualElement BuildTabBar()
        {
            var bar = new VisualElement { name = "tckai-tabbar" };
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.marginBottom = 6;

            VisualElement AddTab(string title, VisualElement panel)
            {
                var b = new Button(() => ShowPanel(panel)) { text = title };
                b.style.marginRight = 4;
                bar.Add(b);
                return b;
            }

            AddTab("Masterlists", _panelMasterlists);
            AddTab("Animation", _panelAnimationLab);
            AddTab("Effects", _panelEffects);
            AddTab("Scripts", _panelScripts);
            return bar;
        }

        // Effects: add spawner on current selection
        private static void AddAbilitySpawnerToSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("Add Spawner", "Select a GameObject in the scene or a prefab instance.", "OK");
                return;
            }

            var spawner = go.GetComponent<TheCovenantKeepers.AI_Game_Assistant.AbilityEffectSpawner>();
            if (spawner == null) spawner = go.AddComponent<TheCovenantKeepers.AI_Game_Assistant.AbilityEffectSpawner>();

            var t = go.transform.Find("VFX_Spawn");
            if (t == null)
            {
                var marker = new GameObject("VFX_Spawn");
                marker.transform.SetParent(go.transform, false);
                marker.transform.localPosition = new Vector3(0f, 0.15f, 0.2f);
                t = marker.transform;
            }
            spawner.spawn = t;

            EditorUtility.SetDirty(go);
            EditorGUIUtility.PingObject(go);
            Debug.Log("✅ AbilityEffectSpawner added/updated on selection.");
        }

        // Masterlists panel (prompt/type/provider + content placeholders)
        private VisualElement BuildMasterlistControlsGroup()
        {
            var panel = new VisualElement { name = "masterlists-panel-root" };

            // Controls group
            var controls = new VisualElement(); controls.AddToClassList("group-box");
            {
                var hdr = new Label("Masterlist Controls"); hdr.AddToClassList("group-header"); controls.Add(hdr);

                // Masterlist type
                var savedType = (MasterlistType)EditorPrefs.GetInt(PrefKeyType, (int)MasterlistType.Character);
                masterListTypeField = new EnumField("Masterlist Type", savedType);
                masterListTypeField.RegisterValueChangedCallback(evt =>
                {
                    var t = (MasterlistType)evt.newValue;
                    EditorPrefs.SetInt(PrefKeyType, (int)t);
                    ShowSection(t);
                });
                controls.Add(masterListTypeField);

                // Provider
                var savedProv = (AIProvider)EditorPrefs.GetInt(PrefKeyProvider, (int)AIProvider.ChatGPT);
                providerField = new EnumField("AI Provider", savedProv);
                providerField.RegisterValueChangedCallback(evt =>
                {
                    var p = (AIProvider)evt.newValue;
                    EditorPrefs.SetInt(PrefKeyProvider, (int)p);
                });
                controls.Add(providerField);

                // Prompt
                controls.Add(new Label("Prompt"));
                var promptField = new TextField { multiline = true };
                promptField.style.minHeight = 120;
                promptField.value = EditorPrefs.GetString(PrefKeyPrompt, string.Empty);
                promptBuffer = promptField.value;
                promptField.RegisterValueChangedCallback(e =>
                {
                    promptBuffer = e.newValue ?? string.Empty;
                    EditorPrefs.SetString(PrefKeyPrompt, promptBuffer);
                });
                controls.Add(promptField);

                // Actions row
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 4 } };

                var btnGenerate = new Button(async () =>
                {
                    var type = (MasterlistType)(masterListTypeField?.value ?? MasterlistType.Character);
                    var providerEnum = providerField?.value is AIProvider p ? p : AIProvider.ChatGPT;
                    var prompt = promptBuffer ?? string.Empty;

                    if (statusLabel != null) statusLabel.text = "Generating...";
                    try
                    {
                        var savePath = GetSavePath(type);
                        await GenerateCsvAsync(type, prompt, savePath, providerEnum);
#if UNITY_EDITOR
                        AssetDatabase.ImportAsset(savePath);
                        AssetDatabase.Refresh();
#endif
                        ShowSection(type);
                        if (statusLabel != null) statusLabel.text = $"Saved: {savePath}";
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"CSV generation failed: {ex.Message}");
                        if (statusLabel != null) statusLabel.text = "Error: see Console";
                    }
                })
                { text = "Generate New List" };
                row.Add(btnGenerate);

                var btnOpenCsv = new Button(() =>
                {
                    var type = (MasterlistType)(masterListTypeField?.value ?? MasterlistType.Character);
                    var path = GetSavePath(type);
                    if (File.Exists(path)) EditorUtility.RevealInFinder(path);
                    else EditorUtility.DisplayDialog("Not Found", $"No CSV at\n{path}", "OK");
                })
                { text = "Open CSV" };
                row.Add(btnOpenCsv);

                var btnDefault = new Button(() =>
                {
                    var t = (MasterlistType)(masterListTypeField?.value ?? MasterlistType.Character);
                    promptBuffer = GetDefaultPromptForType(t);
                    EditorPrefs.SetString(PrefKeyPrompt, promptBuffer);
                    // Update visible prompt
                    var fields = controls.Query<TextField>().ToList();
                    var tf = fields.LastOrDefault();
                    if (tf != null) tf.value = promptBuffer;
                }) { text = "Use Default Prompt" };
                row.Add(btnDefault);

                var btnClear = new Button(() =>
                {
                    promptBuffer = string.Empty;
                    EditorPrefs.SetString(PrefKeyPrompt, promptBuffer);
                    var fields = controls.Query<TextField>().ToList();
                    var tf = fields.LastOrDefault();
                    if (tf != null) tf.value = string.Empty;
                }) { text = "Clear" };
                row.Add(btnClear);

                controls.Add(row);
            }
            panel.Add(controls);

            // Status
            statusLabel = new Label() { name = "status-label" };
            statusLabel.style.marginTop = 4;
            panel.Add(statusLabel);

            // Content areas used by ShowSection/DrawCharacterListForPath
            contentTop = new VisualElement { name = "content-top" };
            contentBottom = new VisualElement { name = "content-bottom" };
            panel.Add(contentTop);
            panel.Add(contentBottom);

            // Initial draw
            var tSaved = (MasterlistType)EditorPrefs.GetInt(PrefKeyType, (int)MasterlistType.Character);
            masterListTypeField.value = tSaved;
            providerField.value = (AIProvider)EditorPrefs.GetInt(PrefKeyProvider, (int)AIProvider.ChatGPT);
            ShowSection(tSaved);

            return panel;
        }

        // Minimal Animation tab (tidied)
        private VisualElement BuildAnimationLabPanel()
        {
            var root = new ScrollView();

            // ==== Setup ====
            var gSetup = new GroupBox { text = "Setup" };
            {
                var row1 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row1.Add(new Label("Template Preset:"));
                _animTemplateField = new ObjectField
                {
                    objectType = typeof(ScriptableObject),
                    allowSceneObjects = false,
                    value = _animTemplatePreset
                };
                _animTemplateField.RegisterValueChangedCallback(e => _animTemplatePreset = e.newValue as ScriptableObject);
                row1.Add(_animTemplateField);
                gSetup.Add(row1);

                var row2 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var presetPath = new TextField("Preset Folder") { value = _animPresetFolder };
                presetPath.RegisterValueChangedCallback(e => _animPresetFolder = e.newValue);
                var ctrlPath = new TextField("Controller Folder") { value = _animControllerFolder };
                ctrlPath.RegisterValueChangedCallback(e => _animControllerFolder = e.newValue);
                row2.Add(presetPath);
                row2.Add(new VisualElement { style = { width = 8 } });
                row2.Add(ctrlPath);
                gSetup.Add(row2);
            }
            root.Add(gSetup);

            // ==== Batch creation ====
            var gBatch = new GroupBox { text = "Batch: Animator Presets by Class" };
            {
                _animClassesCsv = new TextField("Classes (comma-separated)") { value = "Seraphim, Cherubim, Archangel" };
                gBatch.Add(_animClassesCsv);

                var rowT = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var skipToggle = new Toggle("Skip existing") { value = _animSkipExisting };
                skipToggle.tooltip = "Don't recreate existing presets";
                skipToggle.RegisterValueChangedCallback(e => _animSkipExisting = e.newValue);
                rowT.Add(skipToggle);

                rowT.Add(new VisualElement { style = { width = 12 } });

                var clearToggle = new Toggle("Clear clips before Auto-Fill") { value = _animClearBeforeFill };
                clearToggle.RegisterValueChangedCallback(e => _animClearBeforeFill = e.newValue);
                rowT.Add(clearToggle);
                gBatch.Add(rowT);

                var rowB = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                rowB.Add(new Button(() => CreateClassPresetBatch(_animClassesCsv.value, autoFill: false)) { text = "Create Presets" });
                rowB.Add(new VisualElement { style = { width = 8 } });
                rowB.Add(new Button(() => CreateClassPresetBatch(_animClassesCsv.value, autoFill: true)) { text = "Create + Auto-Fill" });
                gBatch.Add(rowB);
            }
            root.Add(gBatch);

            // ==== Per Character (clean) ====
            var gChar = new GroupBox { text = "Per Character" };
            {
                // Character SO selector
                var rowSO = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                _animCharacterSOField = new ObjectField("Character SO")
                {
                    objectType = typeof(ScriptableObject),
                    allowSceneObjects = false
                };
                _animCharacterSOField.RegisterValueChangedCallback(e =>
                {
                    // Only act for RpgCharacterSO
                    var chr = e.newValue as RpgCharacterSO;
                    if (chr != null)
                    {
                        LoadFieldsFromSO(chr);
                    }
                });
                rowSO.Add(_animCharacterSOField);
                gChar.Add(rowSO);

                // Row of editable fields (ModelPath removed from visible UI)
                var rowFields = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                _animName = new TextField("Name");
                _animClassKey = new TextField("Class");

                rowFields.Add(_animName);
                rowFields.Add(new VisualElement { style = { width = 8 } });
                rowFields.Add(_animClassKey);
                rowFields.Add(new VisualElement { style = { width = 8 } });

                // Hidden internal ModelPath field (not added to UI, keeps code compatibility)
                if (_animModelPath == null)
                    _animModelPath = new TextField("ModelPath");
                _animModelPath.style.display = DisplayStyle.None; // ensure not shown

                // Prefab selector
                _animModelPrefabField = new ObjectField("Model Prefab")
                {
                    objectType = typeof(GameObject),
                    allowSceneObjects = false
                };
                _animModelPrefabField.RegisterValueChangedCallback(e =>
                {
                    var go = e.newValue as GameObject;
                    if (go != null)
                    {
                        var path = AssetDatabase.GetAssetPath(go);
                        if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                        {
                            // Store internally
                            if (_animModelPath != null) _animModelPath.value = path;
                        }
                    }
                });
                rowFields.Add(_animModelPrefabField);
                gChar.Add(rowFields);

                // Options
                var rowOpts = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var autoFillToggle = new Toggle("Auto-Fill existing") { value = _animAutofillExisting };
                autoFillToggle.RegisterValueChangedCallback(e => _animAutofillExisting = e.newValue);
                rowOpts.Add(autoFillToggle);

                rowOpts.Add(new VisualElement { style = { width = 12 } });

                var overwriteToggle = new Toggle("Overwrite existing (delete + recreate)") { value = _animOverwriteExisting };
                overwriteToggle.RegisterValueChangedCallback(e => _animOverwriteExisting = e.newValue);
                rowOpts.Add(overwriteToggle);
                gChar.Add(rowOpts);

                // Row: sync options (put this above the Create/Update buttons)
                {
                    var rowSync = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var tSync = new Toggle("Auto-sync from CharacterData") { value = _animAutoSyncFromCharacterData };
                    tSync.RegisterValueChangedCallback(e => _animAutoSyncFromCharacterData = e.newValue);
                    rowSync.Add(tSync);

                    // Manual one-click sync (optional but handy)
                    rowSync.Add(new VisualElement { style = { width = 12, height = 1 } });
                    rowSync.Add(new Button(() => {
                        if (SyncSelectedChrFromCharacterData())
                            Debug.Log("[AnimationLab] Synced CHR from CharacterData.");
                        else
                            Debug.LogWarning("[AnimationLab] Sync failed or no matching CharacterData found.");
                    }) { text = "Sync Now" });

                    gChar.Add(rowSync);
                }

                // Action buttons
                var rowB = new VisualElement { style = { flexDirection = FlexDirection.Row } };

                rowB.Add(new Button(() =>
                {
                    // run on click
                    if (_animAutoSyncFromCharacterData)
                        SyncSelectedChrFromCharacterData();

                    EnsurePresetFromCharacterFields(autoFill: true);
                })
                {
                    text = "Create/Update Preset"
                });

                rowB.Add(new VisualElement { style = { width = 8 } });

                rowB.Add(new Button(() =>
                {
                    GenerateControllerFromCharacterFields();
                })
                {
                    text = "Generate Animator Controller"
                });

                rowB.Add(new Button(() => AutoFillClipsFromExplosiveFolder()) { text = "Auto-Fill Clips (ExplosiveLLC)" });


                gChar.Add(rowB);

                _animLastSyncedLabel = new Label("Last Sync: (none)")
                {
                    style = { marginTop = 4, unityFontStyleAndWeight = FontStyle.Italic }
                };
                gChar.Add(_animLastSyncedLabel);

            }
            root.Add(gChar);

            // ==== Preview ====
            var gPrev = new GroupBox { text = "Model Preview" };
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                _animPreviewClipField = new ObjectField("Preview Clip")
                {
                    objectType = typeof(AnimationClip),
                    allowSceneObjects = false
                };
                row.Add(_animPreviewClipField);
                row.Add(new VisualElement { style = { width = 8 } });
                row.Add(new Button(() => RefreshPreviewFromCharacterSO()) { text = "Load Model Preview" });
                row.Add(new VisualElement { style = { width = 8 } });
                row.Add(new Button(() => PlayPreviewClip()) { text = "Play Clip" });
                row.Add(new VisualElement { style = { width = 8 } });
                row.Add(new Button(() => DisposePreview()) { text = "Clear Preview" });
                gPrev.Add(row);

                _previewImage = new Image
                {
                    scaleMode = ScaleMode.ScaleToFit,
                    image = Texture2D.blackTexture
                };
                _previewImage.style.height = 300;
                _previewImage.style.marginTop = 6;
                _previewImage.style.backgroundColor = new Color(0.10f, 0.10f, 0.10f);
                gPrev.Add(_previewImage);
            }
            root.Add(gPrev);

            return root;
        }

        // Minimal Scripts tab
        private VisualElement BuildScriptPanel()
        {
            var panel = new VisualElement { name = "scripts-panel-root" };
            panel.Add(new Label("Script Generation (WIP)"));
            return panel;
        }

        // No-op data sync for now (keeps Masterlists independent)
        private static void PopulateCharactersFromScriptableObjects(IEnumerable<CharacterData> list)
        {
            // Intentionally left blank to avoid hard-coupling to project-specific SOs.
        }

        private static void SyncCharactersToScriptableObjects(IEnumerable<CharacterData> list)
        {
            // Intentionally left blank; add your project-specific sync here if desired.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
            PopulateCharactersFromScriptableObjects(characters);

            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            toolbar.style.flexWrap = Wrap.Wrap;

            var openCsv = new Button(() => EditorUtility.RevealInFinder(csvPath)) { text = "Open CSV" };
            toolbar.Add(openCsv);

            var spacer = new VisualElement(); spacer.style.flexGrow = 1; toolbar.Add(spacer);

            var search = new ToolbarSearchField { value = searchText }; search.style.minWidth = 200;
            search.RegisterValueChangedCallback(evt => { searchText = evt.newValue ?? string.Empty; RepaintList(); });
            toolbar.Add(search);

            var expandAll = new Button() { text = "Expand All" };
            var collapseAll = new Button() { text = "Collapse All" };
            toolbar.Add(expandAll); toolbar.Add(collapseAll);

            (contentTop ?? contentArea).Add(toolbar);

            var listContainer = new VisualElement();
            (contentTop ?? contentArea).Add(listContainer);

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

                    var classField = new TextField("Class") { value = character.Class };
                    classField.RegisterValueChangedCallback(evt => { character.Class = evt.newValue; fold.text = string.IsNullOrEmpty(character.Name) ? "<unnamed>" : $"{character.Name} — {character.Class} / {character.Faction}"; });
                    inner.Add(classField);

                    var factionField = new TextField("Faction") { value = character.Faction };
                    factionField.RegisterValueChangedCallback(evt => { character.Faction = evt.newValue; fold.text = string.IsNullOrEmpty(character.Name) ? "<unnamed>" : $"{character.Name} — {character.Class} / {character.Faction}"; });
                    inner.Add(factionField);

                    var modelObj = string.IsNullOrEmpty(character.ModelPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(character.ModelPath);
                    var modelField = new ObjectField("Model / Prefab") { objectType = typeof(GameObject), allowSceneObjects = false, value = modelObj };
                    modelField.RegisterValueChangedCallback(e =>
                    {
                        var go = e.newValue as GameObject;
                        character.ModelPath = go != null ? AssetDatabase.GetAssetPath(go) : string.Empty;
                    });
                    inner.Add(modelField);

                    var animCtrlField = new ObjectField("Animator Controller") { objectType = typeof(RuntimeAnimatorController), allowSceneObjects = false, value = character.AnimatorController };
                    animCtrlField.RegisterValueChangedCallback(e =>
                    {
                        character.AnimatorController = e.newValue as RuntimeAnimatorController;
                    });
                    inner.Add(animCtrlField);

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
                            var obj = AssetDatabase.LoadAssetAtPath<UObject>(character.ModelPath);
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

                    var subClassField = new TextField("Sub-Class") { value = character.SubClass }; subClassField.RegisterValueChangedCallback(evt => character.SubClass = evt.newValue); inner.Add(subClassField);
                    var resourceField = new TextField("Resource") { value = character.ResourceType }; resourceField.RegisterValueChangedCallback(evt => character.ResourceType = evt.newValue); inner.Add(resourceField);

                    var statsRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var healthField = new IntegerField("Health") { value = character.Health }; healthField.style.minWidth = 150; healthField.style.marginRight = 8; healthField.RegisterValueChangedCallback(evt => character.Health = evt.newValue); statsRow.Add(healthField);
                    var manaField = new IntegerField("Mana") { value = character.Mana }; manaField.style.minWidth = 150; manaField.RegisterValueChangedCallback(evt => character.Mana = evt.newValue); statsRow.Add(manaField);
                    inner.Add(statsRow);

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

                    var abilRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var pName = new TextField("Passive Name") { value = character.PassiveName }; pName.style.minWidth = 180; pName.RegisterValueChangedCallback(evt => character.PassiveName = evt.newValue); abilRow.Add(pName);
                    var a1 = new TextField("A1 Name") { value = character.Ability1Name }; a1.style.minWidth = 140; a1.RegisterValueChangedCallback(evt => character.Ability1Name = evt.newValue); abilRow.Add(a1);
                    var a2 = new TextField("A2 Name") { value = character.Ability2Name }; a2.style.minWidth = 140; a2.RegisterValueChangedCallback(evt => character.Ability2Name = evt.newValue); abilRow.Add(a2);
                    var a3 = new TextField("A3 Name") { value = character.Ability3Name }; a3.style.minWidth = 140; a3.RegisterValueChangedCallback(evt => character.Ability3Name = evt.newValue); abilRow.Add(a3);
                    inner.Add(abilRow);

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

            var actionsBar = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
            var saveButton = new Button(() =>
            {
                CharacterDatabase.SaveCharacters(characters, csvPath);
                SyncCharactersToScriptableObjects(characters);
                EditorUtility.DisplayDialog("Saved", "Character Masterlist saved and clips synced to ScriptableObjects.", "OK");
            }) { text = "💾 Save Changes" };
            actionsBar.Add(saveButton);
            (contentBottom ?? contentArea).Add(actionsBar);

            var btnSyncSOs = new Button(() =>
            {
                BuildCharacterSOsFromCsv(
                    csvPath: csvPath,
                    outputFolder: "Assets/TheCovenantKeepers/AI_Game_Assistant/ScriptableObjects/Characters"
                );
            })
            { text = "Create/Update Character SOs" };
            header.Add(btnSyncSOs);


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

        // ============ Batch creation ============
        private void CreateClassPresetBatch(string classesCsv, bool autoFill)
        {
            if (_animTemplatePreset == null)
            {
                EditorUtility.DisplayDialog("Animator Presets", "Assign a Template Preset in Setup.", "OK");
                return;
            }

            AssistantPaths.EnsureFolder(_animPresetFolder);

            var classKeys = (classesCsv ?? "")
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var classKey in classKeys)
            {
                var existing = FindPresetByClassKey(classKey, _animPresetFolder);
                if (existing != null)
                {
                    if (_animOverwriteExisting)
                    {
                        var p = AssetDatabase.GetAssetPath(existing);
                        if (!string.IsNullOrEmpty(p)) AssetDatabase.DeleteAsset(p);
                        existing = null;
                    }
                    else if (_animSkipExisting)
                    {
                        Debug.Log($"[Animator Presets] Skip: preset for '{classKey}' already exists.");
                        if (autoFill && _animAutofillExisting)
                        {
                            if (_animClearBeforeFill) ClearAllAnimationClipsOnPreset(existing);
                            TryAutoFillPresetClipsWithClass(existing, classKey);
                            EditorUtility.SetDirty(existing);
                        }
                        continue;
                    }
                }

                var assetName = $"RPG_{SanitizeFileName(classKey)}_Preset";
                var path = $"{_animPresetFolder}/{assetName}.asset";
                var preset = CreatePresetAssetFromTemplate(_animTemplatePreset, path);
                if (preset == null) { Debug.LogError($"[Animator Presets] Failed to create '{classKey}'."); continue; }

                SetIfExists(preset, "characterClassKey", classKey);
                SetIfExists(preset, "displayName", assetName);

                if (autoFill)
                {
                    if (_animClearBeforeFill) ClearAllAnimationClipsOnPreset(preset);
                    TryAutoFillPresetClipsWithClass(preset, classKey);
                }

                EditorUtility.SetDirty(preset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ============ Per-character creation ============
        private void EnsurePresetFromCharacterFields(bool autoFill)
        {
            var name = _animName?.value?.Trim();
            var classKey = _animClassKey?.value?.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(classKey))
            {
                EditorUtility.DisplayDialog("Animator Preset", "Enter both Name and Class.", "OK");
                return;
            }
            if (_animTemplatePreset == null)
            {
                EditorUtility.DisplayDialog("Animator Preset", "Assign a Template Preset in Setup.", "OK");
                return;
            }

            AssistantPaths.EnsureFolder(_animPresetFolder);

            var existing = FindPresetByClassKey(classKey, _animPresetFolder);
            if (existing != null && _animOverwriteExisting)
            {
                var p = AssetDatabase.GetAssetPath(existing);
                if (!string.IsNullOrEmpty(p)) AssetDatabase.DeleteAsset(p);
                existing = null;
            }
            else if (existing != null && _animSkipExisting)
            {
                Debug.Log($"[Animator Presets] Skip: preset for '{classKey}' already exists.");
                if (autoFill && _animAutofillExisting)
                {
                    if (_animClearBeforeFill) ClearAllAnimationClipsOnPreset(existing);
                    TryAutoFillPresetClipsWithClass(existing, classKey);
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                }
                Selection.activeObject = existing;
                return;
            }

            var assetName = $"RPG_{SanitizeFileName(classKey)}_Preset";
            var path = $"{_animPresetFolder}/{assetName}.asset";
            var preset = CreatePresetAssetFromTemplate(_animTemplatePreset, path);
            if (preset == null) { Debug.LogError("[Animator Presets] Failed to create preset."); return; }

            SetIfExists(preset, "characterClassKey", classKey);
            SetIfExists(preset, "displayName", assetName);

            if (autoFill)
            {
                if (_animClearBeforeFill) ClearAllAnimationClipsOnPreset(preset);
                TryAutoFillPresetClipsWithClass(preset, classKey);
            }

            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = preset;
        }
        private void GenerateControllerFromCharacterFields()
        {
            var chr = _animCharacterSOField?.value as RpgCharacterSO;
            if (chr == null)
            {
                Debug.LogWarning("[AnimationLab] No Character SO selected to generate controller from.");
                return;
            }

            // Path + asset
            string controllerName = $"{chr.characterName}_Controller";
            string controllerPath = Path.Combine(_animControllerFolder, $"{controllerName}.controller");

            // Create controller asset (overwrite if exists)
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller == null)
            {
                Debug.LogError("[AnimationLab] Failed to create Animator Controller.");
                return;
            }

            // Parameters
            void AddParam(string name, AnimatorControllerParameterType t)
            {
                if (!controller.parameters.Any(p => p.name == name))
                    controller.AddParameter(name, t);
            }
            AddParam("IsMoving", AnimatorControllerParameterType.Bool);
            AddParam("IsAttacking", AnimatorControllerParameterType.Bool);
            AddParam("IsDead", AnimatorControllerParameterType.Bool);
            AddParam("Speed", AnimatorControllerParameterType.Float);
            AddParam("TriggerAttack", AnimatorControllerParameterType.Trigger);

            // Base layer + state machine
            var layer = controller.layers[0];
            var sm = layer.stateMachine;

            // Nuke any template/placeholder states that may have carried over
            RemovePlaceholderStates(sm);

            // Clips (your existing resolver)
            var idle = GetChrClip(chr, "Idle");
            var walk = GetChrClip(chr, "Walk");
            var run = GetChrClip(chr, "Run");
            var jump = GetChrClip(chr, "Jump");
            var attack = GetChrClip(chr, "Attack");
            var hit = GetChrClip(chr, "Hit");
            var death = GetChrClip(chr, "Death");

            // Helper to add a state if we have a clip
            AnimatorState AddStateWithClip(string name, AnimationClip clip)
            {
                if (clip == null) return null;
                var st = sm.AddState(name);
                SetMotion(st, clip);            // IMPORTANT: clears any placeholder first
                return st;
            }

            // States
            var idleState = sm.AddState("Idle");
            SetMotion(idleState, idle);
            sm.defaultState = idleState;

            var walkState = AddStateWithClip("Walk", walk);
            var runState = AddStateWithClip("Run", run);
            var jumpState = AddStateWithClip("Jump", jump);
            var attackState = AddStateWithClip("Attack", attack);
            var hitState = AddStateWithClip("Hit", hit);
            var deathState = AddStateWithClip("Death", death);

            // Auto transitions back to Idle
            foreach (var s in sm.states)
            {
                if (s.state == idleState) continue;
                var t = s.state.AddTransition(idleState);
                t.hasExitTime = true;
                t.exitTime = 0.9f;
                t.hasFixedDuration = true;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimationLab] Animator Controller created: {controllerPath}");
        }

        private void RefreshPreviewFromModelPath()
        {
            var path = _animModelPath?.value?.Trim();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[AnimationLab] ModelPath is empty.");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            // Fallback 1: if path is a folder, search for a prefab inside it
            if (prefab == null && AssetDatabase.IsValidFolder(path))
            {
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
                if (guids != null && guids.Length > 0)
                {
                    var first = AssetDatabase.GUIDToAssetPath(guids[0]);
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(first);
                    if (prefab != null) path = first;
                }
            }

            // Fallback 2: if path is a partial or name, try a name search
            if (prefab == null && !path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(name))
                {
                    var guids = AssetDatabase.FindAssets($"{name} t:Prefab");
                    foreach (var g in guids)
                    {
                        var p = AssetDatabase.GUIDToAssetPath(g);
                        var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                        if (candidate != null)
                        {
                            prefab = candidate;
                            path = p;
                            break;
                        }
                    }
                }
            }

            if (prefab == null)
            {
                Debug.LogWarning($"No prefab at or under: {path}");
                return;
            }

            // … proceed to spawn preview instance from 'prefab'
            // (your existing preview utility code here)
        }

        private void PlayPreviewClip()
        {
            if (_previewGO == null)
            {
                Debug.LogWarning("[AnimationLab] Load Model Preview first.");
                return;
            }

            var clip = _animPreviewClipField?.value as AnimationClip;
            if (clip == null)
            {
                Debug.LogWarning("[AnimationLab] Assign a Preview Clip.");
                return;
            }

            _currentClip = clip;
            _clipStartTime = EditorApplication.timeSinceStartup;
            _isPlayingClip = true;

            EditorApplication.update -= OnPreviewUpdate;
            EditorApplication.update += OnPreviewUpdate;
        }

        private void DisposePreview()
        {
            AnimationMode.StopAnimationMode();
            if (_previewInstance != null) DestroyImmediate(_previewInstance);
            _previewInstance = null;
            if (_preview != null) { _preview.Cleanup(); _preview = null; }
        }

        private static string SanitizeFileName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Asset";
            foreach (var ch in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
            return s.Trim();
        }

        private static ScriptableObject CreatePresetAssetFromTemplate(ScriptableObject template, string path)
        {
            var inst = ScriptableObject.Instantiate(template);
            AssetDatabase.CreateAsset(inst, path);
            return inst;
        }

        private static ScriptableObject FindPresetByClassKey(string classKey, string folder)
        {
            var expected = $"{folder}/RPG_{SanitizeFileName(classKey)}_Preset.asset";
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(expected);
            if (so != null) return so;

            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".asset")) continue;
                var candidate = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (candidate == null) continue;

                var key = GetStr(candidate, "characterClassKey");
                if (!string.IsNullOrEmpty(key) && string.Equals(key.Trim(), classKey.Trim(), StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        private static void SetIfExists(object obj, string member, string value)
        {
            var t = obj.GetType();
            var f = t.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(string)) { f.SetValue(obj, value); return; }
            var p = t.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(string) && p.CanWrite) p.SetValue(obj, value);
        }

        private static string GetStr(object obj, string member)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            var f = t.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(string)) return f.GetValue(obj) as string;
            var p = t.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(string)) return p.GetValue(obj) as string;
            return null;
        }

        private static void ClearAllAnimationClipsOnPreset(ScriptableObject preset)
        {
            if (preset == null) return;
            var t = preset.GetType();
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(AnimationClip).IsAssignableFrom(f.FieldType)) f.SetValue(preset, null);
                else if (f.FieldType.IsArray && f.FieldType.GetElementType() == typeof(AnimationClip))
                    f.SetValue(preset, Array.CreateInstance(typeof(AnimationClip), 0));
            }
        }

        // Very simple matcher: finds clips whose names contain the class key and the action keyword.
        private static bool TryAutoFillPresetClipsWithClass(ScriptableObject preset, string classKey)
        {
            if (preset == null || string.IsNullOrWhiteSpace(classKey)) return false;

            var guids = AssetDatabase.FindAssets("t:AnimationClip");
            var clips = guids
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p))
                .Where(c => c != null && c.name.IndexOf(classKey, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (clips.Count == 0) return false;

            void set(string field, string token)
            {
                var f = preset.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null || !typeof(AnimationClip).IsAssignableFrom(f.FieldType)) return;
                var c = clips.FirstOrDefault(x => x.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
                if (c != null) f.SetValue(preset, c);
            }

            set("Idle", "Idle");
            set("Walk", "Walk");
            set("Run", "Run");
            set("Jump", "Jump");
            set("Fall", "Fall");
            set("Land", "Land");
            set("Attack1", "Attack");
            set("Hit", "Hit");
            set("Die", "Die");

            return true;
        }

        private void BuildCharacterSOsFromCsv(string csvPath, string outputFolder)
        {
            if (!File.Exists(csvPath))
            {
                EditorUtility.DisplayDialog("CSV → SO", $"CSV not found:\n{csvPath}", "OK");
                return;
            }

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                var parent = "Assets";
                foreach (var part in outputFolder.Replace('\\', '/').Split('/').Skip(1))
                {
                    var next = $"{parent}/{part}";
                    if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, part);
                    parent = next;
                }
            }

            // Pull the parsed characters your editor already uses
            var chars = CharacterDatabase.LoadCharacters(csvPath); // returns a list in your project
            if (chars == null || chars.Count == 0)
            {
                EditorUtility.DisplayDialog("CSV → SO", "No characters found in CSV.", "OK");
                return;
            }

            int created = 0, updated = 0;

            foreach (var c in chars)
            {
                var safeName = SanitizeFileName(c.Name);
                var assetPath = $"{outputFolder}/CHR_{safeName}.asset";

                // Create or load
                var so = AssetDatabase.LoadAssetAtPath<RpgCharacterSO>(assetPath);
                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<RpgCharacterSO>();
                    AssetDatabase.CreateAsset(so, assetPath);
                    created++;
                }
                else
                {
                    updated++;
                }

                // Map fields (adjust to your CharacterData model property names)
                so.characterName = c.Name;
                so.type = c.Type;
                so.role = c.Role;
                so.affiliation = c.Affiliation;
                so.@class = c.Class;
                so.faction = c.Faction;
                so.element = c.Element;
                so.gender = c.Gender;

                so.health = c.Health;
                so.mana = c.Mana;
                so.attack = c.Attack;
                so.defense = c.Defense;
                so.magic = c.Magic;
                so.speed = (int)c.Speed; // <-- Fix for CS0266

                so.ultimateAbility = c.UltimateAbility;
                so.loreBackground = c.LoreBackground;

                so.modelPath = c.ModelPath;
                // Remove these two lines unless you add the properties to CharacterData
                // so.animatorPresetPath = c.AnimatorPreset;
                // so.animatorControllerPath = c.AnimatorController;

                so.subClass = c.SubClass;
                so.resourceType = c.ResourceType;

                so.passiveName = c.PassiveName;
                so.passiveDescription = c.PassiveDescription;

                so.ability1Name = c.Ability1Name;
                so.ability1Description = c.Ability1Description;
                so.ability2Name = c.Ability2Name;
                so.ability2Description = c.Ability2Description;
                so.ability3Name = c.Ability3Name;
                so.ability3Description = c.Ability3Description;

                so.ultimateDescription = c.UltimateDescription;

                EditorUtility.SetDirty(so);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CSV → SO] Characters: {created} created, {updated} updated → {outputFolder}");
        }
        private static int ToInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            // remove non-digits like %, spaces, etc.
            var cleaned = new string(s.Where(char.IsDigit).ToArray());
            if (int.TryParse(cleaned, out var val)) return val;
            return 0;
        }
        // Put this once in AssistantWindow.cs
        private void LoadFieldsFromSO(ScriptableObject so)
        {
            if (so == null) return;

            // Try common names across CharacterData and RpgCharacterSO
            string name = GetStr(so, "Name") ?? GetStr(so, "characterName");
            string cls = GetStr(so, "Class") ?? GetStr(so, "@class") ?? GetStr(so, "classKey");
            string model = GetStr(so, "ModelPath") ?? GetStr(so, "modelPath");

            if (!string.IsNullOrEmpty(name) && _animName != null) _animName.value = name;
            if (!string.IsNullOrEmpty(cls) && _animClassKey != null) _animClassKey.value = cls;
            if (!string.IsNullOrEmpty(model) && _animModelPath != null) _animModelPath.value = model;

            // Sync prefab field if the path points to a prefab
            if (!string.IsNullOrEmpty(model) && _animModelPrefabField != null)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(model);
                _animModelPrefabField.value = go;
            }

            Debug.Log($"[AnimationLab] Loaded fields from {so.name} (class: {cls ?? "?"})");
        }


        // Helper: attempt to resolve a prefab from a path that might be:
        // - a direct prefab path
        // - a folder containing prefabs
        // - a partial/name (without .prefab)
        // Preference order:
        //   1. Exact prefab at path
        //   2. Single prefab in folder
        //   3. Prefab in folder whose name matches characterName or classKey
        //   4. Global search by name
        // Improved prefab resolver with scoring & diagnostics
        private GameObject ResolvePreviewPrefab(string modelPath, string characterName = null, string classKey = null)
        {
            if (string.IsNullOrEmpty(modelPath))
                return null;

            // Normalize path slashes
            modelPath = modelPath.Replace('\\', '/');

            // 1. Direct prefab?
            if (modelPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                var direct = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (direct != null)
                {
                    // Direct success
                    return direct;
                }
            }

            // Local helper: compute a score for a prefab asset path.
            int Score(string assetPath, string nameOnly, string charName, string clsKey)
            {
                int score = 0;
                if (!string.IsNullOrEmpty(charName))
                {
                    if (string.Equals(nameOnly, charName, StringComparison.OrdinalIgnoreCase)) score += 100;
                    else if (nameOnly.IndexOf(charName, StringComparison.OrdinalIgnoreCase) >= 0) score += 40;
                }
                if (!string.IsNullOrEmpty(clsKey))
                {
                    if (string.Equals(nameOnly, clsKey, StringComparison.OrdinalIgnoreCase)) score += 90;
                    else if (nameOnly.IndexOf(clsKey, StringComparison.OrdinalIgnoreCase) >= 0) score += 30;
                }
                // Mild bias toward shorter names (often more specific root objects)
                score -= Math.Max(0, nameOnly.Length - 20);
                // Slight boost if under a "Prefabs" folder
                if (assetPath.IndexOf("/Prefabs/", StringComparison.OrdinalIgnoreCase) >= 0) score += 10;
                return score;
            }

            // Collect candidate prefab GUIDs given a folder context.
            List<string> candidatePaths = new List<string>();

            bool pathIsFolder = AssetDatabase.IsValidFolder(modelPath);
            if (pathIsFolder)
            {
                // Search recursively under the folder
                var folderGuids = AssetDatabase.FindAssets("t:Prefab", new[] { modelPath });
                foreach (var g in folderGuids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                        candidatePaths.Add(p);
                }
            }
            else
            {
                // If not a folder and not a direct prefab, treat as name hint or a parent folder chain
                // Try parent directory if it exists
                var dir = Path.GetDirectoryName(modelPath);
                if (!string.IsNullOrEmpty(dir) && AssetDatabase.IsValidFolder(dir))
                {
                    var dirUnix = dir.Replace('\\', '/');
                    var folderGuids = AssetDatabase.FindAssets("t:Prefab", new[] { dirUnix });
                    foreach (var g in folderGuids)
                    {
                        var p = AssetDatabase.GUIDToAssetPath(g);
                        if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                            candidatePaths.Add(p);
                    }
                }

                // Global name search fallback
                var nameHint = Path.GetFileNameWithoutExtension(modelPath);
                if (!string.IsNullOrEmpty(nameHint))
                {
                    var nameGuids = AssetDatabase.FindAssets($"{nameHint} t:Prefab");
                    foreach (var g in nameGuids)
                    {
                        var p = AssetDatabase.GUIDToAssetPath(g);
                        if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && !candidatePaths.Contains(p))
                            candidatePaths.Add(p);
                    }
                }
            }

            if (candidatePaths.Count == 0)
                return null;

            if (candidatePaths.Count == 1)
                return AssetDatabase.LoadAssetAtPath<GameObject>(candidatePaths[0]);

            // Score candidates
            var scored = candidatePaths
                .Select(p =>
                {
                    var nameOnly = Path.GetFileNameWithoutExtension(p);
                    return new
                    {
                        Path = p,
                        Score = Score(p, nameOnly, characterName, classKey)
                    };
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Path.Length)
                .ToList();

            // If all scores are zero, we still pick the first (deterministic order).
            var best = scored.First();
            // Log diagnostic once for visibility
            Debug.Log($"[AnimationLab] Resolved prefab (score {best.Score}) from '{modelPath}' → '{best.Path}' (candidates: {scored.Count})");
            return AssetDatabase.LoadAssetAtPath<GameObject>(best.Path);
        }

        // CHANGED: More robust resolution (folder/name fallback) + better diagnostics.
        private void RefreshPreviewFromCharacterSO()
        {
            // 1) Prefer the Model Prefab field if it’s set
            if (_animModelPrefabField?.value is GameObject directPrefab)
            {
                // sync hidden path for other code that uses it
                var directPath = AssetDatabase.GetAssetPath(directPrefab);
                if (!string.IsNullOrEmpty(directPath) && _animModelPath != null)
                    _animModelPath.value = directPath;

                // render directly — no need to resolve via path
                DisposePreview();
                EnsurePreviewUtility();

                _previewGO = (GameObject)PrefabUtility.InstantiatePrefab(directPrefab);
                if (_previewGO == null) { Debug.LogWarning("[AnimationLab] Could not instantiate preview prefab."); return; }
                if (_previewGO.GetComponent<Animator>() == null) _previewGO.AddComponent<Animator>();

                CenterAndFrame(_previewGO);
                RenderPreview();
                Debug.Log($"[AnimationLab] Preview loaded (prefab field): {directPath}");
                return; // <- we're done
            }

            // 2) If no prefab field, fall back to your existing SO-driven path flow
            var so = _animCharacterSOField?.value as ScriptableObject;
            LoadFieldsFromRpgSO(so);
            if (so == null) { Debug.LogWarning("[AnimationLab] No Character SO selected."); return; }

            var modelPath = GetStr(so, "ModelPath") ?? GetStr(so, "modelPath");
            var characterName = GetStr(so, "Name") ?? GetStr(so, "characterName");
            var classKey = GetStr(so, "Class") ?? GetStr(so, "@class") ?? GetStr(so, "classKey");

            if (string.IsNullOrEmpty(modelPath)) { Debug.LogWarning("[AnimationLab] Character SO has no ModelPath."); return; }

            var prefab = ResolvePreviewPrefab(modelPath, characterName, classKey);
            if (prefab == null) { Debug.LogWarning($"[AnimationLab] Model prefab not found (after search) from: {modelPath}"); return; }

            var resolvedPath = AssetDatabase.GetAssetPath(prefab);
            if (!string.IsNullOrEmpty(resolvedPath) && _animModelPath != null) _animModelPath.value = resolvedPath;

            DisposePreview();
            EnsurePreviewUtility();

            _previewGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (_previewGO == null) { Debug.LogWarning("[AnimationLab] Could not instantiate preview prefab."); return; }
            if (_previewGO.GetComponent<Animator>() == null) _previewGO.AddComponent<Animator>();

            CenterAndFrame(_previewGO);
            RenderPreview();

            Debug.Log($"[AnimationLab] Preview loaded: {resolvedPath}");
        }

        // === Preview Utility (re-added) ===
        private void EnsurePreviewUtility(int width = 900, int height = 500)
        {
#if UNITY_EDITOR
            if (_previewUtil != null) return;

            _previewUtil = new PreviewRenderUtility(true);
            _previewUtil.cameraFieldOfView = 25f;

            if (_previewRT == null)
            {
                _previewRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "AI-Assistant-PreviewRT"
                };
                _previewRT.Create();
            }

            // Camera defaults
            var cam = _previewUtil.camera;
            cam.clearFlags = CameraClearFlags.Color;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.10f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 500f;

            // Directional light
            _previewLight = new GameObject("PreviewLight").AddComponent<Light>();
            _previewLight.type = LightType.Directional;
            _previewLight.intensity = 1.2f;
            _previewLight.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            _previewUtil.AddSingleGO(_previewLight.gameObject);
#endif
        }

        private void CenterAndFrame(GameObject go)
        {
            if (_previewUtil == null || go == null) return;

            var bounds = CalculateBounds(go);
            var size = Mathf.Max(0.001f,
                Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));

            var cam = _previewUtil.camera;
            // Distance heuristic
            float dist = size * 2.2f + 0.5f;
            var center = bounds.center;
            cam.transform.position = center + new Vector3(0, size * 0.5f, -dist);
            cam.transform.LookAt(center);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = dist * 6f;
        }

        private Bounds CalculateBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one * 0.5f);

            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private void RenderPreview()
        {
            if (_previewUtil == null || _previewImage == null) return;

            var cam = _previewUtil.camera;
            _previewUtil.BeginPreview(
                new Rect(0, 0, _previewRT.width, _previewRT.height),
                GUIStyle.none);

            cam.Render();

            var tex = _previewUtil.EndPreview();
            _previewImage.image = tex;
        }

        private void OnPreviewUpdate()
        {
#if UNITY_EDITOR
            if (!_isPlayingClip || _currentClip == null || _previewGO == null)
                return;

            float t = (float)(EditorApplication.timeSinceStartup - _clipStartTime);
            if (_currentClip.length > 0.0001f)
                t = t % _currentClip.length;

            // Sample directly (no Animator state machine required)
            _currentClip.SampleAnimation(_previewGO, t);

            // Keep camera stable each frame
            CenterAndFrame(_previewGO);
            RenderPreview();
#endif
        }

        // Optional clean-up hook (if you want to ensure update unsubscribes)
        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= OnPreviewUpdate;
            _isPlayingClip = false;
#endif
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            EditorApplication.update -= OnPreviewUpdate;
            _isPlayingClip = false;

            if (_previewGO != null)
                DestroyImmediate(_previewGO);
            _previewGO = null;

            if (_previewLight != null)
                DestroyImmediate(_previewLight.gameObject);
            _previewLight = null;

            if (_previewUtil != null)
            {
                _previewUtil.Cleanup();
                _previewUtil = null;
            }

            if (_previewRT != null)
            {
                _previewRT.Release();
                DestroyImmediate(_previewRT);
                _previewRT = null;
            }
#endif
        }

        // --- Animation Lab: helpers ---
        // requires: using System.Linq; using UnityEditor; using UnityEngine;

        private static AnimationClip FindClipByTokens(string[] tokens, string[] folders = null)
        {
            // tokens are pieces of the name we expect, e.g. new[]{"Idle"} or {"Light","Attack"}
            if (tokens == null || tokens.Length == 0) return null;

            // Build a name query like: name:Idle name:Attack
            string query = "t:AnimationClip " + string.Join(" ", tokens.Select(t => $"\"{t}\""));

            string[] guids = (folders != null && folders.Length > 0)
                ? AssetDatabase.FindAssets(query, folders)
                : AssetDatabase.FindAssets(query);

            // Score by: contains all tokens (case-insensitive), shortest name wins as tie-breaker
            AnimationClip best = null;
            int bestScore = int.MinValue;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (!clip) continue;

                var name = clip.name.ToLowerInvariant();
                int hits = 0;
                bool allMatch = true;
                foreach (var t in tokens)
                {
                    var tt = t.Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(tt)) continue;
                    if (name.Contains(tt)) hits++;
                    else allMatch = false;
                }

                // require at least 1 hit; prefer allMatch
                if (hits == 0) continue;

                int score = hits * 10 - name.Length; // favor more hits, then shorter names
                if (allMatch) score += 50;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = clip;
                }
            }

            return best;
        }

        // Prefer clips already on the Character SO if available
        private AnimationClip PreferClip(AnimationClip fromSO, string[] searchTokens, string[] folders = null)
        {
            if (fromSO != null) return fromSO;
            return FindClipByTokens(searchTokens, folders); // your existing search helper
        }


        private static bool TrySetRef(ScriptableObject preset, string fieldName, UObject value)
        {
            if (!preset || !value) return false;

            var t = preset.GetType();
            var f = t.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f != null && typeof(UObject).IsAssignableFrom(f.FieldType))
            {
                f.SetValue(preset, value);
                EditorUtility.SetDirty(preset);
                return true;
            }
            var p = t.GetProperty(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p != null && typeof(UObject).IsAssignableFrom(p.PropertyType) && p.CanWrite)
            {
                p.SetValue(preset, value);
                EditorUtility.SetDirty(preset);
                return true;
            }
            return false;
        }
        private bool SyncSelectedChrFromCharacterData()
        {
            var chr = _animCharacterSOField?.value as RpgCharacterSO;
            if (chr == null)
            {
                SetSyncStatus("Last Sync: No CHR selected", new Color(0.9f, 0.3f, 0.3f));
                Debug.LogWarning("[AnimationLab] No CHR selected for sync.");
                return false;
            }

            // Prepare / neutral state
            SetSyncStatus("Last Sync: Searching…", new Color(0.75f, 0.75f, 0.75f));

            // Pick a name to search by
            var wanted = string.IsNullOrEmpty(chr.characterName) ? chr.name : chr.characterName;

            // Find candidate CharacterData assets
            var guidMatches = AssetDatabase.FindAssets($"{wanted} t:CharacterData");
            if (guidMatches == null || guidMatches.Length == 0)
            {
                SetSyncStatus($"Last Sync: Not found for '{wanted}'", new Color(0.9f, 0.3f, 0.3f));
                Debug.LogWarning($"[AnimationLab] No CharacterData found for '{wanted}'.");
                return false;
            }

            // Choose best path (prefer exact file name match), else first
            string bestPath = null;
            foreach (var guid in guidMatches)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var file = System.IO.Path.GetFileNameWithoutExtension(p);
                if (string.Equals(file, wanted, System.StringComparison.OrdinalIgnoreCase))
                {
                    bestPath = p;
                    break;
                }
            }
            if (string.IsNullOrEmpty(bestPath))
                bestPath = AssetDatabase.GUIDToAssetPath(guidMatches[0]);

            // If multiple results, mark as warning (but still proceed)
            if (guidMatches.Length > 1)
            {
                SetSyncStatus($"Last Sync: Multiple found for '{wanted}' (picked best) ⚠️",
                    new Color(0.95f, 0.78f, 0.2f));
            }

            var src = AssetDatabase.LoadAssetAtPath<CharacterData>(bestPath);
            if (src == null)
            {
                SetSyncStatus($"Last Sync: Could not load asset", new Color(0.9f, 0.3f, 0.3f));
                Debug.LogWarning($"[AnimationLab] Could not load CharacterData at {bestPath}.");
                return false;
            }

            // Map fields
            MapCharacterDataToChr(src, chr);

            EditorUtility.SetDirty(chr);
            AssetDatabase.SaveAssets();

            var shortPath = System.IO.Path.GetFileName(bestPath);
            SetSyncStatus($"Last Sync: {shortPath} ✅", new Color(0.35f, 0.85f, 0.45f));
            return true;
        }
        private static void MapCharacterDataToChr(CharacterData src, RpgCharacterSO dst)
        {
            // ---- Identity ----
            dst.type = src.Type;
            dst.role = src.Role;
            dst.affiliation = src.Affiliation;
            dst.@class = src.Class;
            dst.subClass = src.SubClass;
            dst.faction = src.Faction;
            dst.element = src.Element;
            dst.gender = src.Gender;
            dst.resourceType = src.ResourceType;
            // dst.weaponType = (map if your CharacterData has it)

            // ---- Core Stats ----
            dst.health = src.Health;
            dst.mana = src.Mana;
            dst.attack = src.Attack;
            dst.defense = src.Defense;
            dst.magic = src.Magic;
            dst.speed = Mathf.RoundToInt(src.Speed);

            // ---- Attributes ----
            dst.strength = src.Strength;
            dst.agility = src.Agility;
            dst.intelligence = src.Intelligence;

            // ---- Combat ----
            dst.armor = src.Armor;
            dst.magicResist = src.MagicResist;
            dst.attackSpeed = src.AttackSpeed;
            dst.moveSpeed = src.MoveSpeed; // keep if you also use 'speed'
            dst.range = src.Range;
            dst.critChance = src.CritChance;
            dst.critDamageMultiplier = src.CritDamageMultiplier;
            dst.armorPenetration = src.ArmorPenetration;
            dst.magicPenetration = src.MagicPenetration;
            dst.lifeSteal = src.LifeSteal;
            dst.spellVamp = src.SpellVamp;
            dst.cooldownReduction = src.CooldownReduction;
            dst.tenacity = src.Tenacity;

            // ---- Meta / Lore ----
            dst.level = src.Level;
            if (!string.IsNullOrEmpty(src.LoreBackground))
                dst.loreBackground = src.LoreBackground;

            // If you keep passives/abilities on CharacterData, map them here too:
            // dst.passiveName = src.PassiveName; etc.
        }

        // ---------- CHARACTER SO SYNC (CSV -> CHR_) ----------
        private static string ChrFolder => "Assets/TheCovenantKeepers/AI_Game_Assistant/ScriptableObjects/Characters";

        private RpgCharacterSO FindOrCreateChrSO(string charName)
        {
            if (string.IsNullOrWhiteSpace(charName)) charName = "Unnamed";
            var fileName = $"CHR_{SanitizeFile(charName)}.asset";
            var path = System.IO.Path.Combine(ChrFolder, fileName).Replace("\\", "/");

            var existing = AssetDatabase.LoadAssetAtPath<RpgCharacterSO>(path);
            if (existing) return existing;

            if (!AssetDatabase.IsValidFolder(ChrFolder))
            {
                var root = "Assets/TheCovenantKeepers/AI_Game_Assistant/ScriptableObjects";
                if (!AssetDatabase.IsValidFolder(root))
                    AssetDatabase.CreateFolder("Assets/TheCovenantKeepers/AI_Game_Assistant", "ScriptableObjects");
                AssetDatabase.CreateFolder(root, "Characters");
            }

            var so = ScriptableObject.CreateInstance<RpgCharacterSO>();
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();
            return so;
        }

        private static string SanitizeFile(string s)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                s = s.Replace(c.ToString(), "");
            return s.Trim();
        }

        private static int RInt(float f) => Mathf.RoundToInt(f);
        private void SyncAllChrFromCharacterData()
        {
            if (characters == null || characters.Count == 0)
            {
                Debug.LogWarning("[Sync] No CharacterData entries loaded.");
                return;
            }

            foreach (var c in characters)
            {
                var dst = FindOrCreateChrSO(c.Name);
                MapCharacterDataToChr(c, dst);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Sync] Updated/created {characters.Count} CHR_ assets.");
        }

        // ---- Animation Lab helpers ----
        private void SetSyncStatus(string msg, Color color)
        {
            if (_animLastSyncedLabel == null) return;
            _animLastSyncedLabel.text = msg;
            _animLastSyncedLabel.style.color = new StyleColor(color);
        }

        // ------------------------------------------------------------
        // Loads Name, Class, and ModelPath fields from any character SO
        // ------------------------------------------------------------
        private void LoadFieldsFromRpgSO(ScriptableObject so)
        {
            if (so == null) return;

            // Safely read common field/property names
            string name = GetStr(so, "Name") ?? GetStr(so, "characterName");
            string cls = GetStr(so, "Class") ?? GetStr(so, "@class") ?? GetStr(so, "classKey");
            string model = GetStr(so, "ModelPath") ?? GetStr(so, "modelPath");

            if (!string.IsNullOrEmpty(name) && _animName != null) _animName.value = name;
            if (!string.IsNullOrEmpty(cls) && _animClassKey != null) _animClassKey.value = cls;
            if (!string.IsNullOrEmpty(model) && _animModelPath != null) _animModelPath.value = model;

            // Load prefab if the path points to one
            if (!string.IsNullOrEmpty(model) && _animModelPrefabField != null)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(model);
                _animModelPrefabField.value = go;
            }

            Debug.Log($"[AI Assistant] Loaded fields from {so.name} → Class={cls}");
        }

        private void LoadFieldsFromSO(RpgCharacterSO chr)
        {
            if (chr == null) return;

            // name
            if (_animName != null)
                _animName.value = string.IsNullOrEmpty(chr.characterName) ? chr.name : chr.characterName;

            // class  ← reads the real field on the SO
            if (_animClassKey != null)
                _animClassKey.value = chr.@class;

            // model (optional)
            if (_animModelPath != null)
                _animModelPath.value = chr.modelPath;
            if (_animModelPrefabField != null)
                _animModelPrefabField.value = chr.modelPrefab;

            Debug.Log($"[AnimationLab] Loaded fields from {chr.name} (class: {chr.@class})");
        }

        // === Auto-clip linking helpers ===============================================

        private static string[] ClipStateNames = {
    "Idle","Walk","Run","Jump","Attack","Hit","Death","Cast",
    "Ability1","Ability2","Ability3","Ultimate"
};

        private static string DirOf(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var i = assetPath.LastIndexOf('/');
            return i > 0 ? assetPath.Substring(0, i) : null;
        }

        private static AnimationClip FindClip(string[] folders, params string[] nameHints)
        {
            if (folders == null || folders.Length == 0) return null;
            // Search by strongest hint first
            foreach (var hint in nameHints)
            {
                if (string.IsNullOrWhiteSpace(hint)) continue;
                var guids = AssetDatabase.FindAssets($"t:AnimationClip {hint}", folders);
                foreach (var g in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                    if (clip != null) return clip;
                }
            }
            return null;
        }

        /// <summary>
        /// Try to populate empty AnimationClip fields on the CHR from folders:
        /// 1) Folder of the model prefab path (and an "Animations" child, if present)
        /// 2) Any folder named "*Animations*/*{classKey}*" in the project (broad fallback)
        /// 3) Whole project fallback by state name
        /// </summary>
        private void EnsureClipsOnCharacterSO(RpgCharacterSO chr)
        {
            if (chr == null) return;

            // Candidate folders
            var folders = new List<string>();

            // From modelPath
            var modelFolder = DirOf(chr.modelPath);
            if (!string.IsNullOrEmpty(modelFolder))
            {
                folders.Add(modelFolder);
                var animChild = modelFolder + "/Animations";
                if (AssetDatabase.IsValidFolder(animChild)) folders.Add(animChild);
            }

            // From class name (search any "*Animations*/*Class*")
            if (!string.IsNullOrEmpty(chr.@class))
            {
                var classLower = chr.@class.ToLowerInvariant();
                var allFolders = AssetDatabase.GetSubFolders("Assets");
                foreach (var f in allFolders)
                {
                    var fl = f.ToLowerInvariant();
                    if (fl.Contains("animation") && fl.Contains(classLower))
                        folders.Add(f);
                }
            }

            // Dedup folders
            folders = folders.Distinct().ToList();

            // Local setter to avoid reflection if your field names differ, edit here:
            void SetIfNull(ref AnimationClip slot, string state)
            {
                if (slot != null) return;

                // Build name hints (strong → weak)
                var hints = new List<string>();
                if (!string.IsNullOrEmpty(chr.@class)) hints.Add($"{chr.@class}_{state}");
                if (!string.IsNullOrEmpty(chr.characterName)) hints.Add($"{chr.characterName}_{state}");
                hints.Add(state);

                // 1) search candidate folders
                var clip = FindClip(folders.ToArray(), hints.ToArray());

                // 2) project-wide fallback
                if (clip == null)
                {
                    var g = AssetDatabase.FindAssets($"t:AnimationClip {state}");
                    foreach (var id in g)
                    {
                        var p = AssetDatabase.GUIDToAssetPath(id);
                        clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                        if (clip != null) break;
                    }
                }

                if (clip != null) slot = clip;
            }

            EditorUtility.SetDirty(chr);
        }

        // Maps friendly keys to your RpgCharacterSO clip slots.
        private static AnimationClip GetChrClip(RpgCharacterSO c, string key)
        {
            if (c == null) return null;

            switch (key)
            {
                case "Idle": return c.Idle;
                case "Walk": return c.Walk;
                case "Run": return c.Run;
                case "Jump": return c.Jump;
                case "Attack": return c.LightAttack ?? c.HeavyAttack ?? c.Ability1;
                case "Hit": return c.HitReact;
                case "Death": return c.Death;
                case "Cast": return c.Ability1;
                case "A1": return c.Ability1;
                case "A2": return c.Ability2;
                case "A3": return c.Ability3;
                case "Ult": return c.Ultimate;
                default: return null;
            }
        }

        private void AutoFillClipsFromExplosiveFolder()
        {
            var chr = _animCharacterSOField?.value as RpgCharacterSO;
            if (chr == null)
            {
                EditorUtility.DisplayDialog("Auto-Fill Clips", "Select a CHR_ (RpgCharacterSO) first.", "OK");
                return;
            }

            // Restrict search to your animation pack folder
            string[] roots = { "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations" };
            var guids = AssetDatabase.FindAssets("t:AnimationClip", roots);

            // Load all clips once
            var clips = new List<AnimationClip>(guids.Length);
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                if (c != null) clips.Add(c);
            }

            // Simple keyword map → adjust to your pack’s names
            TryAssign(ref chr.Idle, clips, "idle", "combat idle", "relax idle");
            TryAssign(ref chr.Walk, clips, "walk forward", "walk");
            TryAssign(ref chr.Run, clips, "run forward", "run");
            TryAssign(ref chr.Jump, clips, "jump", "leap");
            TryAssign(ref chr.LightAttack, clips, "attack1", "light", "slash");
            TryAssign(ref chr.HeavyAttack, clips, "attack2", "heavy", "power");
            TryAssign(ref chr.HitReact, clips, "hit", "get hit", "hitreact", "hurt");
            TryAssign(ref chr.Death, clips, "death", "die");
            TryAssign(ref chr.Ability1, clips, "cast", "spell", "ability1", "skill1");
            TryAssign(ref chr.Ability2, clips, "ability2", "skill2");
            TryAssign(ref chr.Ability3, clips, "ability3", "skill3");
            TryAssign(ref chr.Ultimate, clips, "ultimate", "finisher");

            EditorUtility.SetDirty(chr);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Auto-Fill Clips", $"Finished auto-filling from ExplosiveLLC folder.", "OK");

            // local helper
            void TryAssign(ref AnimationClip slot, List<AnimationClip> pool, params string[] keys)
            {
                if (slot != null) return; // don’t overwrite
                var best = FindBest(pool, keys);
                if (best != null) slot = best;
            }

            AnimationClip FindBest(List<AnimationClip> pool, string[] keys)
            {
                AnimationClip best = null; int scoreBest = int.MinValue;
                foreach (var c in pool)
                {
                    var n = c.name.ToLowerInvariant();
                    int s = Score(n, keys);
                    if (s > scoreBest) { scoreBest = s; best = c; }
                }
                return best;
            }

            int Score(string name, string[] keys)
            {
                int s = int.MinValue;
                foreach (var k in keys)
                {
                    var kk = k.ToLowerInvariant();
                    if (name == kk) s = Math.Max(s, 100);
                    else if (name.StartsWith(kk)) s = Math.Max(s, 80);
                    else if (name.Contains(kk)) s = Math.Max(s, 60);
                }
                if ((name.Contains("idle") || name.Contains("walk") || name.Contains("run")) && name.Contains("loop"))
                    s += 5;
                return s;
            }

            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                if (c == null) continue;
                var n = c.name.ToLowerInvariant();
                if (n.Contains("placeholder")) continue;   // <- ignore template placeholders
                clips.Add(c);
            }

        }
        // --- Helpers to prevent template clips leaking into the final controller ---

        // 1) Remove any states that still reference placeholder clips
        private static void RemovePlaceholderStates(AnimatorStateMachine sm)
        {
            if (sm == null) return;
            foreach (var sc in sm.states)
            {
                var m = sc.state.motion;
                if (m != null && m.name.ToLowerInvariant().Contains("placeholder"))
                    sm.RemoveState(sc.state);
            }
        }

        // 2) Always clear any carried-over motion before assigning a real clip
        private static void SetMotion(AnimatorState state, AnimationClip clip)
        {
            state.motion = null;                // clear template motion
            if (clip != null) state.motion = clip;
        }

    }
}
