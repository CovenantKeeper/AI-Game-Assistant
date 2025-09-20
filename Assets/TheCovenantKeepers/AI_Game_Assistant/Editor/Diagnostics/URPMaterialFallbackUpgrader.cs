#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public static class URPMaterialFallbackUpgrader
    {
        private const string Menu = "The Covenant Keepers/AI Game Assistant/Diagnostics/URP/Upgrade POLYGON Materials (Fallback)";

        // Common POLYGON shader graph names we see in imports
        private static readonly string[] PolygonShaderNames =
        {
            "Shader Graphs/POLYGON_CustomCharacters",
            "Shader Graphs/POLYGON_Character",
            "POLYGON/Characters", // older releases
        };

        [MenuItem(Menu, priority = 1200)]
        public static void UpgradeSelectedOrAll()
        {
            // Prefer URP Lit; fallback to Simple Lit
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var urpSimple = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (urpLit == null && urpSimple == null)
            {
                Debug.LogError("URP shaders not found. Ensure URP is installed and pipeline asset is active.");
                return;
            }

            // Use selection if any materials are selected; else scan all assets
            var matGuids = Selection.objects.OfType<Material>().Any()
                ? Selection.objects.OfType<Material>().Select(m => AssetDatabase.GetAssetPath(m)).ToArray()
                : AssetDatabase.FindAssets("t:Material");

            int total = 0, changed = 0;
            foreach (var guid in matGuids)
            {
                var path = guid.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ? guid : AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                total++;

                var srcShader = mat.shader != null ? mat.shader.name : string.Empty;
                if (!PolygonShaderNames.Any(n => string.Equals(n, srcShader, StringComparison.OrdinalIgnoreCase)))
                    continue; // not a POLYGON custom shader

                // Capture common texture/color props before switching shader
                var baseMap = GetAnyTexture(mat, "_BaseMap", "_BaseColorMap", "_MainTex", "_Albedo", "_BaseTex", "_Texture");
                var baseColor = GetAnyColor(mat, "_BaseColor", "_Color", "_TintColor");
                if (ApproxZero(baseColor))
                {
                    // Use POLYGON primary color if available
                    var pri = GetAnyColor(mat, "_Color_Primary");
                    if (!ApproxZero(pri))
                    {
                        pri.a = pri.a == 0 ? 1 : pri.a;
                        baseColor = pri;
                    }
                }

                // Switch shader
                var targetShader = urpLit != null ? urpLit : urpSimple;
                mat.shader = targetShader;

                // Reassign obvious properties
                if (baseMap != null)
                {
                    TrySetTexture(mat, "_BaseMap", baseMap);
                    TrySetTexture(mat, "_MainTex", baseMap); // legacy
                }
                if (!ApproxZero(baseColor))
                {
                    TrySetColor(mat, "_BaseColor", baseColor);
                    TrySetColor(mat, "_Color", baseColor);
                }

                // Reduce smoothness/metallic for stylized assets
                TrySetFloat(mat, "_Smoothness", 0.05f);
                TrySetFloat(mat, "_Metallic", 0.0f);

                EditorUtility.SetDirty(mat);
                changed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"URP Fallback Upgrade complete. Materials scanned: {total}, changed: {changed}.");
        }

        private static Texture GetAnyTexture(Material m, params string[] names)
        { foreach (var n in names) if (m.HasProperty(n)) { var t = m.GetTexture(n); if (t) return t; } return null; }
        private static Color GetAnyColor(Material m, params string[] names)
        { foreach (var n in names) if (m.HasProperty(n)) return m.GetColor(n); return default; }
        private static void TrySetTexture(Material m, string name, Texture t)
        { if (m.HasProperty(name)) m.SetTexture(name, t); }
        private static void TrySetColor(Material m, string name, Color c)
        { if (m.HasProperty(name)) m.SetColor(name, c); }
        private static void TrySetFloat(Material m, string name, float v)
        { if (m.HasProperty(name)) m.SetFloat(name, v); }
        private static bool ApproxZero(Color c)
        { return c.r == 0f && c.g == 0f && c.b == 0f && c.a == 0f; }
    }
}
#endif
