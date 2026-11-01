using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoadSystem.Editor
{
    /// <summary>
    /// Bakes a procedural RoadNetworkManager hierarchy in place. For every road
    /// and intersection we:
    ///   1. Save the current runtime Mesh as a .asset and point the MeshFilter
    ///      at the saved asset.
    ///   2. Disable (NOT destroy) the procedural generator components:
    ///        RoadSegment, LaneSplineMirror, PedestrianSpline, RoadIntersection.
    ///   3. Mark the GameObject static so it can be batched, lightmapped, etc.
    ///
    /// Disabling instead of removing means the user can flip the components
    /// back on later to resume procedural editing — the bake is reversible.
    /// While the components are disabled they don't run OnEnable / OnDisable
    /// callbacks and don't overwrite the saved MeshFilter reference.
    ///
    /// Spline_VehiclePath / Spline_PedestrianPath GameObjects and their
    /// SplineContainer data are NOT touched — driving and pedestrian simulation
    /// read from those.
    /// </summary>
    public static class RoadNetworkBaker
    {
        const string MeshAssetRoot = "Assets/RoadSystem/BakedMeshes";

        public static void Bake(RoadNetworkManager manager)
        {
            if (manager == null) return;

            string networkName = manager.gameObject.name;
            string folder = $"{MeshAssetRoot}/{networkName}";
            EnsureFolder(folder);

            // Make sure procedural meshes reflect the latest settings (Tiling /
            // Width / SegmentsPerMeter / corner curvature / etc.) before we
            // freeze them as assets.
            manager.SyncGlobalSettingsToIntersections();
            manager.SyncGlobalSettingsToRoads();
            manager.RebuildAllMeshes();

            int roadCount = 0;
            int intersectionCount = 0;
            int laneMirrorCount = 0;
            int pedestrianCount = 0;

            // ── Roads ─────────────────────────────────────────────────────────
            var roads = manager.GetComponentsInChildren<RoadSegment>(true);
            foreach (var road in roads)
            {
                if (road == null) continue;

                var roadGo = road.gameObject;
                var mf = road.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0)
                    continue;

                // 1. Save the runtime mesh as a .asset and swap the MeshFilter
                //    to reference it.
                string meshPath = $"{folder}/Road_{SanitizeName(road.name)}_{roadCount}.asset";
                var savedMesh = SaveMesh(mf.sharedMesh, meshPath);
                Undo.RecordObject(mf, "Bake Road Network");
                mf.sharedMesh = savedMesh;

                // 2. Mark the road GameObject static so it can be batched /
                //    lightmapped / used by NavMesh.
                GameObjectUtility.SetStaticEditorFlags(roadGo, EveryStaticFlag());

                // 3. Disable lane mirror scripts in this road's descendants.
                //    They keep their last-synced data on the SplineContainer
                //    (which is what driving logic reads). Disabling instead of
                //    removing means the user can flip them back on later.
                var mirrors = roadGo.GetComponentsInChildren<LaneSplineMirror>(true);
                foreach (var mirror in mirrors)
                {
                    if (mirror == null) continue;
                    if (!mirror.enabled) continue;
                    Undo.RecordObject(mirror, "Bake Road Network");
                    mirror.enabled = false;
                    laneMirrorCount++;
                }

                // 3b. Same treatment for pedestrian splines — keep the
                //     SplineContainer data, just stop the script from running.
                var pedestrians = roadGo.GetComponentsInChildren<PedestrianSpline>(true);
                foreach (var ped in pedestrians)
                {
                    if (ped == null) continue;
                    if (!ped.enabled) continue;
                    Undo.RecordObject(ped, "Bake Road Network");
                    ped.enabled = false;
                    pedestrianCount++;
                }

                // 4. Disable the RoadSegment script. Its OnDisable destroys
                //    the (now orphaned) runtime mesh — the MeshFilter already
                //    points at the saved asset, so rendering keeps working.
                if (road.enabled)
                {
                    Undo.RecordObject(road, "Bake Road Network");
                    road.enabled = false;
                }
                roadCount++;
            }

            // ── Intersections ─────────────────────────────────────────────────
            var intersections = manager.GetComponentsInChildren<RoadIntersection>(true);
            foreach (var ix in intersections)
            {
                if (ix == null) continue;

                var ixGo = ix.gameObject;
                var mf = ix.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0)
                    continue;

                string meshPath = $"{folder}/Intersection_{intersectionCount}.asset";
                var savedMesh = SaveMesh(mf.sharedMesh, meshPath);
                Undo.RecordObject(mf, "Bake Road Network");
                mf.sharedMesh = savedMesh;

                GameObjectUtility.SetStaticEditorFlags(ixGo, EveryStaticFlag());

                // Disable the RoadIntersection script. While disabled, its
                // OnEnable won't run on scene load, so it can't overwrite the
                // saved MeshFilter reference. Re-enable to resume procedural
                // editing.
                if (ix.enabled)
                {
                    Undo.RecordObject(ix, "Bake Road Network");
                    ix.enabled = false;
                }
                intersectionCount++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[RoadNetworkBaker] Baked {roadCount} roads + " +
                      $"{intersectionCount} intersections in place. " +
                      $"Disabled {laneMirrorCount} lane mirror script(s) and " +
                      $"{pedestrianCount} pedestrian spline script(s). " +
                      $"Mesh assets saved under '{folder}'. " +
                      "Spline_VehiclePath / Spline_PedestrianPath containers preserved " +
                      "for driving and pedestrian simulation.");

            EditorUtility.SetDirty(manager.gameObject);
            Selection.activeGameObject = manager.gameObject;
            EditorGUIUtility.PingObject(manager.gameObject);
        }

        /// <summary>
        /// Re-enables every RoadSegment, LaneSplineMirror, PedestrianSpline,
        /// and RoadIntersection in the manager hierarchy. Their OnEnable
        /// callbacks then rebuild the procedural meshes / synced derived
        /// splines, restoring the live editing flow. Saved Mesh assets on
        /// disk are kept (the next Bake will simply overwrite them).
        /// </summary>
        public static void Unbake(RoadNetworkManager manager)
        {
            if (manager == null) return;

            int roadCount = 0;
            int intersectionCount = 0;
            int laneMirrorCount = 0;
            int pedestrianCount = 0;

            // Derived splines first so they're ready when a re-enabled
            // RoadSegment.OnEnable -> SyncLaneMirrors / SyncPedestrianSplines
            // fires.
            var mirrors = manager.GetComponentsInChildren<LaneSplineMirror>(true);
            foreach (var m in mirrors)
            {
                if (m == null) continue;
                if (m.enabled) continue;
                Undo.RecordObject(m, "Unbake Road Network");
                m.enabled = true;
                laneMirrorCount++;
            }

            var pedestrians = manager.GetComponentsInChildren<PedestrianSpline>(true);
            foreach (var p in pedestrians)
            {
                if (p == null) continue;
                if (p.enabled) continue;
                Undo.RecordObject(p, "Unbake Road Network");
                p.enabled = true;
                pedestrianCount++;
            }

            var roads = manager.GetComponentsInChildren<RoadSegment>(true);
            foreach (var road in roads)
            {
                if (road == null) continue;
                if (road.enabled) continue;
                Undo.RecordObject(road, "Unbake Road Network");
                road.enabled = true;
                roadCount++;
            }

            var intersections = manager.GetComponentsInChildren<RoadIntersection>(true);
            foreach (var ix in intersections)
            {
                if (ix == null) continue;
                if (ix.enabled) continue;
                Undo.RecordObject(ix, "Unbake Road Network");
                ix.enabled = true;
                intersectionCount++;
            }

            Debug.Log($"[RoadNetworkBaker] Unbaked: re-enabled {roadCount} road(s), " +
                      $"{intersectionCount} intersection(s), {laneMirrorCount} lane mirror(s), " +
                      $"{pedestrianCount} pedestrian spline(s). " +
                      "Procedural mesh generation resumed. " +
                      "Saved Mesh assets on disk were not deleted.");

            EditorUtility.SetDirty(manager.gameObject);
        }

        // ─────────── helpers ───────────

        static Mesh SaveMesh(Mesh source, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(source, existing);
                return existing;
            }
            var copy = Object.Instantiate(source);
            copy.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static StaticEditorFlags EveryStaticFlag()
        {
            return StaticEditorFlags.ContributeGI
                 | StaticEditorFlags.OccluderStatic
                 | StaticEditorFlags.BatchingStatic
                 | StaticEditorFlags.OccludeeStatic
                 | StaticEditorFlags.ReflectionProbeStatic;
        }

        static string SanitizeName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }
    }
}
