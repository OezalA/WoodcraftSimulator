using UnityEngine;

namespace WoodcraftSimulator.Audio
{
    /// <summary>What a footstep lands on (Phase 13d, D-061).</summary>
    public enum FootstepSurface
    {
        Concrete,
        Wood,
        Grass,
        Snow,
        Sand
    }

    /// <summary>
    /// The recorded sounds (CC0, Phase 13d, D-061) as one asset the builder fills from the files in the project. Every
    /// list may be empty: the caller then keeps its generated placeholder sound (<see cref="Core.ProceduralSounds"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "WoodcraftSimulator/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Header("Footsteps")]
        public AudioClip[] footstepsConcrete = System.Array.Empty<AudioClip>();
        public AudioClip[] footstepsWood = System.Array.Empty<AudioClip>();
        public AudioClip[] footstepsGrass = System.Array.Empty<AudioClip>();
        public AudioClip[] footstepsSnow = System.Array.Empty<AudioClip>();

        [Header("Putting things down")]
        public AudioClip[] impactWood = System.Array.Empty<AudioClip>();
        public AudioClip[] impactMetal = System.Array.Empty<AudioClip>();
        public AudioClip[] impactGlass = System.Array.Empty<AudioClip>();

        [Header("Interface")]
        public AudioClip[] uiClick = System.Array.Empty<AudioClip>();
        public AudioClip[] uiConfirm = System.Array.Empty<AudioClip>();
        public AudioClip[] uiError = System.Array.Empty<AudioClip>();

        /// <summary>A random clip of the list, or null when it is empty.</summary>
        public static AudioClip Any(AudioClip[] clips) =>
            clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];

        /// <summary>Footstep clips for a surface (sand: the snow crunch, played higher by the caller).</summary>
        public AudioClip[] Footsteps(FootstepSurface surface) => surface switch
        {
            FootstepSurface.Wood => footstepsWood,
            FootstepSurface.Grass => footstepsGrass,
            FootstepSurface.Snow => footstepsSnow,
            FootstepSurface.Sand => footstepsSnow,
            _ => footstepsConcrete
        };
    }
}
