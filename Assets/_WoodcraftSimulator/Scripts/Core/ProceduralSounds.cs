using System;
using UnityEngine;

namespace WoodcraftSimulator.Core
{
    /// <summary>
    /// Placeholder sounds generated in code (Phase 8d, D-044): no audio files, so nothing to license. Each clip is
    /// built once and cached. They are meant to be replaced by recorded or purchased sounds in the visual/audio pass
    /// (D-037); the call sites only need an <see cref="AudioClip"/>.
    /// </summary>
    public static class ProceduralSounds
    {
        private const int SampleRate = 44100;

        private static AudioClip saw;
        private static AudioClip drill;
        private static AudioClip click;
        private static AudioClip thunk;
        private static AudioClip coinIn;
        private static AudioClip coinOut;
        private static AudioClip ambience;

        /// <summary>Hand saw strokes: band-limited noise pulsing about 2.5 times per second (loops).</summary>
        public static AudioClip Saw => saw ??= Build("Saw", 0.8f, (t, noise) =>
        {
            float stroke = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2.5f));
            return noise * (0.15f + 0.85f * stroke) * 0.5f;
        }, lowPass: 0.35f);

        /// <summary>Drill motor: a whine with harmonics and a little noise (loops).</summary>
        public static AudioClip Drill => drill ??= Build("Drill", 1f, (t, noise) =>
        {
            float f = 2f * Mathf.PI * 230f * t;
            return (0.5f * Mathf.Sin(f) + 0.25f * Mathf.Sin(2f * f) + 0.12f * Mathf.Sin(3f * f) + 0.1f * noise) * 0.35f;
        });

        /// <summary>The clutch slipping or a screw seating: a short click.</summary>
        public static AudioClip Click => click ??= Build("Click", 0.05f, (t, noise) =>
            (noise * 0.6f + Mathf.Sin(2f * Mathf.PI * 2200f * t) * 0.4f) * Mathf.Exp(-t * 90f));

        /// <summary>Picking up or putting down: a soft low knock.</summary>
        public static AudioClip Thunk => thunk ??= Build("Thunk", 0.15f, (t, noise) =>
            (Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.7f + noise * 0.3f) * Mathf.Exp(-t * 30f) * 0.8f, lowPass: 0.2f);

        /// <summary>Money received: two rising tones.</summary>
        public static AudioClip CoinIn => coinIn ??= Build("CoinIn", 0.3f, (t, noise) =>
            Mathf.Sin(2f * Mathf.PI * (t < 0.12f ? 880f : 1320f) * t) * Mathf.Exp(-(t % 0.15f) * 18f) * 0.35f);

        /// <summary>Money spent: two falling tones.</summary>
        public static AudioClip CoinOut => coinOut ??= Build("CoinOut", 0.3f, (t, noise) =>
            Mathf.Sin(2f * Mathf.PI * (t < 0.12f ? 990f : 660f) * t) * Mathf.Exp(-(t % 0.15f) * 18f) * 0.3f);

        /// <summary>Room tone: very quiet, deep noise (loops).</summary>
        public static AudioClip Ambience => ambience ??= Build("Ambience", 4f, (t, noise) => noise * 0.25f, lowPass: 0.02f);

        /// <param name="lowPass">One-pole low-pass coefficient for the noise (1 = none, smaller = darker).</param>
        private static AudioClip Build(string name, float seconds, Func<float, float, float> sample, float lowPass = 1f)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            var random = new System.Random(name.GetHashCode());
            float filtered = 0f;
            for (int i = 0; i < count; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered += lowPass * (white - filtered);
                float noise = lowPass < 1f ? filtered / Mathf.Sqrt(lowPass) : white;
                data[i] = Mathf.Clamp(sample(i / (float)SampleRate, noise), -1f, 1f);
            }

            // Short fades at both ends so loops and one-shots don't click.
            int fade = Mathf.Min(count / 4, SampleRate / 200);
            for (int i = 0; i < fade; i++)
            {
                float gain = i / (float)fade;
                data[i] *= gain;
                data[count - 1 - i] *= gain;
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>A looping sound on an <see cref="AudioSource"/> that is switched on and off by a condition each frame.</summary>
    public sealed class LoopSound
    {
        private readonly AudioSource source;

        public LoopSound(AudioSource source, AudioClip clip, float volume)
        {
            this.source = source;
            if (source != null)
            {
                source.clip = clip;
                source.loop = true;
                source.volume = volume;
                source.playOnAwake = false;
            }
        }

        public void Set(bool playing)
        {
            if (source == null || source.isPlaying == playing)
            {
                return;
            }

            if (playing)
            {
                source.Play();
            }
            else
            {
                source.Stop();
            }
        }
    }
}
