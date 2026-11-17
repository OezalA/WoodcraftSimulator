using System;
using System.Collections.Generic;

namespace WoodcraftSimulator.Persistence
{
    /// <summary>
    /// The save holds every place's state (D-056): each entry of a place-bound list (boards, assemblies, sites, bins,
    /// switches, cut lists) carries its place. Only the place loaded right now is captured from the scene; the others
    /// keep what was captured when they were left. Older saves have no place: everything was in the workshop.
    /// </summary>
    public static class SaveLocations
    {
        public const string Workshop = "workshop";

        /// <summary>The place an entry belongs to (empty = the workshop).</summary>
        public static string Of(string location) => string.IsNullOrEmpty(location) ? Workshop : location;

        /// <summary>The entries of one place.</summary>
        public static List<T> At<T>(IEnumerable<T> entries, string location, Func<T, string> locationOf)
        {
            var result = new List<T>();
            foreach (T entry in entries ?? Array.Empty<T>())
            {
                if (entry != null && Of(locationOf(entry)) == Of(location))
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>All entries of other places, followed by the new entries of this one.</summary>
        public static T[] Replace<T>(IEnumerable<T> entries, string location, Func<T, string> locationOf,
            IEnumerable<T> replacement)
        {
            var result = new List<T>();
            foreach (T entry in entries ?? Array.Empty<T>())
            {
                if (entry != null && Of(locationOf(entry)) != Of(location))
                {
                    result.Add(entry);
                }
            }

            result.AddRange(replacement ?? Array.Empty<T>());
            return result.ToArray();
        }
    }
}
