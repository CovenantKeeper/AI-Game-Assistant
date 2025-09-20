#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public static class CharacterControllerFixes
    {
        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Character + "/Fix Selected Character Controller(s)", priority = 50)]
        public static void FixSelected()
        {
            int fixedCount = 0;
            foreach (var obj in Selection.gameObjects)
            {
                fixedCount += FixOnGameObject(obj) ? 1 : 0;
            }
            EditorUtility.DisplayDialog("Character Controller Fixes",
                fixedCount > 0 ? $"Fixed {fixedCount} object(s)." : "No CharacterController found on selection.",
                "OK");
        }

        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Character + "/Fix Character Controllers In Scene", priority = 51)]
        public static void FixInScene()
        {
            var controllers = UnityEngine.Object.FindObjectsOfType<CharacterController>();
            int fixedCount = 0;
            foreach (var cc in controllers)
            {
                if (cc == null) continue;
                if (TryFitCharacterController(cc)) fixedCount++;
            }
            EditorUtility.DisplayDialog("Character Controller Fixes",
                fixedCount > 0 ? $"Fixed {fixedCount} CharacterController(s) in the scene." : "No CharacterControllers found in the scene.",
                "OK");
        }

        private static bool FixOnGameObject(GameObject go)
        {
            if (go == null) return false;
            var cc = go.GetComponent<CharacterController>();
            if (cc == null) return false;
            return TryFitCharacterController(cc);
        }

        // Recomputes CharacterController size/center so its bottom aligns to the model's visual bottom.
        private static bool TryFitCharacterController(CharacterController controller)
        {
            try
            {
                var root = controller.gameObject;
                var renderers = root.GetComponentsInChildren<Renderer>();
                if (renderers == null || renderers.Length == 0) return false;

                var bounds = new Bounds(renderers[0].bounds.center, Vector3.zero);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    bounds.Encapsulate(r.bounds);
                }

                // Compute desired controller dimensions from visual bounds
                float height = Mathf.Max(0.5f, bounds.size.y);
                float radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f, 0.1f, height * 0.5f);

                // Align controller bottom to model's bottom
                var worldBottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var localBottom = root.transform.InverseTransformPoint(worldBottom);
                var center = new Vector3(0f, localBottom.y + height * 0.5f, 0f);

                // Apply settings
                Undo.RecordObject(controller, "Fit CharacterController");
                controller.height = height;
                controller.radius = radius;
                controller.center = center;
                controller.slopeLimit = Mathf.Max(controller.slopeLimit, 45f);
                controller.stepOffset = Mathf.Clamp(height * 0.25f, 0.1f, 0.5f);
                controller.skinWidth = Mathf.Clamp(controller.skinWidth, 0.03f, 0.08f);

                // Make sure Rigidbody won't fight CharacterController
                var rb = root.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = Undo.AddComponent<Rigidbody>(root);
                }
                Undo.RecordObject(rb, "Configure Rigidbody for CharacterController");
                rb.useGravity = false;
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeRotation;

                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(rb);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
