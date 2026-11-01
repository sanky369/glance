using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnusedAssetCleanup
{
    internal class UnusedAssetCleanupWindow : EditorWindow
    {
        enum SortColumn { Name, Path, Type, Size, Class }

        const string MenuRoot = "Tools/Unused Asset Cleanup/";

        ScanResult _result;
        SortColumn _sort = SortColumn.Size;
        bool _ascending;

        bool _folderScope;
        string _selectedFolder;
        string _search = "";
        string _typeFilter = "All types";
        readonly HashSet<Classification> _visible = new HashSet<Classification>
        {
            Classification.Unused, Classification.Review
        };

        Vector2 _tableScroll, _previewScroll, _notesScroll;
        string _preview;
        bool _armed;
        QuarantineResult _last;
        bool _showNotes;

        [MenuItem(MenuRoot + "Open Cleanup Window")]
        static void Open()
        {
            var w = GetWindow<UnusedAssetCleanupWindow>(false, "Asset Cleanup", true);
            w.minSize = new Vector2(880, 520);
            w.RefreshFolder();
            w.Show();
        }

        [MenuItem(MenuRoot + "Restore From Quarantine")]
        static void RestoreMenu()
        {
            string root = AssetQuarantine.QuarantineRoot;
            if (!Directory.Exists(root))
            {
                Debug.Log("[Asset Cleanup] Nothing quarantined yet. Expected: " + root);
                return;
            }

            string chosen = EditorUtility.OpenFolderPanel("Pick a quarantine batch to restore", root, "");
            if (string.IsNullOrEmpty(chosen)) return;

            int n = AssetQuarantine.Restore(chosen);
            Debug.Log(n < 0
                ? "[Asset Cleanup] " + chosen + " has no manifest.txt - not a quarantine folder."
                : "[Asset Cleanup] Restored " + n + " asset(s) from " + chosen);
        }

        void OnSelectionChange()
        {
            RefreshFolder();
            Repaint();
        }

        void RefreshFolder()
        {
            foreach (var o in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string p = AssetDatabase.GetAssetPath(o);
                if (!string.IsNullOrEmpty(p) && AssetDatabase.IsValidFolder(p) && p.StartsWith("Assets/"))
                {
                    _selectedFolder = p;
                    return;
                }
            }
            _selectedFolder = null;
        }

        void OnGUI()
        {
            DrawToolbar();
            if (_result == null)
            {
                EditorGUILayout.HelpBox(
                    "Scan builds the full dependency graph from every scene in the project, all Resources " +
                    "and StreamingAssets content, Editor tooling, ProjectSettings references, preloaded " +
                    "assets, AssetBundles and Addressables.\n\n" +
                    "Anything it cannot prove is unused is marked Review rather than Unused.",
                    MessageType.Info);
                return;
            }

            DrawSummary();
            DrawFilters();
            DrawTable();
            DrawFooter();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool newScope = GUILayout.Toggle(_folderScope, "Selected folder only",
                    EditorStyles.toolbarButton, GUILayout.Width(150));
                if (newScope != _folderScope) { _folderScope = newScope; _armed = false; }

                GUILayout.Label(_folderScope
                    ? (_selectedFolder ?? "<no folder selected>")
                    : "Whole project", EditorStyles.miniLabel, GUILayout.Width(320));

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(_folderScope && _selectedFolder == null))
                {
                    if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(70)))
                        Scan();
                }

                if (GUILayout.Button("Restore...", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    RestoreMenu();
            }
        }

        void Scan()
        {
            _armed = false;
            _preview = null;
            _last = null;
            _result = UnusedAssetScanner.Scan(_folderScope ? _selectedFolder : null);
            Resort();
        }

        void DrawSummary()
        {
            int unused = _result.Entries.Count(e => e.Classification == Classification.Unused);
            int review = _result.Entries.Count(e => e.Classification == Classification.Review);
            int used = _result.Entries.Count(e => e.Classification == Classification.Referenced);
            int prot = _result.Entries.Count(e => e.Classification == Classification.Protected);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(string.Format(
                "{0}  —  {1} asset(s):  {2} unused, {3} need review, {4} referenced, {5} protected",
                _result.Scope, _result.Entries.Count, unused, review, used, prot),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format(
                "{0} root(s) walked, {1} asset(s) reachable.  Recoverable if all unused are removed: {2}",
                _result.RootCount, _result.ReferencedCount, AssetQuarantine.Size(_result.UnusedBytes)));

            _showNotes = EditorGUILayout.Foldout(_showNotes, "How this scan was built (" + _result.Notes.Count + ")");
            if (_showNotes)
            {
                _notesScroll = EditorGUILayout.BeginScrollView(_notesScroll, GUILayout.Height(90));
                foreach (string n in _result.Notes) EditorGUILayout.LabelField("• " + n, EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (Classification c in new[]
                         {
                             Classification.Unused, Classification.Review,
                             Classification.Referenced, Classification.Protected
                         })
                {
                    bool on = _visible.Contains(c);
                    bool now = GUILayout.Toggle(on, c.ToString(), EditorStyles.miniButton, GUILayout.Width(90));
                    if (now != on)
                    {
                        if (now) _visible.Add(c); else _visible.Remove(c);
                    }
                }

                var types = new List<string> { "All types" };
                types.AddRange(_result.Entries.Select(e => e.TypeName).Distinct().OrderBy(t => t));
                int idx = Mathf.Max(0, types.IndexOf(_typeFilter));
                idx = EditorGUILayout.Popup(idx, types.ToArray(), GUILayout.Width(160));
                _typeFilter = types[idx];

                GUILayout.Label("Search", GUILayout.Width(45));
                _search = EditorGUILayout.TextField(_search);

                if (GUILayout.Button("Select all unused", EditorStyles.miniButton, GUILayout.Width(120)))
                {
                    foreach (var e in Filtered()) e.Selected = e.Deletable;
                    _armed = false;
                }
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    foreach (var e in _result.Entries) e.Selected = false;
                    _armed = false;
                }
            }
        }

        IEnumerable<AssetEntry> Filtered()
        {
            return _result.Entries.Where(e =>
                _visible.Contains(e.Classification)
                && (_typeFilter == "All types" || e.TypeName == _typeFilter)
                && (string.IsNullOrEmpty(_search)
                    || e.Path.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0));
        }

        void DrawTable()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(22);
                Header("Name", SortColumn.Name, 220);
                Header("Type", SortColumn.Type, 110);
                Header("Size", SortColumn.Size, 80);
                Header("Class", SortColumn.Class, 90);
                Header("Path", SortColumn.Path, 0);
            }

            _tableScroll = EditorGUILayout.BeginScrollView(_tableScroll, GUILayout.ExpandHeight(true));
            foreach (var e in Filtered())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!e.Deletable))
                    {
                        bool sel = GUILayout.Toggle(e.Selected, GUIContent.none, GUILayout.Width(18));
                        if (sel != e.Selected) { e.Selected = sel; _armed = false; }
                    }

                    if (GUILayout.Button(e.Name, EditorStyles.label, GUILayout.Width(220)))
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(e.Path));

                    GUILayout.Label(e.TypeName, GUILayout.Width(110));
                    GUILayout.Label(AssetQuarantine.Size(e.Bytes), GUILayout.Width(80));

                    var prev = GUI.color;
                    GUI.color = ClassColor(e.Classification);
                    GUILayout.Label(e.Classification.ToString(), GUILayout.Width(90));
                    GUI.color = prev;

                    GUILayout.Label(new GUIContent(e.Path, e.Reason), EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        static Color ClassColor(Classification c)
        {
            switch (c)
            {
                case Classification.Unused: return new Color(1f, 0.6f, 0.4f);
                case Classification.Review: return new Color(1f, 0.85f, 0.4f);
                case Classification.Protected: return new Color(0.6f, 0.75f, 1f);
                default: return Color.white;
            }
        }

        void Header(string label, SortColumn column, float width)
        {
            string arrow = _sort == column ? (_ascending ? " ^" : " v") : "";
            var opts = width > 0 ? new[] { GUILayout.Width(width) } : new GUILayoutOption[0];
            if (GUILayout.Button(label + arrow, EditorStyles.toolbarButton, opts))
            {
                if (_sort == column) _ascending = !_ascending;
                else { _sort = column; _ascending = column != SortColumn.Size; }
                Resort();
            }
        }

        void Resort()
        {
            if (_result == null) return;
            IEnumerable<AssetEntry> q = _result.Entries;
            switch (_sort)
            {
                case SortColumn.Name: q = q.OrderBy(e => e.Name); break;
                case SortColumn.Path: q = q.OrderBy(e => e.Path); break;
                case SortColumn.Type: q = q.OrderBy(e => e.TypeName).ThenByDescending(e => e.Bytes); break;
                case SortColumn.Class: q = q.OrderBy(e => e.Classification).ThenByDescending(e => e.Bytes); break;
                default: q = q.OrderBy(e => e.Bytes); break;
            }
            var list = q.ToList();
            if (!_ascending) list.Reverse();
            _result.Entries = list;
        }

        void DrawFooter()
        {
            var selected = _result.Entries.Where(e => e.Selected && e.Deletable).ToList();
            long bytes = selected.Sum(e => e.Bytes);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    selected.Count + " selected  —  " + AssetQuarantine.Size(bytes) + " recoverable",
                    EditorStyles.boldLabel, GUILayout.Width(300));

                using (new EditorGUI.DisabledScope(selected.Count == 0))
                {
                    if (GUILayout.Button("Preview cleanup", GUILayout.Height(22)))
                    {
                        _preview = AssetQuarantine.BuildPreview(selected);
                        _armed = false;
                    }

                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_preview)))
                    {
                        _armed = GUILayout.Toggle(_armed, _armed ? "Armed" : "Arm",
                            EditorStyles.miniButton, GUILayout.Width(80), GUILayout.Height(22));

                        using (new EditorGUI.DisabledScope(!_armed))
                        {
                            var bg = GUI.backgroundColor;
                            if (_armed) GUI.backgroundColor = new Color(1f, 0.5f, 0.35f);
                            if (GUILayout.Button("Quarantine " + selected.Count + " asset(s)",
                                    GUILayout.Height(22)))
                                RunQuarantine(selected);
                            GUI.backgroundColor = bg;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(_preview))
            {
                _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.Height(150));
                EditorGUILayout.TextArea(_preview, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }

            if (_last != null)
            {
                EditorGUILayout.HelpBox(string.Format(
                    "Moved {0}, blocked {1}, failed {2}. Freed {3}.\n{4}",
                    _last.Moved.Count, _last.Blocked.Count, _last.Failed.Count,
                    AssetQuarantine.Size(_last.BytesFreed), _last.Directory),
                    _last.Failed.Count > 0 ? MessageType.Warning : MessageType.Info);

                if (GUILayout.Button("Open quarantine folder"))
                    EditorUtility.RevealInFinder(_last.Directory);
            }
        }

        void RunQuarantine(List<AssetEntry> selected)
        {
            _armed = false;
            _last = AssetQuarantine.Quarantine(selected);

            foreach (string b in _last.Blocked) Debug.LogWarning("[Asset Cleanup] Kept: " + b);
            foreach (string f in _last.Failed) Debug.LogError("[Asset Cleanup] Failed: " + f);
            Debug.Log("[Asset Cleanup] Quarantined " + _last.Moved.Count + " asset(s) to " + _last.Directory);

            _preview = null;
            Scan();
        }
    }
}
