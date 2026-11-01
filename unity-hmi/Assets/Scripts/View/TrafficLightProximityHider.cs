using System.Collections.Generic;
using UnityEngine;

namespace HMI_Hub_Template
{
    public class TrafficLightProximityHider : MonoBehaviour
    {
        [Tooltip("Distance at which traffic light renderers are disabled.")]
        public float hideRadius = 8f;

        private struct Entry
        {
            public TrafficLightView view;
            public Renderer[]       renderers;
            public bool             hidden;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        void Start() => Rebuild();

        public void Rebuild()
        {
            _entries.Clear();
            foreach (var tlv in FindObjectsByType<TrafficLightView>(FindObjectsSortMode.None))
            {
                _entries.Add(new Entry
                {
                    view      = tlv,
                    renderers = tlv.GetComponentsInChildren<Renderer>(includeInactive: true),
                    hidden    = false
                });
            }
        }

        void LateUpdate()
        {
            Vector3 camPos      = transform.position;
            float   hideRadiusSq = hideRadius * hideRadius;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.view == null) { _entries.RemoveAt(i); continue; }

                bool tooClose = (camPos - entry.view.transform.position).sqrMagnitude < hideRadiusSq;

                if (tooClose != entry.hidden)
                {
                    foreach (var r in entry.renderers)
                        if (r != null) r.enabled = !tooClose;

                    entry.hidden = tooClose;
                    _entries[i]  = entry;
                }
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, hideRadius);
        }
#endif
    }
}
