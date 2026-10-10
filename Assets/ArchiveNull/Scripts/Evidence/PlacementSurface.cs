using UnityEngine;

namespace ArchiveNull.Evidence
{
    // Queries ignore the operator, including scenes whose camera sits above its capsule.
    public static class PlacementSurface
    {
        private static readonly RaycastHit[] hits = new RaycastHit[128];
        private static readonly Collider[] overlaps = new Collider[128];
        private static BoxCollider probe;
        public static bool Raycast(Ray ray, float distance, Transform player, out RaycastHit nearest)
        {
            nearest = default;
            float best = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(ray, hits, distance, ~(1 << 31), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(player) || hits[i].distance >= best) continue;
                nearest = hits[i]; best = nearest.distance;
            }
            return best < float.PositiveInfinity;
        }
        public static bool Clear(Vector3 center, Vector3 halfSize, Quaternion rotation, Transform player, Transform ignored = null)
        {
            if (probe == null)
            {
                var go = new GameObject("Placement volume query") { hideFlags = HideFlags.HideAndDontSave };
                probe = go.AddComponent<BoxCollider>(); probe.enabled = false;
            }
            probe.size = halfSize * 2; probe.center = Vector3.zero;
            int count = Physics.OverlapBoxNonAlloc(center, halfSize, overlaps, rotation, ~(1 << 31), QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider other = overlaps[i];
                if (other == probe || other.transform.IsChildOf(player) || ignored != null && other.transform.IsChildOf(ignored)) continue;
                // Mesh bounds can cover an entire room. Reject actual penetration, not its bounds.
                if (Physics.ComputePenetration(probe, center, rotation, other, other.transform.position,
                    other.transform.rotation, out _, out float depth) && depth > .002f) return false;
            }
            return true;
        }
    }
}
