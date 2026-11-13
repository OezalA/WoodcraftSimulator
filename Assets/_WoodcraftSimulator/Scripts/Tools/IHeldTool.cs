using System;
using UnityEngine;

namespace WoodcraftSimulator.Tools
{
    /// <summary>What a tool in hand needs to know about its user.</summary>
    public sealed class HeldToolContext
    {
        public HeldToolContext(Transform eyes, Transform playerRoot)
        {
            Eyes = eyes;
            PlayerRoot = playerRoot;
        }

        /// <summary>The player camera: origin and direction of aiming.</summary>
        public Transform Eyes { get; }

        /// <summary>Root of the player hierarchy; casts from the eyes must skip it (D-023).</summary>
        public Transform PlayerRoot { get; }
    }

    /// <summary>
    /// Common contract for tools used from the hand (tape measure, pencil, later saws, drills ...).
    /// The tool belt equips the tool when it is taken in hand and unequips it when holstered; between those
    /// calls the tool's own Update does the work. One shared shape for all tools (master prompt §6.3).
    /// </summary>
    public interface IHeldTool
    {
        void Equip(HeldToolContext context);

        void Unequip();

        /// <summary>Short live text for the HUD (e.g. a tape reading), or null when there is nothing to show.</summary>
        string Readout { get; }

        /// <summary>Raised when <see cref="Readout"/> changed.</summary>
        event Action ReadoutChanged;
    }
}
