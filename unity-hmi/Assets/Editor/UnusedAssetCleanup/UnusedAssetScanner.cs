using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnusedAssetCleanup
{
    internal enum Classification
    {
        Referenced,
        Unused,
        Review,
        Protected
    }

    internal class AssetEntry
    {
        public string Path;
        public string Name;
        public string TypeName;
        public string Extension;
        public long Bytes;
        public Classification Classification;
        public string Reason = "";
        public bool Selected;

        public bool Deletable
        {
            get { return Classification == Classification.Unused; }
        }
    }

    internal class ScanResult
    {
        public List<AssetEntry> Entries = new List<AssetEntry>();
        public int RootCount;
        public int ReferencedCount;
        public string Scope = "";
        public readonly List<string> Notes = new List<string>();

        public long UnusedBytes
        {
            get { return Entries.Where(e => e.Classification == Classification.Unused).Sum(e => e.Bytes); }
        }
    }

    internal static class UnusedAssetScanner
    {
        // Never offered for deletion, whatever the dependency graph says.
        static readonly HashSet<string> ProtectedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".asmdef", ".asmref", ".dll", ".rsp", ".unity", ".inputactions",
            ".asmdef.meta", ".json", ".xml", ".md", ".txt", ".uxml", ".uss"
        };

        // Path fragments whose contents are loaded by name at runtime, so the graph cannot see them.
        static readonly string[] StringLoadedFolders =
        {
            "/Resources/", "/StreamingAssets/", "/Plugins/", "/Editor Default Resources/"
        };

        // Vendored package content imported into Assets. An unreferenced shader variant here is
        // usually genuinely unused, but removing it breaks the vendor's own reimport, so it is
        // never offered as a one-click delete.
        static readonly string[] VendoredFolders =
        {
            "Assets/TextMesh Pro/", "Assets/Samples/", "Assets/ThirdParty/"
        };

        public static ScanResult Scan(string folderScope)
        {
            var result = new ScanResult
            {
                Scope = string.IsNullOrEmpty(folderScope) ? "Whole project" : folderScope
            };

            try
            {
                EditorUtility.DisplayProgressBar("Unused Asset Cleanup", "Collecting assets...", 0.05f);

                string[] allPaths = AssetDatabase.GetAllAssetPaths()
                    .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
                    .Where(p => !AssetDatabase.IsValidFolder(p))
                    .Distinct()
                    .ToArray();

                EditorUtility.DisplayProgressBar("Unused Asset Cleanup", "Collecting roots...", 0.15f);
                var roots = CollectRoots(allPaths, result);
                result.RootCount = roots.Count;

                EditorUtility.DisplayProgressBar("Unused Asset Cleanup", "Walking dependencies...", 0.35f);
                var referenced = new HashSet<string>(
                    AssetDatabase.GetDependencies(roots.ToArray(), true), StringComparer.Ordinal);
                foreach (string r in roots) referenced.Add(r);
                result.ReferencedCount = referenced.Count;

                EditorUtility.DisplayProgressBar("Unused Asset Cleanup", "Indexing script literals...", 0.6f);
                var literals = BuildScriptLiteralIndex();
                result.Notes.Add("Indexed " + literals.Count + " string literals from scripts as a Resources.Load safety net");

                EditorUtility.DisplayProgressBar("Unused Asset Cleanup", "Classifying...", 0.8f);

                IEnumerable<string> candidates = allPaths;
                if (!string.IsNullOrEmpty(folderScope))
                {
                    string prefix = folderScope.TrimEnd('/') + "/";
                    candidates = allPaths.Where(p => p.StartsWith(prefix, StringComparison.Ordinal));
                }

                foreach (string path in candidates)
                {
                    var entry = Classify(path, referenced, literals);
                    if (entry != null) result.Entries.Add(entry);
                }

                result.Entries = result.Entries.OrderByDescending(e => e.Bytes).ToList();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return result;
        }

        // ------------------------------------------------------------- roots

        static List<string> CollectRoots(string[] allPaths, ScanResult result)
        {
            var roots = new HashSet<string>(StringComparer.Ordinal);

            // Every scene in the project, not just the ones in Build Settings.
            var scenes = allPaths.Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (string s in scenes) roots.Add(s);
            result.Notes.Add(scenes.Count + " scene(s) used as roots (" +
                             EditorBuildSettings.scenes.Count(s => s.enabled) + " of them in Build Settings)");

            // String-loaded folders: anything inside them stays alive.
            int stringLoaded = 0;
            foreach (string p in allPaths)
            {
                if (IsStringLoaded(p)) { roots.Add(p); stringLoaded++; }
            }
            if (stringLoaded > 0)
                result.Notes.Add(stringLoaded + " asset(s) in Resources/StreamingAssets/Plugins treated as roots");

            // Editor tooling assets keep whatever they reference alive.
            int editorAssets = 0;
            foreach (string p in allPaths)
            {
                if (IsEditorAsset(p)) { roots.Add(p); editorAssets++; }
            }
            if (editorAssets > 0) result.Notes.Add(editorAssets + " Editor-folder asset(s) treated as roots");

            // Anything a ProjectSettings file points at: graphics, quality, tags, splash, icons, URP chain.
            int settingsRefs = 0;
            foreach (string p in CollectProjectSettingsReferences())
            {
                if (roots.Add(p)) settingsRefs++;
            }
            result.Notes.Add(settingsRefs + " asset(s) referenced from ProjectSettings");

            // Preloaded assets.
            try
            {
                foreach (var o in PlayerSettings.GetPreloadedAssets())
                {
                    if (o == null) continue;
                    string p = AssetDatabase.GetAssetPath(o);
                    if (!string.IsNullOrEmpty(p)) roots.Add(p);
                }
            }
            catch (Exception e)
            {
                result.Notes.Add("Could not read preloaded assets: " + e.Message);
            }

            // AssetBundle assignments.
            int bundled = 0;
            foreach (string name in AssetDatabase.GetAllAssetBundleNames())
            {
                foreach (string p in AssetDatabase.GetAssetPathsFromAssetBundle(name))
                {
                    if (roots.Add(p)) bundled++;
                }
            }
            result.Notes.Add(bundled == 0
                ? "No AssetBundle assignments found"
                : bundled + " asset(s) assigned to AssetBundles treated as roots");

            // Addressables, if the package happens to be installed.
            int addressable = AddressablesProbe.AddRoots(roots, result);
            result.Notes.Add(addressable < 0
                ? "Addressables package not installed - nothing to check"
                : addressable + " Addressable entr(ies) treated as roots");

            return roots.ToList();
        }

        static IEnumerable<string> CollectProjectSettingsReferences()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings");
            if (!Directory.Exists(dir)) yield break;

            var rx = new Regex(@"guid:\s*([0-9a-f]{32})", RegexOptions.IgnoreCase);
            foreach (string file in Directory.GetFiles(dir, "*.asset"))
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch { continue; }

                foreach (Match m in rx.Matches(text))
                {
                    string path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                    if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal))
                        yield return path;
                }
            }
        }

        // -------------------------------------------------------- classification

        static AssetEntry Classify(string path, HashSet<string> referenced, HashSet<string> literals)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            var entry = new AssetEntry
            {
                Path = path,
                Name = Path.GetFileName(path),
                Extension = ext,
                Bytes = FileSize(path),
                TypeName = TypeNameOf(path)
            };

            if (ProtectedExtensions.Contains(ext))
            {
                entry.Classification = Classification.Protected;
                entry.Reason = ext + " is never offered for deletion";
                return entry;
            }

            if (IsStringLoaded(path))
            {
                entry.Classification = Classification.Review;
                entry.Reason = "Lives in a folder loaded by name at runtime";
                return entry;
            }

            if (IsEditorAsset(path))
            {
                entry.Classification = Classification.Review;
                entry.Reason = "Editor-only asset - not shipped, but tooling may need it";
                return entry;
            }

            if (IsVendored(path) && !referenced.Contains(path))
            {
                entry.Classification = Classification.Review;
                entry.Reason = "Vendored package content - unreferenced, but deleting it breaks the vendor's reimport";
                return entry;
            }

            if (referenced.Contains(path))
            {
                entry.Classification = Classification.Referenced;
                entry.Reason = "Reachable from a scene, prefab or settings root";
                return entry;
            }

            var importer = AssetImporter.GetAtPath(path);
            if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName))
            {
                entry.Classification = Classification.Review;
                entry.Reason = "Assigned to AssetBundle '" + importer.assetBundleName + "'";
                return entry;
            }

            string bare = Path.GetFileNameWithoutExtension(path);
            if (literals.Contains(bare))
            {
                entry.Classification = Classification.Review;
                entry.Reason = "Its name appears as a string literal in a script - may be loaded dynamically";
                return entry;
            }

            entry.Classification = Classification.Unused;
            entry.Reason = "No path from any root";
            return entry;
        }

        static bool IsStringLoaded(string path)
        {
            foreach (string frag in StringLoadedFolders)
                if (path.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static bool IsVendored(string path)
        {
            foreach (string frag in VendoredFolders)
                if (path.StartsWith(frag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool IsEditorAsset(string path)
        {
            return path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0
                   || path.StartsWith("Assets/Editor/", StringComparison.OrdinalIgnoreCase);
        }

        static long FileSize(string assetPath)
        {
            try
            {
                string abs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
                return File.Exists(abs) ? new FileInfo(abs).Length : 0;
            }
            catch { return 0; }
        }

        static string TypeNameOf(string path)
        {
            var t = AssetDatabase.GetMainAssetTypeAtPath(path);
            return t != null ? t.Name : Path.GetExtension(path).TrimStart('.');
        }

        static HashSet<string> BuildScriptLiteralIndex()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rx = new Regex("\"([^\"\\r\\n]{1,200})\"");

            string[] files;
            try { files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories); }
            catch { return set; }

            foreach (string file in files)
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch { continue; }

                foreach (Match m in rx.Matches(text))
                {
                    string s = m.Groups[1].Value;
                    if (s.Length == 0) continue;
                    set.Add(s);

                    int slash = s.LastIndexOf('/');
                    if (slash >= 0 && slash < s.Length - 1) set.Add(s.Substring(slash + 1));
                }
            }
            return set;
        }

        /// <summary>Re-checks a specific set of paths right before deletion.</summary>
        public static HashSet<string> RecheckReferenced(IEnumerable<string> paths)
        {
            var allPaths = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .ToArray();

            var throwaway = new ScanResult();
            var roots = CollectRoots(allPaths, throwaway);

            var referenced = new HashSet<string>(
                AssetDatabase.GetDependencies(roots.ToArray(), true), StringComparer.Ordinal);
            foreach (string r in roots) referenced.Add(r);

            return new HashSet<string>(paths.Where(referenced.Contains), StringComparer.Ordinal);
        }
    }
}
