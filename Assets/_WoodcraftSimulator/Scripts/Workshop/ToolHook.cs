using UnityEngine;
using WoodcraftSimulator.Interaction;
using WoodcraftSimulator.Inventory;
using WoodcraftSimulator.Localization;

namespace WoodcraftSimulator.Workshop
{
    /// <summary>
    /// A place on the tool wall for one tool (Phase 8a, D-044). With that tool on the belt, E hangs it here; the
    /// hanging tool is picked up like any other (its own collider is in front of the hook). The tool hangs at this
    /// transform's position and rotation; a painted outline shows where it belongs.
    /// </summary>
    public sealed class ToolHook : MonoBehaviour, IInteractable, IDescribable
    {
        [SerializeField] private ItemDefinition item;

        [Tooltip("Where the tool hangs (position and rotation); this object itself is the outline on the wall.")]
        [SerializeField] private Transform hangPoint;

        public ItemDefinition Item => item;

        public string Description => item != null
            ? Loc.T($"Place for the {item.DisplayName}", $"{item.DisplayName} yeri")
            : name;

        public string InteractionPrompt
        {
            get
            {
                if (item == null)
                {
                    return name;
                }

                PlayerToolBelt belt = FindPlayerBelt();
                return belt != null && belt.Find(item.Id) != null
                    ? Loc.T($"Hang the {item.DisplayName} on the wall", $"{item.DisplayName} duvara as")
                    : Description;
            }
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (item == null || !interactor.TryGetComponent(out PlayerToolBelt belt))
            {
                return;
            }

            PickupItem stored = belt.Find(item.Id);
            if (stored != null)
            {
                Transform pose = hangPoint != null ? hangPoint : transform;
                belt.TryRelease(stored, pose.position, pose.rotation);
            }
        }

        private static PlayerToolBelt cachedBelt;

        private static PlayerToolBelt FindPlayerBelt()
        {
            if (cachedBelt == null)
            {
                cachedBelt = FindFirstObjectByType<PlayerToolBelt>();
            }

            return cachedBelt;
        }
    }
}
