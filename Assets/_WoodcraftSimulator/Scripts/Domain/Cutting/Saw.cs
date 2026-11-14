using System;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Cutting
{
    /// <summary>
    /// The cutting-relevant data of a saw. <see cref="Kerf"/> is the width of material the blade turns into sawdust
    /// (set of the teeth included), so every cut loses that much length.
    /// </summary>
    public sealed class Saw
    {
        public Saw(string id, string displayName, Length kerf)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Saw id must not be empty.", nameof(id));
            }

            if (kerf <= Length.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(kerf), kerf, "Kerf must be positive.");
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Kerf = kerf;
        }

        /// <summary>Stable id of the saw type, e.g. "hand-saw".</summary>
        public string Id { get; }

        public string DisplayName { get; }

        public Length Kerf { get; }

        public override string ToString() => $"{DisplayName} (kerf {Kerf})";
    }
}
