using UnityEngine;

namespace WoodcraftSimulator.Core
{
    /// <summary>
    /// Ray/sphere casts that ignore the colliders of one hierarchy (typically the player).
    /// Needed because casts starting inside the player's CharacterController DO report it when looking down
    /// (observed 2026-11-13: hit 'Player' at ~0.09 m), despite the general "origin inside is ignored" rule.
    /// Non-alloc: callers pass a reusable buffer. If more hits than the buffer holds exist, the closest valid
    /// hit may be missed (results are unordered); keep buffers comfortably large (16 is plenty here).
    /// </summary>
    public static class PhysicsQueries
    {
        public static bool RaycastIgnoring(Ray ray, float maxDistance, int layerMask, Transform ignoredRoot,
            RaycastHit[] buffer, out RaycastHit closest)
        {
            int count = Physics.RaycastNonAlloc(ray, buffer, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
            return FindClosest(buffer, count, ignoredRoot, out closest);
        }

        public static bool SphereCastIgnoring(Ray ray, float radius, float maxDistance, int layerMask,
            Transform ignoredRoot, RaycastHit[] buffer, out RaycastHit closest)
        {
            int count = Physics.SphereCastNonAlloc(ray, radius, buffer, maxDistance, layerMask,
                QueryTriggerInteraction.Ignore);
            return FindClosest(buffer, count, ignoredRoot, out closest);
        }

        private static bool FindClosest(RaycastHit[] buffer, int count, Transform ignoredRoot, out RaycastHit closest)
        {
            closest = default;
            bool found = false;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = buffer[i];
                if (ignoredRoot != null && hit.collider.transform.IsChildOf(ignoredRoot))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    closest = hit;
                    found = true;
                }
            }

            return found;
        }
    }
}
