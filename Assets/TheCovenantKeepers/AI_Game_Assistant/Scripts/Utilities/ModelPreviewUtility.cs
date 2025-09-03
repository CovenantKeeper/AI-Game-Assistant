using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    public class ModelPreviewUtility
    {
        private PreviewRenderUtility previewUtility;
        private GameObject previewInstance;
        private Vector2 previewRotation = new Vector2(120, -20);
        private Vector3 previewPosition = Vector3.zero;

        public void Initialize()
        {
            if (previewUtility == null)
            {
                previewUtility = new PreviewRenderUtility(true);
                previewUtility.cameraFieldOfView = 30f;

                previewUtility.camera.transform.position = new Vector3(0, 1, -5);
                previewUtility.camera.transform.LookAt(previewPosition);

                previewUtility.lights[0].intensity = 1.4f;
                previewUtility.lights[0].transform.rotation = Quaternion.Euler(50f, 50f, 0);

                previewUtility.lights[1].intensity = 1.0f;
                previewUtility.ambientColor = Color.gray;
            }
        }

        public void SetModel(GameObject modelPrefab)
        {
            Cleanup();

            if (modelPrefab == null) return;

            previewInstance = GameObject.Instantiate(modelPrefab);
            previewInstance.transform.position = previewPosition;

            // Disable all MonoBehaviours (to prevent Update/Start/etc. from running)
            foreach (var comp in previewInstance.GetComponentsInChildren<MonoBehaviour>())
                comp.enabled = false;

            previewUtility.AddSingleGO(previewInstance);
        }

        public void OnGUI(Rect rect)
        {
            if (previewUtility == null || previewInstance == null)
                return;

            Event e = Event.current;
            if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition))
            {
                previewRotation += e.delta * 0.5f;
                GUI.changed = true;
            }

            previewUtility.BeginPreview(rect, GUIStyle.none);
            previewUtility.camera.transform.position = Quaternion.Euler(previewRotation.y, -previewRotation.x, 0) * new Vector3(0, 1, -5);
            previewUtility.camera.transform.LookAt(previewPosition);
            previewUtility.Render();

            Texture resultRender = previewUtility.EndPreview();
            GUI.DrawTexture(rect, resultRender, ScaleMode.StretchToFill, false);
        }

        public void Cleanup()
        {
            if (previewInstance != null)
                Object.DestroyImmediate(previewInstance);
            previewInstance = null;
        }

        public void Dispose()
        {
            Cleanup();
            previewUtility?.Cleanup();
            previewUtility = null;
        }
    }
}

