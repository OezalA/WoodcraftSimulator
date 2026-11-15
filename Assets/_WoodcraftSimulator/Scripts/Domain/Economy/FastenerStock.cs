using System;
using System.Collections.Generic;

namespace WoodcraftSimulator.Economy
{
    /// <summary>
    /// Loose fasteners in the workshop (owner's choice K4): bought by the box, used one per driven screw. When a type
    /// runs out it can't be driven until a new box is bought.
    /// </summary>
    public sealed class FastenerStock
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public event Action Changed;

        /// <summary>Fastener id → count, only ids with a count above zero.</summary>
        public IReadOnlyDictionary<string, int> All => counts;

        public int Count(string fastenerId) =>
            fastenerId != null && counts.TryGetValue(fastenerId, out int count) ? count : 0;

        public void Add(string fastenerId, int count)
        {
            if (string.IsNullOrWhiteSpace(fastenerId)) throw new ArgumentException("Fastener id must not be empty.", nameof(fastenerId));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Count must not be negative.");
            if (count == 0)
            {
                return;
            }

            counts[fastenerId] = Count(fastenerId) + count;
            Changed?.Invoke();
        }

        /// <summary>Takes <paramref name="count"/> fasteners if there are enough; otherwise nothing changes.</summary>
        public bool TryUse(string fastenerId, int count = 1)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be positive.");

            int available = Count(fastenerId);
            if (available < count)
            {
                return false;
            }

            if (available == count)
            {
                counts.Remove(fastenerId);
            }
            else
            {
                counts[fastenerId] = available - count;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Replaces all counts (loading a save).</summary>
        public void Restore(IEnumerable<KeyValuePair<string, int>> restored)
        {
            if (restored == null) throw new ArgumentNullException(nameof(restored));

            var next = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> entry in restored)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 0 || next.ContainsKey(entry.Key))
                {
                    throw new ArgumentException($"Invalid fastener count '{entry.Key}': {entry.Value}.", nameof(restored));
                }

                if (entry.Value > 0)
                {
                    next.Add(entry.Key, entry.Value);
                }
            }

            counts.Clear();
            foreach (KeyValuePair<string, int> entry in next)
            {
                counts.Add(entry.Key, entry.Value);
            }

            Changed?.Invoke();
        }
    }
}
