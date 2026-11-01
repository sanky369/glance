using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace UnusedAssetCleanup
{
    /// <summary>
    /// Addressables is not in this project's manifest, but the tool has to stay correct if it is
    /// ever added. Everything here goes through reflection so the scripts compile either way.
    /// </summary>
    internal static class AddressablesProbe
    {
        /// <summary>Adds every addressable entry path to <paramref name="roots"/>.
        /// Returns the number added, or -1 when the package is not installed.</summary>
        public static int AddRoots(HashSet<string> roots, ScanResult result)
        {
            try
            {
                Type defaultObject = FindType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject");
                if (defaultObject == null) return -1;

                PropertyInfo settingsProp = defaultObject.GetProperty(
                    "Settings", BindingFlags.Public | BindingFlags.Static);
                object settings = settingsProp != null ? settingsProp.GetValue(null) : null;
                if (settings == null) return 0;

                PropertyInfo groupsProp = settings.GetType().GetProperty("groups");
                var groups = groupsProp != null ? groupsProp.GetValue(settings) as IEnumerable : null;
                if (groups == null) return 0;

                int added = 0;
                foreach (object group in groups)
                {
                    if (group == null) continue;

                    PropertyInfo entriesProp = group.GetType().GetProperty("entries");
                    var entries = entriesProp != null ? entriesProp.GetValue(group) as IEnumerable : null;
                    if (entries == null) continue;

                    foreach (object entry in entries)
                    {
                        if (entry == null) continue;
                        PropertyInfo pathProp = entry.GetType().GetProperty("AssetPath");
                        string path = pathProp != null ? pathProp.GetValue(entry) as string : null;
                        if (!string.IsNullOrEmpty(path) && roots.Add(path)) added++;
                    }
                }
                return added;
            }
            catch (Exception e)
            {
                if (result != null)
                    result.Notes.Add("Addressables probe failed (treating nothing as addressable): " + e.Message);
                return 0;
            }
        }

        static Type FindType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, false); }
                catch { }
                if (t != null) return t;
            }
            return null;
        }
    }
}
