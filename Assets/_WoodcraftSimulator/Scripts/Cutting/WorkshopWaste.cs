using UnityEngine;

namespace WoodcraftSimulator.Cutting
{
    /// <summary>Scene holder of the workshop's <see cref="MaterialWasteLog"/> (shared by all saws).</summary>
    public sealed class WorkshopWaste : MonoBehaviour
    {
        public MaterialWasteLog Log { get; } = new MaterialWasteLog();
    }
}
