#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TheCovenantKeepers.AI_Game_Assistant; // for AssistantPaths

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public static class AbilityEffectTemplates
    {
        private const string Root = "Assets/TheCovenantKeepers/AI_Game_Assistant/Blueprints/Effects";

        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Effects + "/Create Effect ScriptableObject", priority = 300)]
        public static void CreateEffectAsset()
        {
            AssistantPaths.EnsureFolder(Root);
            var asset = ScriptableObject.CreateInstance<AbilityEffect>();
            var path = EditorUtility.SaveFilePanelInProject("Create AbilityEffect", "NewAbilityEffect", "asset", "Select location", Root);
            if (string.IsNullOrEmpty(path)) { Object.DestroyImmediate(asset); return; }
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Effects + "/Create Simple Spark VFX Prefab", priority = 301)]
        public static void CreateSparkPrefab()
        {
            AssistantPaths.EnsureFolder(Root);
            var go = new GameObject("FX_Spark");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.duration = 0.5f; main.startLifetime = 0.35f; main.startSpeed = 2.5f; main.startSize = 0.2f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.loop = false; main.maxParticles = 128;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 24, 36) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.05f;
            var trails = ps.trails; trails.enabled = true; trails.ratio = 0.5f;
            go.AddComponent<EffectAutoDestroy>().lifetime = 0.6f;

            var path = AssetDatabase.GenerateUniqueAssetPath($"{Root}/FX_Spark.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            EditorGUIUtility.PingObject(prefab);
            Selection.activeObject = prefab;
        }

        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Effects + "/Create Simple Hit Puff VFX Prefab", priority = 302)]
        public static void CreateHitPuffPrefab()
        {
            AssistantPaths.EnsureFolder(Root);
            var go = new GameObject("FX_HitPuff");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.duration = 0.7f; main.startLifetime = 0.45f; main.startSpeed = 1.2f; main.startSize = 0.35f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.loop = false; main.maxParticles = 256;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 18, 26) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = 0.05f;
            var colorOverLife = ps.colorOverLifetime; colorOverLife.enabled = true; colorOverLife.color = new ParticleSystem.MinMaxGradient(new Gradient {
                colorKeys = new [] { new GradientColorKey(new Color(1f,0.9f,0.7f),0f), new GradientColorKey(new Color(1f,0.4f,0.1f),0.4f), new GradientColorKey(new Color(0.3f,0.05f,0.01f),1f) },
                alphaKeys = new [] { new GradientAlphaKey(1f,0f), new GradientAlphaKey(0.0f,1f) }
            });
            go.AddComponent<EffectAutoDestroy>().lifetime = 0.8f;

            var path = AssetDatabase.GenerateUniqueAssetPath($"{Root}/FX_HitPuff.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            EditorGUIUtility.PingObject(prefab);
            Selection.activeObject = prefab;
        }

        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Effects + "/Create AbilityEffect + Sample VFX Pair", priority = 303)]
        public static void CreateEffectWithVfxPair()
        {
            // Create cast & hit VFX
            CreateSparkPrefab();
            var cast = Selection.activeObject as GameObject;
            CreateHitPuffPrefab();
            var hit = Selection.activeObject as GameObject; // now hit selected

            // Create SO
            AssistantPaths.EnsureFolder(Root);
            var asset = ScriptableObject.CreateInstance<AbilityEffect>();
            asset.castVfx = cast; asset.hitVfx = hit; asset.positionOffset = Vector3.zero; asset.rotationOffsetEuler = Vector3.zero; asset.parentToSpawn = false;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{Root}/AbilityEffect_Simple.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
    }
}
#endif
