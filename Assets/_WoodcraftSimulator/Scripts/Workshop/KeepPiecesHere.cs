using UnityEngine;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>
    /// Marks a storage place (the timber rack) whose pieces the <see cref="Apprentice"/> leaves alone when tidying
    /// up (D-052): timber lying on any collider below this object is in its place already.
    /// </summary>
    public sealed class KeepPiecesHere : MonoBehaviour
    {
    }
}
