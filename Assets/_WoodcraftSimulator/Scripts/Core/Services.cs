using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WoodcraftSimulator.Core
{
    /// <summary>
    /// Finds the one object of a type in any loaded scene (Phase 12.5, D-056). Unity can't keep serialized references
    /// between scenes, so an object in the workshop or a customer location reaches the core services (economy, career,
    /// jobs, save system, player) through here; a serialized reference, where the builder could set one, still wins
    /// (see <see cref="Resolve{T}"/>). Results are cached until the object is destroyed (a scene unloaded).
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, Object> Cache = new Dictionary<Type, Object>();

        /// <summary>The single enabled-or-not object of this type in the loaded scenes, or null.</summary>
        public static T Get<T>() where T : Object
        {
            if (Cache.TryGetValue(typeof(T), out Object cached) && cached != null)
            {
                return (T)cached;
            }

            T found = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (found != null)
            {
                Cache[typeof(T)] = found;
            }
            else
            {
                Cache.Remove(typeof(T));
            }

            return found;
        }

        /// <summary>
        /// The serialized reference if set, otherwise the service from the loaded scenes (stored back into the field so
        /// the lookup happens once): <c>economy = Services.Resolve(economy);</c>.
        /// </summary>
        public static T Resolve<T>(T assigned) where T : Object => assigned != null ? assigned : Get<T>();

        /// <summary>Forgets cached objects (e.g. after scenes were replaced, or between Play sessions).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => Cache.Clear();
    }
}
