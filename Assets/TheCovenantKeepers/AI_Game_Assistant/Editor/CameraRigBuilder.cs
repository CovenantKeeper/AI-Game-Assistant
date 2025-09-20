#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public static class CameraRigBuilder
    {
        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Build + "/Setup Scene Camera Rig", priority = 120)]
        public static void SetupSceneCameraRig()
        {
            // Try reusing existing MainCamera
            Camera cam = Camera.main;
            GameObject go = cam != null ? cam.gameObject : null;

            if (go == null)
            {
                go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
                Debug.Log("Created new Main Camera.");
            }

            Undo.RegisterFullObjectHierarchyUndo(go, "Setup Scene Camera Rig");

            // Ensure listener
            if (go.GetComponent<AudioListener>() == null)
                go.AddComponent<AudioListener>();

            // Add/Configure follow camera
            var follow = go.GetComponent<TheCovenantKeepers.AI_Game_Assistant.TckFollowCamera>();
            if (follow == null) follow = go.AddComponent<TheCovenantKeepers.AI_Game_Assistant.TckFollowCamera>();

            // Try to auto-target Player
            if (follow.target == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) follow.target = tagged.transform;
                else
                {
                    // Look for a component named CharacterMetadata via reflection
                    var all = Object.FindObjectsOfType<GameObject>();
                    foreach (var obj in all)
                    {
                        var comp = obj.GetComponent("CharacterMetadata");
                        if (comp == null) continue;
                        var type = comp.GetType();
                        var field = type.GetField("prefabKind");
                        var val = field != null ? field.GetValue(comp) as string : null;
                        if (!string.IsNullOrEmpty(val) && val.ToLowerInvariant() == "player")
                        {
                            follow.target = obj.transform;
                            break;
                        }
                    }

                    if (follow.target == null)
                    {
                        var cc = Object.FindObjectOfType<CharacterController>();
                        if (cc != null) follow.target = cc.transform;
                    }
                }
            }

            // Place camera in a sensible starting position
            if (follow.target != null)
            {
                go.transform.position = follow.target.position + new Vector3(0, 1.6f, -6f);
                go.transform.rotation = Quaternion.LookRotation((follow.target.position + follow.targetOffset) - go.transform.position, Vector3.up);
            }
            else
            {
                go.transform.position = new Vector3(0, 2f, -6f);
                go.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("? Scene camera rig is ready. Use right mouse to orbit, scroll to zoom.");
        }
    }
}
#endif
