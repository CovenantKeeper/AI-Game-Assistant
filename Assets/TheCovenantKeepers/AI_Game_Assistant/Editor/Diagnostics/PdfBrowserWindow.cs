#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    /// <summary>
    /// Simple browser to list and open PDF files inside a target Assets folder.
    /// Default folder is Assets/GabrielAguiarProductions.
    /// </summary>
    public class PdfBrowserWindow : EditorWindow
    {
        private string _rootFolder = "Assets/GabrielAguiarProductions";
        private Vector2 _scroll;
        private List<string> _pdfPaths = new List<string>();

        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Diagnostics + "/PDF Browser", priority = 1200)]
        public static void ShowWindow()
        {
            var w = GetWindow<PdfBrowserWindow>(true, "PDF Browser", true);
            w.minSize = new Vector2(640, 360);
            w.RefreshList();
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("PDF Folder (inside Assets)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _rootFolder = EditorGUILayout.TextField(_rootFolder);
            if (GUILayout.Button("Pick", GUILayout.Width(60)))
            {
                var sel = EditorUtility.OpenFolderPanel("Pick Folder Under Assets", Application.dataPath, "");
                if (!string.IsNullOrEmpty(sel))
                {
                    // Convert to project-relative path
                    var data = Application.dataPath.Replace("\\", "/");
                    sel = sel.Replace("\\", "/");
                    if (sel.StartsWith(data))
                    {
                        _rootFolder = "Assets" + sel.Substring(data.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Folder", "Pick a folder under the project's Assets/.", "OK");
                    }
                }
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(80))) RefreshList();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open All", GUILayout.Width(100))) OpenAll();
                if (GUILayout.Button("Reveal Folder", GUILayout.Width(120)))
                {
                    if (AssetDatabase.IsValidFolder(_rootFolder))
                        EditorUtility.RevealInFinder(_rootFolder);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Found: {_pdfPaths.Count}", GUILayout.Width(120));
            }

            EditorGUILayout.Space(6);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_pdfPaths.Count == 0)
            {
                EditorGUILayout.HelpBox("No PDFs found in the selected folder.", MessageType.Info);
            }
            else
            {
                foreach (var p in _pdfPaths)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(p, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Open", GUILayout.Width(70))) EditorUtility.OpenWithDefaultApp(Path.GetFullPath(p));
                    if (GUILayout.Button("Reveal", GUILayout.Width(70))) EditorUtility.RevealInFinder(p);
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("This tool opens PDFs with your OS viewer. Text extraction/parsing is not performed here.", MessageType.None);
        }

        private void RefreshList()
        {
            _pdfPaths.Clear();
            if (!AssetDatabase.IsValidFolder(_rootFolder)) return;
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { _rootFolder });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (path.EndsWith(".pdf", System.StringComparison.OrdinalIgnoreCase))
                    _pdfPaths.Add(path);
            }
            _pdfPaths = _pdfPaths.Distinct().OrderBy(p => p).ToList();
        }

        private void OpenAll()
        {
            foreach (var p in _pdfPaths)
            {
                try { EditorUtility.OpenWithDefaultApp(Path.GetFullPath(p)); }
                catch { }
            }
        }
    }
}
#endif
