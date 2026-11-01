using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnusedAssetCleanup
{
    /// <summary>
    /// Sanity checks for the scanner. The whole tool is only as trustworthy as its refusal to
    /// mark something essential as unused, so that is what this asserts.
    /// </summary>
    internal static class CleanupSelfTest
    {
        [MenuItem("Tools/Unused Asset Cleanup/Run Self Test", false, 100)]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Unused Asset Cleanup self test ===");

            var result = UnusedAssetScanner.Scan(null);

            sb.AppendFormat("roots={0}  reachable={1}  entries={2}  unused={3}\n",
                result.RootCount, result.ReferencedCount, result.Entries.Count,
                AssetQuarantine.Size(result.UnusedBytes));

            foreach (var g in result.Entries.GroupBy(e => e.Classification).OrderBy(g => g.Key))
                sb.AppendFormat("  {0,-12} {1,4}   {2}\n",
                    g.Key, g.Count(), AssetQuarantine.Size(g.Sum(e => e.Bytes)));

            int failures = 0;

            // 1. Nothing essential may ever be classified Unused.
            string[] essential = { ".cs", ".unity", ".asmdef", ".dll", ".inputactions" };
            var violations = result.Entries
                .Where(e => e.Classification == Classification.Unused
                            && essential.Any(x => e.Path.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            failures += Assert(sb, "no script/scene/asmdef marked Unused", violations.Count == 0,
                string.Join(", ", violations.Take(5).Select(v => v.Path).ToArray()));

            // 2. Anything under a Resources folder is loaded by name, so it must not be Unused.
            var resources = result.Entries.Where(e => e.Path.Contains("/Resources/")).ToList();
            failures += Assert(sb, "Resources content never Unused (" + resources.Count + " asset(s))",
                resources.All(e => e.Classification != Classification.Unused), "");

            // 3. Every dependency of every build scene must come back Referenced.
            var byPath = result.Entries.ToDictionary(e => e.Path, e => e);
            int checkedDeps = 0, wrong = 0;
            string firstWrong = "";
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                foreach (string dep in AssetDatabase.GetDependencies(scene.path, true))
                {
                    AssetEntry e;
                    if (!byPath.TryGetValue(dep, out e)) continue;
                    checkedDeps++;
                    if (e.Classification == Classification.Unused)
                    {
                        wrong++;
                        if (firstWrong.Length == 0) firstWrong = dep;
                    }
                }
            }
            failures += Assert(sb,
                "all " + checkedDeps + " build-scene dependencies are not Unused", wrong == 0, firstWrong);

            // 4. Round-trip: quarantine paths must resolve back to real files.
            var unused = result.Entries.Where(e => e.Classification == Classification.Unused).ToList();
            int missing = unused.Count(e => !File.Exists(Path.Combine(AssetQuarantine.ProjectRoot, e.Path)));
            failures += Assert(sb, "every Unused entry exists on disk", missing == 0, missing + " missing");

            // 5. The pre-delete recheck must agree with the scan.
            if (unused.Count > 0)
            {
                var sample = unused.OrderByDescending(e => e.Bytes).Take(25).Select(e => e.Path).ToList();
                var stillReferenced = UnusedAssetScanner.RecheckReferenced(sample);
                failures += Assert(sb,
                    "pre-delete recheck agrees on " + sample.Count + " sampled asset(s)",
                    stillReferenced.Count == 0,
                    string.Join(", ", stillReferenced.Take(3).ToArray()));
            }

            sb.AppendLine();
            sb.AppendLine("Top 15 unused by size:");
            foreach (var e in unused.OrderByDescending(e => e.Bytes).Take(15))
                sb.AppendFormat("  {0,10}  {1}\n", AssetQuarantine.Size(e.Bytes), e.Path);

            sb.AppendLine();
            sb.AppendLine(failures == 0 ? "SELF TEST: PASS" : "SELF TEST: " + failures + " FAILURE(S)");

            if (failures == 0) Debug.Log(sb.ToString());
            else Debug.LogError(sb.ToString());
        }

        static int Assert(StringBuilder sb, string label, bool ok, string detail)
        {
            sb.AppendFormat("  [{0}] {1}{2}\n", ok ? "PASS" : "FAIL", label,
                ok || string.IsNullOrEmpty(detail) ? "" : "  -> " + detail);
            return ok ? 0 : 1;
        }
    }
}
