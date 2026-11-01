using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnusedAssetCleanup
{
    internal class QuarantineResult
    {
        public string Directory;
        public readonly List<string> Moved = new List<string>();
        public readonly List<string> Blocked = new List<string>();
        public readonly List<string> Failed = new List<string>();
        public long BytesFreed;
    }

    /// <summary>
    /// Nothing is deleted. Files and their .meta are moved out of Assets into a timestamped
    /// folder beside the project, which keeps GUIDs intact if they have to come back.
    /// </summary>
    internal static class AssetQuarantine
    {
        public static string ProjectRoot
        {
            get
            {
                var parent = System.IO.Directory.GetParent(Application.dataPath);
                return parent != null ? parent.FullName : Application.dataPath;
            }
        }

        public static string QuarantineRoot
        {
            get { return Path.Combine(ProjectRoot, "AssetQuarantine"); }
        }

        public static QuarantineResult Quarantine(List<AssetEntry> entries)
        {
            var result = new QuarantineResult();
            var paths = entries.Select(e => e.Path).ToList();
            if (paths.Count == 0) return result;

            // Last line of defence: rebuild the dependency graph and drop anything that
            // became reachable since the scan.
            EditorUtility.DisplayProgressBar("Unused Asset Cleanup", "Re-checking dependencies...", 0.2f);
            HashSet<string> nowReferenced;
            try { nowReferenced = UnusedAssetScanner.RecheckReferenced(paths); }
            finally { EditorUtility.ClearProgressBar(); }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            result.Directory = Path.Combine(QuarantineRoot, stamp);
            System.IO.Directory.CreateDirectory(result.Directory);

            var manifest = new StringBuilder();
            manifest.AppendLine("# Unused Asset Cleanup quarantine");
            manifest.AppendLine("# " + DateTime.Now.ToString("u"));
            manifest.AppendLine("# Restore with Tools > Unused Asset Cleanup > Restore From Quarantine");
            manifest.AppendLine();

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    EditorUtility.DisplayProgressBar("Unused Asset Cleanup",
                        "Quarantining " + entry.Name, (float)i / entries.Count);

                    if (nowReferenced.Contains(entry.Path))
                    {
                        result.Blocked.Add(entry.Path + "  (became referenced since the scan)");
                        continue;
                    }

                    try
                    {
                        string src = Path.Combine(ProjectRoot, entry.Path);
                        string dst = Path.Combine(result.Directory,
                            entry.Path.Replace('/', Path.DirectorySeparatorChar));

                        if (!File.Exists(src))
                        {
                            result.Failed.Add(entry.Path + "  (file missing)");
                            continue;
                        }

                        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(dst));
                        File.Move(src, dst);
                        if (File.Exists(src + ".meta")) File.Move(src + ".meta", dst + ".meta");

                        result.Moved.Add(entry.Path);
                        result.BytesFreed += entry.Bytes;
                        manifest.AppendLine(entry.Path);
                    }
                    catch (Exception e)
                    {
                        result.Failed.Add(entry.Path + "  (" + e.Message + ")");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            File.WriteAllText(Path.Combine(result.Directory, "manifest.txt"), manifest.ToString());
            AssetDatabase.Refresh(ImportAssetOptions.Default);
            return result;
        }

        public static int Restore(string quarantineDir)
        {
            string manifest = Path.Combine(quarantineDir, "manifest.txt");
            if (!File.Exists(manifest)) return -1;

            var paths = File.ReadAllLines(manifest)
                            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
                            .ToList();

            int restored = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string assetPath in paths)
                {
                    string src = Path.Combine(quarantineDir, assetPath.Replace('/', Path.DirectorySeparatorChar));
                    string dst = Path.Combine(ProjectRoot, assetPath);
                    if (!File.Exists(src)) continue;

                    System.IO.Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.Copy(src, dst, true);
                    if (File.Exists(src + ".meta")) File.Copy(src + ".meta", dst + ".meta", true);
                    restored++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return restored;
        }

        public static string BuildPreview(List<AssetEntry> entries)
        {
            var sb = new StringBuilder();
            long total = entries.Sum(e => e.Bytes);
            sb.AppendLine(entries.Count + " asset(s) will be moved out of Assets, freeing " + Size(total) + ".");
            sb.AppendLine("Originals and their .meta files go to " + QuarantineRoot + "\\<timestamp>.");
            sb.AppendLine();

            foreach (var group in entries.GroupBy(e => e.TypeName).OrderByDescending(g => g.Sum(e => e.Bytes)))
                sb.AppendLine(string.Format("{0,-22} {1,4} file(s)   {2}",
                    group.Key, group.Count(), Size(group.Sum(e => e.Bytes))));

            sb.AppendLine();
            foreach (var e in entries.OrderByDescending(e => e.Bytes).Take(40))
                sb.AppendLine("  " + Size(e.Bytes).PadLeft(10) + "  " + e.Path);
            if (entries.Count > 40) sb.AppendLine("  ... and " + (entries.Count - 40) + " more");

            return sb.ToString();
        }

        public static string Size(long bytes)
        {
            if (bytes >= 1048576) return (bytes / 1048576f).ToString("0.00") + " MB";
            if (bytes >= 1024) return (bytes / 1024f).ToString("0.0") + " KB";
            return bytes + " B";
        }
    }
}
